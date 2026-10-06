using System;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>
    /// NPC 관계·기억 프로토타입의 동작을 확인하는 임시 테스트 화면이다.
    /// 실제 게임 UI를 만들 때는 표시/입력 부분만 참고하고, 규칙은 MemoryService를 통해 적용한다.
    /// </summary>
    public sealed class NpcFeatureTestScene : MonoBehaviour
    {
        // 테스트 상태를 제공하는 브리지. Inspector 참조가 비어 있으면 씬에서 찾아 연결한다.
        [SerializeField] private NpcFeatureTestHarness bridge;
        // 통합 플레이에서는 기본적으로 숨기고 NPC 프리팹 Inspector에서 필요한 경우 켠다.
        [SerializeField] private bool showTestUI;
        public bool ShowTestUI { get => showTestUI; set => showTestUI = value; }
        // 관계·기억 테스트 영역과 기억 목록의 스크롤 위치를 각각 유지한다.
        private Vector2 memoryScrollPosition;
        private Vector2 bodyScrollPosition;
        private Vector2 npcScrollPosition;
        private Vector2 profileScrollPosition;
        private string displayedProfileId;
        private string saveStatus = "";
        private Vector2 saveStatusScroll;
        private int bodyTab;
        private Vector2 preferenceScroll;
        private Vector2 reactionScroll;
        private NpcChoiceReactionDefinition selectedReaction;

        /// <summary>씬의 브리지를 찾아 테스트 화면에서 사용할 준비를 한다.</summary>
        private void Start()
        {
            if (bridge == null) bridge = FindFirstObjectByType<NpcFeatureTestHarness>();
        }

        /// <summary>인물 선택·프로필과 기존 관계·Memory 테스트 조작을 한 화면에 그린다.</summary>
        private void OnGUI()
        {
            if (!showTestUI || bridge == null || bridge.RuntimeState == null) return;

            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var offset = new Vector2((Screen.width - 1280f * scale) * 0.5f, (Screen.height - 720f * scale) * 0.5f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            var profileTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            var footerButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            var relation = bridge.RuntimeState.relation;
            var profile = bridge.SelectedProfile;
            GUI.BeginGroup(new Rect(40f, 20f, 1200f, 680f));
            GUI.Box(new Rect(0f, 0f, 1200f, 680f), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(20f, 8f, 1160f, 38f), "NPC 프로필 / 관계 / Memory 테스트", titleStyle);
            DrawNpcProfileSelector(profile);
            DrawProfileDescription(profile, profileTextStyle);
            GUI.Label(new Rect(20f, 202f, 1160f, 26f),
                $"현재 날짜: {bridge.CurrentTurn}일    기록된 기억: {bridge.RuntimeState.memories.Count}개", labelStyle);
            GUI.Label(new Rect(20f, 228f, 1160f, 25f),
                "관계 버튼은 직접 조작 · 사건 Memory에는 NPC별 임시 성향 보정이 더해짐 · 게임 규칙 확정 전 테스트용", profileTextStyle);
            GUI.Label(new Rect(20f, 255f, 1160f, 27f),
                $"신뢰 {relation.trust:0}   존중 {relation.respect:0}   두려움 {relation.fear:0}", labelStyle);
            GUI.Label(new Rect(20f, 282f, 1160f, 27f),
                $"적대감 {relation.hostility:0}   의존도 {relation.dependency:0}   관심 {relation.interest:0}", labelStyle);

            // 중앙의 관계/기억 조작 영역만 스크롤한다. 하단 동작은 별도 고정 영역에 둔다.
            bodyTab = GUI.Toolbar(new Rect(20f, 314f, 1160f, 30f), bodyTab,
                new[] { "관계·Memory 조작", "선호·불호", "이벤트 미리보기" }, footerButtonStyle);
            var bodyViewport = new Rect(10f, 350f, 1180f, 184f);
            if (bodyTab == 0)
            {
                bodyScrollPosition = GUI.BeginScrollView(
                    bodyViewport, bodyScrollPosition, new Rect(0f, 0f, 1160f, 620f), false, true);
                GUILayout.BeginArea(new Rect(0f, 0f, 1140f, 620f));
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(540f));
                GUILayout.Label("관계 스테이터스 테스트", titleStyle);
                DrawActionButton("신뢰 올리기", bridge.ApplyTrustTestDelta, buttonStyle);
                DrawActionButton("존중 올리기", bridge.ApplyRespectTestDelta, buttonStyle);
                DrawActionButton("두려움 올리기", bridge.ApplyFearTestDelta, buttonStyle);
                DrawActionButton("적대감 올리기", bridge.ApplyHostilityTestDelta, buttonStyle);
                DrawActionButton("의존도 올리기", bridge.ApplyDependencyTestDelta, buttonStyle);
                DrawActionButton("관심 올리기", bridge.ApplyInterestTestDelta, buttonStyle);
                GUILayout.EndVertical();

                GUILayout.Space(30f);
                GUILayout.BeginVertical(GUILayout.Width(570f));
                GUILayout.Label("임시 Memory 기록", titleStyle);
                DrawMemoryButton("선의 / 약속 이행", NpcMemoryType.GoodFaith, buttonStyle);
                DrawMemoryButton("계약 파기", NpcMemoryType.ContractBreach, buttonStyle);
                DrawMemoryButton("대규모 구조조정", NpcMemoryType.MassLayoff, buttonStyle);
                DrawMemoryButton("적대적 인수", NpcMemoryType.HostileTakeover, buttonStyle);
                DrawMemoryButton("회사 구원", NpcMemoryType.SavedCompany, buttonStyle);
                DrawMemoryButton("공개적 망신", NpcMemoryType.PublicHumiliation, buttonStyle);
                DrawMemoryButton("정보 유출", NpcMemoryType.LeakedInfo, buttonStyle);
                DrawMemoryButton("뇌물", NpcMemoryType.Bribery, buttonStyle);
                GUILayout.Space(10f);
                GUILayout.Label("현재 Memory 목록", titleStyle);
                memoryScrollPosition = GUILayout.BeginScrollView(memoryScrollPosition, GUI.skin.box, GUILayout.Height(150f));
                if (bridge.RuntimeState.memories.Count == 0)
                {
                    GUILayout.Label("기록된 Memory가 없습니다.", labelStyle);
                }
                else
                {
                    foreach (var memory in bridge.RuntimeState.memories)
                    {
                        var duration = memory.permanent
                            ? "영구 영향력 100%"
                            : $"협상 영향력 {memory.influence * 100f:0}% · 잔여 {memory.remainingTurns}턴";
                        GUILayout.Label($"{memory.memoryId} | {GetMemoryLabel(memory.memoryType)} | 행동 성향 {GetActionDispositionLabel(memory.actionDisposition)} | 기록 턴 {memory.turn} | {duration}", labelStyle);
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                GUI.EndScrollView();
            }
            else
            {
                GUILayout.BeginArea(bodyViewport);
                if (bodyTab == 1) DrawPreferences(profile, profileTextStyle);
                else DrawReactionPreview(profileTextStyle, footerButtonStyle);
                GUILayout.EndArea();
            }

            if (bridge.LastRecordedMemory != null)
            {
                GUI.Label(new Rect(20f, 538f, 1160f, 26f),
                    $"마지막 기록: {bridge.LastRecordedMemory.memoryId} / {bridge.LastRecordedMemory.memoryType} · 공개 범위: {bridge.LastRecordedMemory.publicity} · {bridge.LastRecordedMemory.turn}일",
                    profileTextStyle);
            }
            // 마지막 기록 아래의 별도 영역에서 긴 저장 오류도 스크롤하며 확인한다.
            saveStatusScroll = GUI.BeginScrollView(new Rect(20f, 568f, 1160f, 40f), saveStatusScroll,
                new Rect(0f, 0f, 1135f, Mathf.Max(36f, profileTextStyle.CalcHeight(new GUIContent(saveStatus), 1135f))));
            GUI.Label(new Rect(0f, 0f, 1135f, profileTextStyle.CalcHeight(new GUIContent(saveStatus), 1135f)), saveStatus, profileTextStyle);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(10f, 614f, 220f, 52f), "테스트 사건 발행", footerButtonStyle)) bridge.PublishTestEvent();
            if (GUI.Button(new Rect(236f, 614f, 135f, 52f), "턴 진행", footerButtonStyle)) bridge.AdvanceTurn();
            if (GUI.Button(new Rect(377f, 614f, 125f, 52f), "저장", footerButtonStyle)) SaveState();
            if (GUI.Button(new Rect(508f, 614f, 140f, 52f), "불러오기", footerButtonStyle)) LoadState();
            if (GUI.Button(new Rect(654f, 614f, 240f, 52f), "선택 NPC 초기화", footerButtonStyle)) bridge.ResetSelectedNpcState();
            if (GUI.Button(new Rect(900f, 614f, 290f, 52f), "전체 NPC 초기화", footerButtonStyle)) bridge.ResetAllNpcStates();
            GUI.EndGroup();
        }

        /// <summary>길이에 맞춘 프로필을 고정 영역 안에서만 스크롤해 아래 상태 표시를 가리지 않는다.</summary>
        private void DrawProfileDescription(NpcStateRegistry.NpcSeed profile, GUIStyle style)
        {
            if (profile == null || profile.profile == null) return;
            if (displayedProfileId != profile.npcId)
            {
                displayedProfileId = profile.npcId;
                profileScrollPosition = Vector2.zero;
                preferenceScroll = Vector2.zero;
            }
            const float textWidth = 1135f;
            const float gap = 6f;
            var personality = new GUIContent($"{profile.profile.affiliation} · 성향: {profile.profile.personalitySummary}");
            var role = new GUIContent($"게임 역할: {profile.profile.gameplayRoleSummary}");
            var personalityHeight = style.CalcHeight(personality, textWidth);
            var roleHeight = style.CalcHeight(role, textWidth);
            var contentHeight = personalityHeight + gap + roleHeight;
            // NPC 버튼은 126에서 끝나고 날짜 표시는 202에서 시작한다. 사이 영역만 사용한다.
            var viewport = new Rect(20f, 128f, 1160f, 70f);
            profileScrollPosition.y = Mathf.Clamp(profileScrollPosition.y, 0f, Mathf.Max(0f, contentHeight - viewport.height));
            profileScrollPosition.x = 0f;
            profileScrollPosition = GUI.BeginScrollView(viewport, profileScrollPosition,
                new Rect(0f, 0f, textWidth, contentHeight), false, true,
                GUIStyle.none, GUI.skin.verticalScrollbar);
            GUI.Label(new Rect(0f, 0f, textWidth, personalityHeight), personality, style);
            GUI.Label(new Rect(0f, personalityHeight + gap, textWidth, roleHeight), role, style);
            GUI.EndScrollView();
        }

        /// <summary>선택 NPC의 행동 ID와 선호 이유를 원본 데이터 그대로 읽어 표시한다.</summary>
        private void DrawPreferences(NpcStateRegistry.NpcSeed profile, GUIStyle style)
        {
            preferenceScroll = GUILayout.BeginScrollView(preferenceScroll);
            if (profile == null || profile.actionPreferences == null || profile.actionPreferences.Count == 0)
                GUILayout.Label("등록된 행동 선호가 없습니다.", style);
            else
                foreach (var preference in profile.actionPreferences)
                {
                    if (preference == null) continue;
                    GUILayout.Label($"[{GetActionDispositionLabel(preference.disposition)}] {preference.actionId}", style);
                    GUILayout.Label($"이유: {preference.rationale}", style);
                    GUILayout.Space(8f);
                }
            GUILayout.EndScrollView();
        }

        /// <summary>등록된 반응만 수동으로 발행한다. 대상은 선택 NPC가 아닌 반응 SO 설정을 따른다.</summary>
        private void DrawReactionPreview(GUIStyle style, GUIStyle buttonStyle)
        {
            reactionScroll = GUILayout.BeginScrollView(reactionScroll);
            var reactions = bridge.RegisteredReactions;
            var selectionRegistered = false;
            foreach (var reaction in reactions)
                if (reaction != null && reaction == selectedReaction) selectionRegistered = true;
            if (!selectionRegistered) selectedReaction = null;
            GUILayout.Label("반응 SO의 대상에게 적용되며 런타임 관계·기억이 변경됩니다. 저장은 저장 버튼으로 합니다.", style);
            if (reactions.Count == 0)
                GUILayout.Label("NpcLogic의 NpcEventResultReceiver > Reactions에 반응 SO를 등록한 뒤 Play를 시작하세요.", style);
            foreach (var reaction in reactions)
            {
                if (reaction == null) continue;
                if (GUILayout.Button($"{(selectedReaction == reaction ? "● " : "")}{reaction.name} | {reaction.SourceEventId} | 선택 {reaction.ChoiceIndex} | 결과 {reaction.ResultValue}", buttonStyle))
                    selectedReaction = reaction;
            }
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && selectedReaction != null;
            if (GUILayout.Button("선택 반응 수동 발행", buttonStyle, GUILayout.Height(36f)))
                bridge.PreviewReaction(selectedReaction);
            GUI.enabled = previousEnabled;
            GUILayout.Label(bridge.ReactionPreviewReport, style);
            GUILayout.EndScrollView();
        }

        /// <summary>등록된 전체 NPC를 일정 너비의 버튼으로 표시하고 가로 스크롤로 선택한다.</summary>
        private void DrawNpcProfileSelector(NpcStateRegistry.NpcSeed selectedProfile)
        {
            var profiles = bridge.NpcProfiles;
            if (profiles == null || profiles.Count == 0) return;

            const float viewportWidth = 1160f;
            const float tabWidth = 232f;
            const float tabHeight = 54f;
            var viewport = new Rect(20f, 54f, viewportWidth, 72f);
            var contentWidth = Mathf.Max(viewportWidth, profiles.Count * tabWidth);
            var maxScroll = contentWidth - viewportWidth;
            npcScrollPosition.x = Mathf.Clamp(npcScrollPosition.x, 0f, maxScroll);
            npcScrollPosition.y = 0f;
            // NPC 영역 위의 휠은 좌우 이동으로 사용한다. 본문 스크롤과는 별개다.
            var input = Event.current;
            if (maxScroll > 0f && input.type == EventType.ScrollWheel && viewport.Contains(input.mousePosition))
            {
                var delta = Mathf.Abs(input.delta.x) > 0f ? input.delta.x : input.delta.y;
                npcScrollPosition.x = Mathf.Clamp(npcScrollPosition.x + delta * 40f, 0f, maxScroll);
                input.Use();
            }
            npcScrollPosition = GUI.BeginScrollView(viewport, npcScrollPosition,
                new Rect(0f, 0f, contentWidth, tabHeight), true, false,
                GUI.skin.horizontalScrollbar, GUIStyle.none);
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                if (profile == null || profile.profile == null) continue;
                var isSelected = selectedProfile != null && selectedProfile.npcId == profile.npcId;
                var tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap = true
                };
                var label = $"{profile.profile.displayName}\n{profile.profile.role}";
                if (GUI.Button(new Rect(index * tabWidth, 0f, tabWidth - 6f, tabHeight), label, tabStyle))
                {
                    if (!bridge.SelectNpcProfile(profile.npcId))
                        Debug.LogWarning($"NPC 프로필 '{profile.npcId}'을 선택하지 못했습니다.", this);
                }
            }
            GUI.EndScrollView();
        }

        private void SaveState()
        {
            saveStatus = bridge.TrySaveNpcState() ? "NPC 상태 저장 완료" : bridge.SaveError;
        }

        private void LoadState()
        {
            saveStatus = bridge.LoadNpcState() ? "저장 상태 불러오기 완료" : bridge.SaveError;
        }

        /// <summary>관계 변화 테스트 버튼을 만들고 클릭 시 브리지의 해당 함수를 호출한다.</summary>
        private void DrawActionButton(string label, Action action, GUIStyle style)
        {
            if (GUILayout.Button(label, style, GUILayout.Height(42f))) action();
        }

        /// <summary>지정한 유형의 테스트 기억을 기록하는 버튼을 만든다.</summary>
        private void DrawMemoryButton(string label, NpcMemoryType memoryType, GUIStyle style)
        {
            if (GUILayout.Button(label, style, GUILayout.Height(36f))) bridge.TriggerTestMemory(memoryType);
        }

        /// <summary>테스트 화면에서 enum 이름 대신 팀원이 읽기 쉬운 기억 이름을 보여준다.</summary>
        private static string GetActionDispositionLabel(NpcActionDisposition disposition)
        {
            switch (disposition)
            {
                case NpcActionDisposition.Likes: return "선호";
                case NpcActionDisposition.Dislikes: return "불호";
                default: return "미지정";
            }
        }

        private static string GetMemoryLabel(NpcMemoryType memoryType)
        {
            switch (memoryType)
            {
                case NpcMemoryType.GoodFaith: return "선의 / 약속 이행";
                case NpcMemoryType.ContractBreach: return "계약 파기";
                case NpcMemoryType.MassLayoff: return "대규모 구조조정";
                case NpcMemoryType.HostileTakeover: return "적대적 인수";
                case NpcMemoryType.SavedCompany: return "회사 구원";
                case NpcMemoryType.PublicHumiliation: return "공개적 망신";
                case NpcMemoryType.LeakedInfo: return "정보 유출";
                case NpcMemoryType.Bribery: return "뇌물";
                default: return "기타";
            }
        }

    }
}
