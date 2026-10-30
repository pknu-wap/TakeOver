using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TakeOver.Events;
using UnityEngine;

namespace TakeOver.NPC
{
    public sealed partial class NpcFeatureTestHarness
    {
        // NPC 소유 프리팹의 테스트 카탈로그다. 이벤트 SO를 읽기만 하며 타 팀 컴포넌트 참조는 저장하지 않는다.
        [SerializeField] private List<EventDefinition> previewEvents = new List<EventDefinition>();
        public string ActualEventPreviewReport { get; private set; } = "이벤트 이름을 누르고 선택 결과를 테스트하세요.";

        /// <summary>어느 선택에서든 현재 NPC를 대상으로 하는 실제 이벤트만 반환한다.</summary>
        public IReadOnlyList<EventDefinition> GetRelatedEvents() => previewEvents
            .Where(d => d != null && d.getChoices().Any(c => c.getNpcResult()?.targetNpcIds?.Contains(npcId) == true)).ToArray();

        /// <summary>실제 이벤트와 같은 치환 규칙에 현재 NPC 회사/대표를 공급한다. SO 원문은 변경하지 않는다.</summary>
        public TodayEventInfo GetPreviewEventInfo(EventDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var eventCompany = definition.hasCompanyConditions() ? companyId
                : definition.getCompanyIds().FirstOrDefault() ?? "";
            if (definition.hasCompanyConditions() && eventCompany == "external-network")
            {
                // 외부 인물의 회사 사건은 현재 발생 회사, 없으면 테스트용 첫 실제 회사를 사용한다.
                var controller = FindFirstObjectByType<EventSystemController>();
                eventCompany = controller?.getTodayEventInfos().FirstOrDefault(i => i.definition == definition)?.companyId
                    ?? FindFirstObjectByType<EventTempWorldData>()?.getCompanies().FirstOrDefault()?.companyId
                    ?? registry.Profiles.FirstOrDefault(p => p.position == NpcPosition.CEO)?.companyId ?? "";
            }
            EventCompanyData company = null;
            if (!string.IsNullOrEmpty(eventCompany))
            {
                registry.TryFindByPosition(eventCompany, NpcPosition.CEO, out var ceo);
                var contact = SelectedProfile?.companyId == eventCompany ? SelectedProfile : ceo;
                company = new EventCompanyData
                {
                    companyId = eventCompany,
                    companyName = ceo?.profile?.affiliation ?? contact?.profile?.affiliation ?? eventCompany,
                    ceoNpcId = ceo?.npcId, ceoName = ceo?.profile?.displayName,
                    restructuringNpcId = contact?.npcId, restructuringName = contact?.profile?.displayName,
                    restructuringRole = contact?.profile?.role
                };
            }
            return new TodayEventInfo(definition, company, definition.getValidDays());
        }

        /// <summary>등장 조건을 우회하는 NPC 결과 테스트다. 실제 SO의 선택을 공용 수신 경계에 전달한다.</summary>
        public bool PreviewActualEvent(EventDefinition definition, int choiceIndex)
        {
            if (definition == null || !GetRelatedEvents().Contains(definition) || choiceIndex < 0 || choiceIndex >= definition.getChoices().Count)
            { ActualEventPreviewReport = "현재 NPC에 해당하는 이벤트와 선택지를 지정하세요."; return false; }
            var choice = definition.getChoices()[choiceIndex];
            var preview = GetPreviewEventInfo(definition);
            var eventCompany = preview.companyId;
            var info = new ChoiceConfirmedInfo(definition.getId(), choiceIndex, choice.getResultValue(),
                CurrentTurn, choice.getNpcResult(), choice.getCashDelta(), eventCompany);
            // 실제 컨트롤러와 마찬가지로 회사별 사건은 해당 회사의 NPC만 남긴다.
            if (definition.hasCompanyConditions() && info.npcResult != null)
            {
                info.npcResult.targetNpcIds.RemoveAll(id => registry.TryGetProfile(id, out var seed)
                    && seed.companyId != eventCompany && seed.companyId != "external-network");
                info.npcResult.targetReactions.RemoveAll(r => !info.npcResult.targetNpcIds.Contains(r.targetNpcId));
                // 회사 필터 후 대상이 없으면 실제 컨트롤러처럼 NPC 효과 없는 선택으로 처리한다.
                if (info.npcResult.targetNpcIds.Count == 0)
                    info = new ChoiceConfirmedInfo(definition.getId(), choiceIndex, choice.getResultValue(),
                        CurrentTurn, null, choice.getCashDelta(), eventCompany);
            }
            var before = registry.CreateSnapshot().ToDictionary(s => s.npcId);
            var occurrence = "npc-event-preview-" + Guid.NewGuid().ToString("N");
            var success = reactionReceiver != null && reactionReceiver.TryPublishEventResult(info, occurrence);
            var report = new StringBuilder();
            report.AppendLine($"{preview.title} / {preview.resolve(choice.getText())}");
            report.AppendLine(success ? "NPC 선택 결과 처리 완료" : "처리 실패: " + reactionReceiver?.LastError);
            report.AppendLine("결과 대사: " + preview.resolve(choice.getResultDialogue()));
            var count = 0;
            foreach (var state in registry.States)
            {
                var memories = state.memories.Where(m => m.sourceEventId == occurrence).ToArray();
                if (memories.Length == 0) continue;
                count += memories.Length;
                registry.TryGetProfile(state.npcId, out var seed);
                var delta = NpcRelationDelta.Between(state.relation, before[state.npcId].relation);
                report.AppendLine($"{seed?.profile?.displayName}: 신뢰 {delta.trust:+0.##;-0.##;0}, 존중 {delta.respect:+0.##;-0.##;0}, 두려움 {delta.fear:+0.##;-0.##;0}, 적대감 {delta.hostility:+0.##;-0.##;0}, 의존도 {delta.dependency:+0.##;-0.##;0}, 관심 {delta.interest:+0.##;-0.##;0}");
                foreach (var memory in memories) report.AppendLine("생성 Memory: " + memory.memoryType + " / " + memory.memoryId);
            }
            report.AppendLine(count == 0 ? "새 Memory 없음 (효과 없는 선택 또는 수신 실패 여부를 확인하세요)." : $"생성 Memory 총 {count}개");
            report.AppendLine("등장 조건·AP·현금·지분·이벤트 완료 상태는 이 테스트에서 변경하지 않습니다. 관계·기억 저장은 별도입니다.");
            ActualEventPreviewReport = report.ToString();
            return success;
        }

        /// <summary>현재 등장 중인 사건은 실제 이벤트 상세 화면에서도 열 수 있다.</summary>
        public bool OpenActualEvent(EventDefinition definition)
        {
            var controller = FindFirstObjectByType<EventSystemController>();
            var info = controller == null ? null : controller.getTodayEventInfos().FirstOrDefault(i => i.definition == definition
                && (!definition.hasCompanyConditions() || i.companyId == companyId));
            if (info == null)
            { ActualEventPreviewReport = "현재 이 회사에서 발생하지 않은 이벤트입니다. 등장 조건·기한·완료 여부를 확인하세요. 선택 결과 테스트는 별도로 가능합니다."; return false; }
            controller.openEvent(definition.getId());
            return true;
        }
    }
}
