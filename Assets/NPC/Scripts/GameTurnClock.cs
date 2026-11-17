using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>마감 문서 등 하루 진행 전 필수 처리를 연결하는 계약</summary>
    public interface ITurnAdvanceGuard
    {
        bool CanAdvanceTurn(int currentTurn);
        void OnTurnAdvanceBlocked();
    }
    /// <summary>공통 게임 턴 번호를 보유하고 종료 시 NPC 기억 서비스에 한 번만 턴 경과를 알린다.</summary>
    public sealed class GameTurnClock : MonoBehaviour
    {
        [SerializeField, Min(1)] private int currentTurn = 1;
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private NpcMemoryService memoryService;
        private readonly List<ITurnBlocker> blockers = new List<ITurnBlocker>();

        /// <summary>현재 게임 턴. 초기값은 1이며 RestoreTurn도 최소 1로 제한한다.</summary>
        public int CurrentTurn => currentTurn;
        public event Action<int> TurnAdvanced;
        /// <summary>모든 날짜 갱신과 경제 계산이 끝난 뒤 날짜 시작 조건을 판정한다.</summary>
        public event Action<int> DayStarted;
        /// <summary>저장 복원 시 표시를 갱신한다. 날짜 진행이나 경제 효과를 실행하지 않는다.</summary>
        public event Action<int> TurnRestored;
        public event Action<string> AdvanceBlocked;

        public void RegisterBlocker(ITurnBlocker blocker)
        {
            if (!blockers.Contains(blocker)) blockers.Add(blocker);
        }

        public void UnregisterBlocker(ITurnBlocker blocker)
        {
            blockers.Remove(blocker);
        }

        /// <summary>공용 상태 저장소와 기억 경과 처리 서비스를 연결한다.</summary>
        public void Configure(NpcStateRegistry stateRegistry, NpcMemoryService service)
        {
            registry = stateRegistry;
            memoryService = service;
        }

        private void Awake()
        {
            if (registry == null) registry = GetComponent<NpcStateRegistry>();
            if (memoryService == null) memoryService = GetComponent<NpcMemoryService>();
            if (memoryService == null) memoryService = gameObject.AddComponent<NpcMemoryService>();
        }

        /// <summary>게임의 턴 종료 경로에서 호출한다. 등록된 각 NPC에 경과 처리를 적용한다.</summary>
        public void AdvanceTurn()
        {
            if (!TakeOver.Story.StoryDirector.CanAdvanceTurn) return;
            foreach (ITurnBlocker blocker in blockers)
            {
                if (blocker.IsBlocking(out string reason))
                {
                    AdvanceBlocked?.Invoke(reason);
                    return;
                }
            }
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component.isActiveAndEnabled && component is ITurnAdvanceGuard guard && !guard.CanAdvanceTurn(currentTurn))
                { guard.OnTurnAdvanceBlocked(); return; }
            currentTurn++;
            if (registry != null)
                foreach (var state in registry.States) memoryService.AdvanceTurn(state, currentTurn);
            TurnAdvanced?.Invoke(currentTurn);
            DayStarted?.Invoke(currentTurn);
        }

        /// <summary>저장 데이터에서 턴을 복원한다. 잘못된 0 이하 값은 1턴으로 보정한다.</summary>
        public void RestoreTurn(int turn)
        {
            currentTurn = Mathf.Max(1, turn);
            TurnRestored?.Invoke(currentTurn);
        }
    }
}
