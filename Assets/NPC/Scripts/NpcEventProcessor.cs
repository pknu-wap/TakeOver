using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TakeOver.NPC
{
    /// <summary>공용 사건 결과를 대상 NPC별 Memory와 관계 변화로 반영한다.</summary>
    public sealed class NpcEventProcessor : MonoBehaviour
    {
        [SerializeField] private NpcMemoryService memoryService;
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private GameTurnClock turnClock;
        [SerializeField] private NpcEventChannel eventChannel;
        [SerializeField] private UnityEvent onMemoryRecorded = new UnityEvent();

        public event Action<NpcMemoryRecord> MemoryRecorded;
        public int CurrentTurn => turnClock == null ? 1 : turnClock.CurrentTurn;

        private void Awake() => ResolveDependencies();

        private void OnEnable()
        {
            ResolveDependencies();
            if (eventChannel != null) eventChannel.Published += HandleEvent;
        }

        private void OnDisable()
        {
            if (eventChannel != null) eventChannel.Published -= HandleEvent;
        }

        /// <summary>같은 오브젝트 또는 Inspector에서 지정한 공용 서비스를 연결한다.</summary>
        private void ResolveDependencies()
        {
            if (registry == null) registry = GetComponent<NpcStateRegistry>();
            if (memoryService == null) memoryService = GetComponent<NpcMemoryService>();
            if (turnClock == null) turnClock = GetComponent<GameTurnClock>();
            if (eventChannel == null) eventChannel = GetComponent<NpcEventChannel>();
            if (memoryService != null && registry != null) memoryService.ConfigureRegistry(registry);
            if (turnClock != null && registry != null && memoryService != null) turnClock.Configure(registry, memoryService);
        }

        /// <summary>전달된 사건을 대상별 반응과 등록된 NPC 상태에 반영한다.</summary>
        private void HandleEvent(NpcEventPayload payload)
        {
            if (payload == null || payload.targetNpcIds == null || registry == null || memoryService == null) return;
            // 프로필 보정까지 전 대상에 대해 먼저 검사해 뒤쪽 대상의 잘못된 값으로 일부만 반영되지 않게 한다.
            foreach (var targetId in payload.targetNpcIds)
            {
                if (!registry.TryGet(targetId, out _)) continue;
                var reaction = FindReaction(payload, targetId);
                var type = reaction == null ? payload.memoryType : reaction.memoryType;
                var delta = reaction == null ? payload.relationDelta : reaction.relationDelta;
                NpcInputValidation.ValidateEvent(type,
                    reaction == null ? payload.publicity : reaction.publicity,
                    reaction == null ? payload.reliability : reaction.reliability,
                    registry.ApplyProfileEventBias(targetId, type, delta));
            }
            foreach (var targetId in payload.targetNpcIds)
            {
                if (!registry.TryGet(targetId, out var state))
                {
                    Debug.LogWarning($"이벤트 대상 NPC '{targetId}'가 레지스트리에 등록되지 않아 기억을 기록하지 않았습니다.", this);
                    continue;
                }
                var reaction = FindReaction(payload, targetId);
                var recordInput = new NpcMemoryRecord
                {
                    memoryId = $"{payload.eventId}:{targetId}", sourceEventId = payload.eventId,
                    actorId = payload.actorId, targetId = targetId, actionId = payload.actionId,
                    companyId = payload.companyId, turn = CurrentTurn
                };
                // 사건 공급자가 actionId를 보내면 해당 NPC 프로필의 선호 방향을 기록에 함께 남긴다.
                // 선호를 관계 수치나 기억 강도로 바꾸는 공식은 기획 결정 전까지 적용하지 않는다.
                if (registry.TryGetActionPreference(targetId, payload.actionId, out var disposition))
                    recordInput.actionDisposition = disposition;
                if (reaction == null)
                {
                    recordInput.memoryType = payload.memoryType;
                    recordInput.strength = payload.strength;
                    recordInput.publicity = payload.publicity;
                    recordInput.permanent = payload.permanent;
                    recordInput.durationTurns = payload.durationTurns;
                    recordInput.reliability = payload.reliability;
                    recordInput.interpretation = payload.interpretation;
                    recordInput.tags = payload.tags == null ? new List<string>() : new List<string>(payload.tags);
                    recordInput.relationDelta = payload.relationDelta;
                }
                else
                {
                    recordInput.memoryType = reaction.memoryType;
                    recordInput.strength = reaction.strength;
                    recordInput.publicity = reaction.publicity;
                    recordInput.permanent = reaction.permanent;
                    recordInput.durationTurns = reaction.durationTurns;
                    recordInput.reliability = reaction.reliability;
                    recordInput.interpretation = reaction.interpretation;
                    recordInput.tags = reaction.tags == null ? new List<string>() : new List<string>(reaction.tags);
                    recordInput.relationDelta = reaction.relationDelta;
                }

                // TryRecordMemory가 false면 동일 eventId의 기존 기록이므로 UI 알림도 다시 울리지 않는다.
                // 프로필별 임시 성향 반응을 사건 기본/개별 반응에 더한다.
                recordInput.relationDelta = registry.ApplyProfileEventBias(
                    targetId, recordInput.memoryType, recordInput.relationDelta);
                if (!memoryService.TryRecordMemory(state, recordInput, out var record)) continue;
                MemoryRecorded?.Invoke(record);
                onMemoryRecorded?.Invoke();
            }
        }

        /// <summary>대상별 반응이 있으면 우선 사용하고, 없으면 payload의 공통 기본값으로 돌아간다.</summary>
        private static NpcEventReaction FindReaction(NpcEventPayload payload, string targetNpcId)
        {
            if (payload.targetReactions == null) return null;
            foreach (var reaction in payload.targetReactions)
                if (reaction != null && string.Equals(reaction.targetNpcId, targetNpcId, StringComparison.Ordinal)) return reaction;
            return null;
        }
    }
}
