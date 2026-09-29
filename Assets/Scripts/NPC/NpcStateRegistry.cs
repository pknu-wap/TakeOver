using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>프로젝트의 여러 시스템이 NPC ID로 동일한 관계·기억 상태를 조회하는 저장소다.</summary>
    public sealed class NpcStateRegistry : MonoBehaviour
    {
        [Serializable]
        public sealed class NpcSeed
        {
            public string npcId = "npc-ceo-01";
            public string companyId = "portfolio-company-01";
            public List<NpcActionPreference> actionPreferences = new List<NpcActionPreference>();
        }

        [SerializeField] private List<NpcSeed> initialNpcs = new List<NpcSeed>();
        private readonly Dictionary<string, NpcRuntimeState> states = new Dictionary<string, NpcRuntimeState>();
        // 선호/불호는 정적 프로필 데이터이므로 세이브되는 NPC 런타임 상태와 분리한다.
        private readonly Dictionary<string, List<NpcActionPreference>> actionPreferencesByNpc = new Dictionary<string, List<NpcActionPreference>>();
        private readonly List<NpcRuntimeState> orderedStates = new List<NpcRuntimeState>();

        /// <summary>턴·저장 서비스가 모든 NPC 상태를 순서대로 읽을 때 사용하는 읽기 전용 뷰다.</summary>
        public IReadOnlyList<NpcRuntimeState> States => orderedStates;
        public event Action<NpcRuntimeState> StateChanged;

        private void Awake()
        {
            if (initialNpcs == null) return;
            foreach (var seed in initialNpcs)
            {
                if (seed == null) continue;
                var state = GetOrCreate(seed.npcId, seed.companyId);
                ConfigureActionPreferences(state.npcId, seed.actionPreferences);
            }
        }

        /// <summary>ID로 상태를 찾고, 최초 요청이면 명시한 소속 회사와 함께 만든다.</summary>
        public NpcRuntimeState GetOrCreate(string npcId, string companyId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) throw new ArgumentException("NPC ID가 필요합니다.", nameof(npcId));
            if (states.TryGetValue(npcId, out var existing))
            {
                return existing;
            }

            var created = new NpcRuntimeState(npcId, companyId);
            states.Add(npcId, created);
            orderedStates.Add(created);
            StateChanged?.Invoke(created);
            return created;
        }

        public bool TryGet(string npcId, out NpcRuntimeState state) => states.TryGetValue(npcId ?? string.Empty, out state);

        /// <summary>
        /// NPC별 선호/불호 행동 프로필을 복사해 등록한다. actionId가 겹치면 첫 항목만 유지한다.
        /// 선호는 현재 데이터 기반만 제공하며 관계 변화나 행동 판단 점수로 환산하지 않는다.
        /// </summary>
        public void ConfigureActionPreferences(string npcId, IList<NpcActionPreference> preferences)
        {
            if (!states.TryGetValue(npcId ?? string.Empty, out var state))
                throw new ArgumentException("선호 프로필을 등록하기 전에 NPC 상태를 생성해야 합니다.", nameof(npcId));

            var configured = new List<NpcActionPreference>();
            var seenActionIds = new HashSet<string>(StringComparer.Ordinal);
            if (preferences != null)
            {
                foreach (var preference in preferences)
                {
                    if (preference == null || string.IsNullOrWhiteSpace(preference.actionId)) continue;
                    if (!seenActionIds.Add(preference.actionId)) continue;
                    configured.Add(preference.Clone());
                }
            }
            actionPreferencesByNpc[npcId] = configured;
            NotifyStateChanged(state);
        }

        /// <summary>행동/협상 시스템이 NPC별 선호 방향을 조회할 때 사용한다.</summary>
        public bool TryGetActionPreference(string npcId, string actionId, out NpcActionDisposition disposition)
        {
            disposition = NpcActionDisposition.Unspecified;
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(actionId)) return false;
            if (!actionPreferencesByNpc.TryGetValue(npcId, out var preferences)) return false;
            foreach (var preference in preferences)
            {
                if (preference == null || !string.Equals(preference.actionId, actionId, StringComparison.Ordinal)) continue;
                disposition = preference.disposition;
                return disposition != NpcActionDisposition.Unspecified;
            }
            return false;
        }

        /// <summary>로드된 전체 NPC 상태를 복사해 교체하고 이후 변경 알림을 보낸다.</summary>
        public void ReplaceAll(IList<NpcRuntimeState> loadedStates)
        {
            var previous = new Dictionary<string, NpcRuntimeState>(states);
            states.Clear();
            orderedStates.Clear();
            if (loadedStates != null)
                foreach (var item in loadedStates)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.npcId) || states.ContainsKey(item.npcId)) continue;
                    var copy = previous.TryGetValue(item.npcId, out var existing) ? existing : item.Clone();
                    if (copy == existing) copy.CopyFrom(item);
                    states.Add(copy.npcId, copy);
                    orderedStates.Add(copy);
                }
            foreach (var item in orderedStates) StateChanged?.Invoke(item);
        }

        public List<NpcRuntimeState> CreateSnapshot()
        {
            var snapshot = new List<NpcRuntimeState>(orderedStates.Count);
            foreach (var state in orderedStates) snapshot.Add(state.Clone());
            return snapshot;
        }

        /// <summary>관계 또는 기억을 변경한 시스템이 다른 화면에 상태 갱신을 알릴 때 호출한다.</summary>
        public void NotifyStateChanged(NpcRuntimeState state)
        {
            if (state != null && states.TryGetValue(state.npcId, out var registered) && registered == state)
                StateChanged?.Invoke(state);
        }
    }
}
