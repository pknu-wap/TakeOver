using System;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>
    /// NPC 기억 기록과 관계 변화 규칙을 한곳에서 처리한다.
    /// 실제 이벤트와 UI가 직접 상태를 수정하지 않고 이 서비스를 거치도록 확장한다.
    /// </summary>
    public sealed class NpcMemoryService : MonoBehaviour
    {
        [SerializeField] private NpcStateRegistry registry;
        /// <summary>새 기억이 실제로 추가됐을 때 발생한다. 중복 ID면 발생하지 않는다.</summary>
        public event Action<NpcRuntimeState, NpcMemoryRecord> memoryRecorded;
        /// <summary>관계 또는 기억 상태를 화면에 다시 표시해야 할 때 발생한다.</summary>
        public event Action<NpcRuntimeState> relationChanged;

        public void ConfigureRegistry(NpcStateRegistry stateRegistry) => registry = stateRegistry;

        /// <summary>
        /// 기억 입력을 검증·복사·정규화해 목록에 추가하고, 새 기록이면 관계 변화량을 한 번 적용한다.
        /// 반환된 기록은 UI 확인, 이벤트 로그, 이후 협상 판단의 입력으로 쓸 수 있다.
        /// </summary>
        public NpcMemoryRecord RecordMemory(NpcRuntimeState state, NpcMemoryRecord memory)
        {
            TryRecordMemory(state, memory, out var stored);
            return stored;
        }

        /// <summary>
        /// 새 기록 여부와 정규화된 결과를 함께 반환한다. 중복이면 기존 기록을 돌려주고 관계 변화와 알림은 반복하지 않는다.
        /// </summary>
        public bool TryRecordMemory(NpcRuntimeState state, NpcMemoryRecord memory, out NpcMemoryRecord stored)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            stored = memory.Clone();
            // 저장 범위를 정리해 Inspector/이벤트 입력값이 예상 범위를 벗어나지 않게 한다.
            stored.strength = Mathf.Clamp(stored.strength, 1, 5);
            stored.reliability = Mathf.Clamp01(stored.reliability);
            stored.durationTurns = Mathf.Clamp(stored.durationTurns, 1, 20);
            stored.remainingTurns = stored.permanent ? 0 : stored.durationTurns;
            // 두 종류 모두 최초 영향력은 100%다. 영구 여부는 이후 턴 감소를 적용할지 결정한다.
            stored.influence = 1f;
            if (string.IsNullOrWhiteSpace(stored.memoryId)) stored.memoryId = Guid.NewGuid().ToString("N");

            var existing = state.memories.Find(item => item != null && item.memoryId == stored.memoryId);
            // 목록에 살아 있는 기억끼리만 중복을 막는다. 만료되어 삭제된 뒤에는 같은 ID도 다시 기록할 수 있다.
            if (existing != null)
            {
                stored = existing;
                return false;
            }

            state.memories.Add(stored);
            state.relation.Apply(stored.relationDelta, stored.turn);
            memoryRecorded?.Invoke(state, stored);
            relationChanged?.Invoke(state);
            registry?.NotifyStateChanged(state);
            return true;
        }

        /// <summary>기억을 만들지 않는 단독 관계 효과를 적용한다(테스트 버튼 등에서 사용).</summary>
        public void ApplyRelationDelta(NpcRuntimeState state, NpcRelationDelta delta, int turn)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.relation.Apply(delta, turn);
            relationChanged?.Invoke(state);
            registry?.NotifyStateChanged(state);
        }

        /// <summary>
        /// 턴이 진행될 때 비영구 기억의 남은 턴을 줄인다.
        /// 잔여 턴이 0이 되는 즉시 목록에서 제거하며, 그 기억이 과거에 적용한 관계 변화는 되돌리지 않는다.
        /// </summary>
        public void AdvanceTurn(NpcRuntimeState state, int turn)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            for (var index = state.memories.Count - 1; index >= 0; index--)
            {
                var memory = state.memories[index];
                if (memory == null || memory.permanent) continue;

                memory.remainingTurns = Mathf.Max(0, memory.remainingTurns - 1);
                if (memory.remainingTurns == 0)
                {
                    // 뒤에서 앞으로 순회하므로 현재 항목을 지워도 아직 처리하지 않은 인덱스가 유지된다.
                    // 이후 같은 사건 ID가 새 안건으로 들어오면 RecordMemory가 새 기억으로 받아들인다.
                    state.memories.RemoveAt(index);
                    continue;
                }

                memory.influence = memory.durationTurns <= 0
                    ? 0f
                    : Mathf.Clamp01((float)memory.remainingTurns / memory.durationTurns);
            }

            relationChanged?.Invoke(state);
            registry?.NotifyStateChanged(state);
        }

        /// <summary>다른 시스템이 기억 영향력을 조회할 때 영구/일반 기억 규칙을 적용한다.</summary>
        public float GetMemoryInfluence(NpcMemoryRecord memory)
        {
            if (memory == null) return 0f;
            return memory.permanent ? 1f : Mathf.Clamp01(memory.influence);
        }

        /// <summary>테스트 상태를 초기화한다. 영구 저장 데이터는 이 프로토타입에 없다.</summary>
        public void ResetState(NpcRuntimeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.relation = default;
            state.memories.Clear();
            state.currentGoal = string.Empty;
            relationChanged?.Invoke(state);
            registry?.NotifyStateChanged(state);
        }
    }
}
