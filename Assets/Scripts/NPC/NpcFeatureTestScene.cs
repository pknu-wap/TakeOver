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
        private enum ConnectionScreen
        {
            Main,
            EventPlaceholder,
            PortfolioPlaceholder
        }

        // 테스트 상태를 제공하는 브리지. Inspector 참조가 비어 있으면 씬에서 찾아 연결한다.
        [SerializeField] private NpcFeatureTestHarness bridge;
        // 중앙 테스트 영역과 기억 목록의 스크롤 위치를 각각 유지한다.
        private Vector2 memoryScrollPosition;
        private Vector2 bodyScrollPosition;
        private string saveStatus = "";
        private ConnectionScreen connectionScreen;

        /// <summary>씬의 브리지를 찾아 테스트 화면에서 사용할 준비를 한다.</summary>
        private void Start()
        {
            if (bridge == null) bridge = FindFirstObjectByType<NpcFeatureTestHarness>();
        }

        /// <summary>
        /// 상단 요약, 스크롤 가능한 두 열 테스트 영역, 하단 고정 버튼을 그린다.
        /// 하단 버튼을 고정 좌표로 두어 기억 목록이 길어져도 주요 입력을 계속 사용할 수 있다.
        /// </summary>
        private void OnGUI()
        {
            if (bridge == null || bridge.RuntimeState == null) return;

            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var offset = new Vector2((Screen.width - 1280f * scale) * 0.5f, (Screen.height - 720f * scale) * 0.5f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            if (connectionScreen != ConnectionScreen.Main)
            {
                DrawConnectionPlaceholder(titleStyle, labelStyle, buttonStyle);
                return;
            }
            var relation = bridge.RuntimeState.relation;
            GUI.BeginGroup(new Rect(40f, 20f, 1200f, 680f));
            GUI.Box(new Rect(0f, 0f, 1200f, 680f), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(20f, 10f, 1160f, 40f), "NPC / 포트폴리오 연결 테스트", titleStyle);
            GUI.Label(new Rect(20f, 52f, 1160f, 30f), $"NPC: {bridge.RuntimeState.npcId}    회사: {bridge.RuntimeState.companyId}", labelStyle);
            GUI.Label(new Rect(20f, 82f, 1160f, 30f), $"현재 날짜: {bridge.CurrentTurn}일    기록된 기억: {bridge.RuntimeState.memories.Count}개", labelStyle);
            GUI.Label(new Rect(20f, 112f, 1160f, 30f), "관계 변화는 기록 시 1회 적용 · Memory 영향력은 감소값만 제공(행동/협상 소비부 미연결)", labelStyle);
            GUI.Label(new Rect(20f, 142f, 1160f, 30f), $"신뢰 {relation.trust:0}   존중 {relation.respect:0}   두려움 {relation.fear:0}", labelStyle);
            GUI.Label(new Rect(20f, 172f, 1160f, 30f), $"적대감 {relation.hostility:0}   의존도 {relation.dependency:0}   관심 {relation.interest:0}", labelStyle);

            // 중앙의 관계/기억 조작 영역만 스크롤한다. 하단 동작은 별도 고정 영역에 둔다.
            var bodyViewport = new Rect(10f, 210f, 1180f, 360f);
            bodyScrollPosition = GUI.BeginScrollView(
                bodyViewport, bodyScrollPosition, new Rect(0f, 0f, 1160f, 620f), false, true);
            GUILayout.BeginArea(new Rect(0f, 0f, 1140f, 620f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(540f));
            GUILayout.Label("다른 파트 화면 연결", titleStyle);
            DrawActionButton("이벤트 파트 화면 열기", OpenEventScreen, buttonStyle);
            DrawActionButton("포트폴리오/지분 화면 열기", OpenPortfolioScreen, buttonStyle);
            GUILayout.Space(8f);
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
                    GUILayout.Label($"{memory.memoryId} | {GetMemoryLabel(memory.memoryType)} | 기록 턴 {memory.turn} | {duration}", labelStyle);
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
            if (GUI.Button(new Rect(10f, 614f, 300f, 52f), "테스트 사건 발행", buttonStyle)) bridge.PublishTestEvent();
            if (GUI.Button(new Rect(315f, 614f, 205f, 52f), "턴 진행", buttonStyle)) bridge.AdvanceTurn();
            if (GUI.Button(new Rect(525f, 614f, 205f, 52f), "저장", buttonStyle)) SaveState();
            if (GUI.Button(new Rect(735f, 614f, 205f, 52f), "불러오기", buttonStyle)) LoadState();
            if (GUI.Button(new Rect(945f, 614f, 245f, 52f), "초기화", buttonStyle)) bridge.ResetTestState();
            GUI.EndGroup();
        }

        /// <summary>대응 화면이 아직 연결되지 않은 상태도 버튼 동작으로 확인할 수 있게 안내 화면을 띄운다.</summary>
        private void DrawConnectionPlaceholder(GUIStyle titleStyle, GUIStyle labelStyle, GUIStyle buttonStyle)
        {
            var isEventScreen = connectionScreen == ConnectionScreen.EventPlaceholder;
            var title = isEventScreen ? "이벤트 파트 연결" : "포트폴리오/지분 파트 연결";
            var description = isEventScreen
                ? "이벤트 파트 화면이 준비되면 Inspector의 이벤트 화면 UnityEvent에 연결해 주세요.\n선택 결과는 NPC 이벤트 채널을 통해 관계와 Memory에 반영할 수 있습니다."
                : "포트폴리오/지분 파트 화면이 준비되면 Inspector의 포트폴리오 화면 UnityEvent에 연결해 주세요.\nNPC 파트 버튼 연결은 준비되어 있습니다.";

            GUI.BeginGroup(new Rect(40f, 20f, 1200f, 680f));
            GUI.Box(new Rect(0f, 0f, 1200f, 680f), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(40f, 32f, 1120f, 50f), title, titleStyle);
            GUI.Label(new Rect(40f, 120f, 1120f, 120f), description, labelStyle);
            if (GUI.Button(new Rect(400f, 570f, 400f, 60f), "NPC 테스트 화면으로 돌아가기", buttonStyle))
                connectionScreen = ConnectionScreen.Main;
            GUI.EndGroup();
        }

        /// <summary>이벤트 파트 진입 버튼을 누르면 연결된 화면을 열고, 미연결이면 안내 화면을 보여준다.</summary>
        private void OpenEventScreen()
        {
            connectionScreen = ConnectionScreen.EventPlaceholder;
            bridge.OpenEventScreen();
        }

        /// <summary>포트폴리오 파트 진입 버튼을 누르면 연결된 화면을 열고, 미연결이면 안내 화면을 보여준다.</summary>
        private void OpenPortfolioScreen()
        {
            connectionScreen = ConnectionScreen.PortfolioPlaceholder;
            bridge.OpenPortfolioScreen();
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
