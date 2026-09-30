using System;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>공통 게임 턴 번호를 보유하고 종료 시 NPC 기억 서비스에 한 번만 턴 경과를 알린다.</summary>
    public sealed class GameTurnClock : MonoBehaviour
    {
        [SerializeField, Min(1)] private int currentTurn = 1;
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private NpcMemoryService memoryService;

        /// <summary>현재 게임 턴. 초기값은 1이며 RestoreTurn도 최소 1로 제한한다.</summary>
        public int CurrentTurn => currentTurn;
        public event Action<int> TurnAdvanced;

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
            currentTurn++;
            if (registry != null)
                foreach (var state in registry.States) memoryService.AdvanceTurn(state, currentTurn);
            TurnAdvanced?.Invoke(currentTurn);
        }

        /// <summary>저장 데이터에서 턴을 복원한다. 잘못된 0 이하 값은 1턴으로 보정한다.</summary>
        public void RestoreTurn(int turn) => currentTurn = Mathf.Max(1, turn);
    }
}
