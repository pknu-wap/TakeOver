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
        // 관계·기억 테스트 영역과 기억 목록의 스크롤 위치를 각각 유지한다.
        private Vector2 memoryScrollPosition;
        private Vector2 bodyScrollPosition;
        private string saveStatus = "";

        /// <summary>씬의 브리지를 찾아 테스트 화면에서 사용할 준비를 한다.</summary>
        private void Start()
        {
            if (bridge == null) bridge = FindFirstObjectByType<NpcFeatureTestHarness>();
        }

        /// <summary>인물 선택·프로필과 기존 관계·Memory 테스트 조작을 한 화면에 그린다.</summary>
        private void OnGUI()
        {
            if (bridge == null || bridge.RuntimeState == null) return;

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
            if (profile != null && profile.profile != null)
            {
                GUI.Label(new Rect(20f, 126f, 1160f, 34f),
                    $"{profile.profile.affiliation} · 성향: {profile.profile.personalitySummary}", profileTextStyle);
                GUI.Label(new Rect(20f, 160f, 1160f, 40f),
                    $"게임 역할: {profile.profile.gameplayRoleSummary}", profileTextStyle);
            }
            GUI.Label(new Rect(20f, 202f, 1160f, 26f),
                $"현재 날짜: {bridge.CurrentTurn}일    기록된 기억: {bridge.RuntimeState.memories.Count}개", labelStyle);
            GUI.Label(new Rect(20f, 228f, 1160f, 25f),
                "관계 버튼은 직접 조작 · 사건 Memory에는 NPC별 임시 성향 보정이 더해짐 · 게임 규칙 확정 전 테스트용", profileTextStyle);
            GUI.Label(new Rect(20f, 255f, 1160f, 27f),
                $"신뢰 {relation.trust:0}   존중 {relation.respect:0}   두려움 {relation.fear:0}", labelStyle);
            GUI.Label(new Rect(20f, 282f, 1160f, 27f),
                $"적대감 {relation.hostility:0}   의존도 {relation.dependency:0}   관심 {relation.interest:0}", labelStyle);

            // 중앙의 관계/기억 조작 영역만 스크롤한다. 하단 동작은 별도 고정 영역에 둔다.
            var bodyViewport = new Rect(10f, 314f, 1180f, 256f);
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

            if (bridge.LastRecordedMemory != null)
            {
                GUI.Label(new Rect(20f, 576f, 1160f, 28f),
                    $"마지막 기록: {bridge.LastRecordedMemory.memoryId} / {bridge.LastRecordedMemory.memoryType} · 공개 범위: {bridge.LastRecordedMemory.publicity} · {bridge.LastRecordedMemory.turn}일",
                    labelStyle);
            }
            GUI.Label(new Rect(400f, 580f, 780f, 25f), saveStatus, labelStyle);
            if (GUI.Button(new Rect(10f, 614f, 220f, 52f), "테스트 사건 발행", footerButtonStyle)) bridge.PublishTestEvent();
            if (GUI.Button(new Rect(236f, 614f, 135f, 52f), "턴 진행", footerButtonStyle)) bridge.AdvanceTurn();
            if (GUI.Button(new Rect(377f, 614f, 125f, 52f), "저장", footerButtonStyle)) SaveState();
            if (GUI.Button(new Rect(508f, 614f, 140f, 52f), "불러오기", footerButtonStyle)) LoadState();
            if (GUI.Button(new Rect(654f, 614f, 240f, 52f), "선택 NPC 초기화", footerButtonStyle)) bridge.ResetSelectedNpcState();
            if (GUI.Button(new Rect(900f, 614f, 290f, 52f), "전체 NPC 초기화", footerButtonStyle)) bridge.ResetAllNpcStates();
            GUI.EndGroup();
        }

        /// <summary>프리팹에 저장한 다섯 프로필을 탭처럼 보여주고 선택 NPC를 브리지에 전달한다.</summary>
        private void DrawNpcProfileSelector(NpcStateRegistry.NpcSeed selectedProfile)
        {
            var profiles = bridge.NpcProfiles;
            if (profiles == null || profiles.Count == 0) return;

            const float startX = 20f;
            const float totalWidth = 1160f;
            const float top = 54f;
            const float tabHeight = 54f;
            var tabWidth = totalWidth / profiles.Count;
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
                if (GUI.Button(new Rect(startX + index * tabWidth, top, tabWidth - 6f, tabHeight), label, tabStyle))
                {
                    if (!bridge.SelectNpcProfile(profile.npcId))
                        Debug.LogWarning($"NPC 프로필 '{profile.npcId}'을 선택하지 못했습니다.", this);
                }
            }
        }

        private void SaveState()
        {
            bridge.SaveNpcState();
            saveStatus = "NPC 상태 저장 완료";
        }

        private void LoadState()
        {
            saveStatus = bridge.LoadNpcState() ? "저장 상태 불러오기 완료" : "저장 파일이 없습니다";
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
