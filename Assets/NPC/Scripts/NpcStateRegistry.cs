using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>프로젝트의 여러 시스템이 NPC ID로 동일한 관계·기억 상태를 조회하는 저장소다.</summary>
    public sealed class NpcStateRegistry : MonoBehaviour
    {
        [Serializable]
        public sealed class NpcProfile
        {
            // 스탠딩은 원본 SO에서 참조한다. JsonUtility 사본에 Unity 객체 참조를 직렬화해 옮기지 않는다.
            public Sprite standingIllustration;
            public Sprite dimmedStandingIllustration;
            public string displayName;
            public string role;
            public string affiliation;
            [TextArea(2, 4)] public string personalitySummary;
            [TextArea(2, 4)] public string gameplayRoleSummary;
            // Reference에서 수치가 확정되지 않은 동안 모든 사건/사건 유형에 더할 임시 관계 반응이다.
            public NpcRelationDelta anyEventRelationDelta;
            public List<NpcEventRelationBias> eventRelationBiases = new List<NpcEventRelationBias>();
        }

        [Serializable]
        public sealed class NpcEventRelationBias
        {
            public NpcMemoryType memoryType;
            public NpcRelationDelta relationDelta;
        }

        [Serializable]
        public sealed class NpcSeed
        {
            public NpcPosition position;
            public NpcPreferenceResponse preferenceResponse = new NpcPreferenceResponse();
            public List<NpcMemoryPreference> memoryPreferences = new List<NpcMemoryPreference>();
            public string npcId = "";
            public string companyId = "";
            public NpcProfile profile = new NpcProfile();
            public List<NpcActionPreference> actionPreferences = new List<NpcActionPreference>();
        }

        [SerializeField] private List<NpcSeed> initialNpcs = new List<NpcSeed>();
        [Tooltip("공유 NPC 정의. 같은 ID의 기존 인라인 데이터보다 우선하며 런타임에는 사본만 사용합니다.")]
        [SerializeField] private List<NpcDefinition> npcDefinitions = new List<NpcDefinition>();
        private readonly Dictionary<string, NpcRuntimeState> states = new Dictionary<string, NpcRuntimeState>();
        // 선호/불호는 정적 프로필 데이터이므로 세이브되는 NPC 런타임 상태와 분리한다.
        private readonly NpcPreferenceEvaluator preferences = new NpcPreferenceEvaluator();
        private readonly List<NpcRuntimeState> orderedStates = new List<NpcRuntimeState>();
        private bool initialNpcsInitialized;
        private bool initialNpcsInitializing;
        // 초기화 알림은 기존 발생 순서를 유지하되 모든 상태·선호 등록이 끝난 뒤 전달한다.
        private readonly List<NpcRuntimeState> pendingInitialNotifications = new List<NpcRuntimeState>();

        /// <summary>턴·저장 서비스가 모든 NPC 상태를 순서대로 읽을 때 사용하는 읽기 전용 뷰다.</summary>
        public IReadOnlyList<NpcRuntimeState> States => orderedStates;
        /// <summary>프리팹에 정의된 NPC 정적 프로필을 선택 UI가 읽는 읽기 전용 목록이다.</summary>
        public IReadOnlyList<NpcSeed> Profiles
        {
            get
            {
                EnsureInitialNpcsInitialized();
                return initialNpcs;
            }
        }
        public event Action<NpcRuntimeState> StateChanged;

        /// <summary>회사 ID와 직책으로 이벤트 화자의 정적 프로필을 찾는다. 같은 조건이 여러 명이면 실패한다.</summary>
        public bool TryFindByPosition(string companyId, NpcPosition position, out NpcSeed result)
        {
            result = null;
            if (position == NpcPosition.None) return false;
            foreach (var seed in Profiles)
            {
                if (seed == null || seed.companyId != companyId || seed.position != position) continue;
                if (result != null) { result = null; return false; }
                result = seed;
            }
            return result != null;
        }

        /// <summary>SO의 보너스 기준에 따라 행동 또는 Memory 선호를 조회한다. 두 배율을 중복 적용하지 않는다.</summary>
        public NpcRelationDelta EvaluatePreference(string npcId, string actionId, NpcMemoryType memoryType,
            NpcRelationDelta delta, out NpcActionDisposition disposition)
        {
            var result = EvaluatePreferenceDetails(npcId, actionId, memoryType, delta);
            disposition = result.disposition;
            return result.relationDelta;
        }

        public NpcPreferenceEvaluation EvaluatePreferenceDetails(string npcId, string actionId,
            NpcMemoryType memoryType, NpcRelationDelta delta)
            => TryGetProfile(npcId, out var seed) ? preferences.Evaluate(seed, npcId, actionId, memoryType, delta)
                : new NpcPreferenceEvaluation { relationDelta = delta, multiplier = 1f };

        private void Awake() => EnsureInitialNpcsInitialized();

        /// <summary>
        /// 프리팹에 초기 프로필이 없으면 테스트 화면에서도 같은 기본 명단을 사용할 수 있게 채운다.
        /// 정적 프로필과 세이브 대상인 관계·기억 상태는 별도로 보관한다.
        /// </summary>
        public void EnsureInitialNpcsInitialized()
        {
            if (initialNpcsInitialized || initialNpcsInitializing) return;
            initialNpcsInitializing = true;
            try
            {
                if (initialNpcs == null) initialNpcs = new List<NpcSeed>();
                // 이전 씬의 인라인 데이터는 호환용으로 유지하고, SO는 독립 사본으로 병합한다.
                var seeds = new List<NpcSeed>(initialNpcs);
                var definitionIds = new HashSet<string>(StringComparer.Ordinal);
                if (npcDefinitions != null)
                    foreach (var definition in npcDefinitions)
                    {
                        if (definition == null) continue;
                        var seed = definition.CreateRuntimeSeed();
                        if (seed == null || string.IsNullOrWhiteSpace(seed.npcId) || !definitionIds.Add(seed.npcId))
                            throw new InvalidOperationException("NPC 정의의 ID가 비어 있거나 중복됐습니다.");
                        seeds.RemoveAll(item => item != null && item.npcId == seed.npcId);
                        seeds.Add(seed);
                    }
                initialNpcs = seeds;
                if (initialNpcs.Count == 0) initialNpcs.AddRange(CreateDefaultNpcRoster());

                foreach (var seed in initialNpcs)
                {
                    if (seed == null) continue;
                    var state = GetOrCreate(seed.npcId, seed.companyId);
                    ConfigureActionPreferences(state.npcId, seed.actionPreferences);
                }
                initialNpcsInitialized = true;
            }
            finally
            {
                initialNpcsInitializing = false;
                if (!initialNpcsInitialized) pendingInitialNotifications.Clear();
            }
            // 알림 콜백에서 Profiles를 조회해도 초기화가 재진입하지 않는다.
            var notifications = pendingInitialNotifications.ToArray();
            pendingInitialNotifications.Clear();
            foreach (var state in notifications) NotifyStateChanged(state);
        }

        /// <summary>지정한 ID의 정적 인물 프로필을 찾는다.</summary>
        public bool TryGetProfile(string npcId, out NpcSeed profile)
        {
            EnsureInitialNpcsInitialized();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                foreach (var seed in initialNpcs)
                {
                    if (seed == null || !string.Equals(seed.npcId, npcId, StringComparison.Ordinal)) continue;
                    profile = seed;
                    return true;
                }
            }
            profile = null;
            return false;
        }

        /// <summary>기존 테스트 씬에서 사용하는 기본 5인 명단을 독립 사본으로 만든다.</summary>
        public static List<NpcSeed> CreateDefaultNpcRoster() => NpcDefaultRoster.Create();

        public NpcRelationDelta ApplyProfileEventBias(string npcId, NpcMemoryType memoryType, NpcRelationDelta relationDelta)
        {
            return TryGetProfile(npcId, out var seed)
                ? NpcPreferenceEvaluator.ApplyProfileBias(seed, memoryType, relationDelta)
                : relationDelta;
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
            NotifyStateChanged(created);
            return created;
        }

        /// <summary>ID로 등록 상태를 조회한다. 찾지 못하면 false와 null 상태를 반환한다.</summary>
        public bool TryGet(string npcId, out NpcRuntimeState state) => states.TryGetValue(npcId ?? string.Empty, out state);

        /// <summary>
        /// NPC별 선호/불호 행동 프로필을 복사해 등록한다. actionId가 겹치면 첫 항목만 유지한다.
        /// 등록된 방향은 EvaluatePreference에서 인물별 활성 설정에 따라 관계 배율에 사용한다.
        /// </summary>
        public void ConfigureActionPreferences(string npcId, IList<NpcActionPreference> preferences)
        {
            if (!states.TryGetValue(npcId ?? string.Empty, out var state))
                throw new ArgumentException("선호 프로필을 등록하기 전에 NPC 상태를 생성해야 합니다.", nameof(npcId));

            this.preferences.Configure(npcId, preferences);
            NotifyStateChanged(state);
        }

        /// <summary>행동/협상 시스템이 NPC별 선호 방향을 조회할 때 사용한다.</summary>
        public bool TryGetActionPreference(string npcId, string actionId, out NpcActionDisposition disposition)
            => preferences.TryGet(npcId, actionId, out disposition);

        /// <summary>로드된 전체 NPC 상태를 복사해 교체하고 이후 변경 알림을 보낸다.</summary>
        public void ReplaceAll(IList<NpcRuntimeState> loadedStates, bool notify = true)
        {
            EnsureInitialNpcsInitialized();
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
            // 이전 저장에 없는 현재 프로필도 이벤트 대상이 될 수 있도록 초기 상태로 보충한다.
            // 로드 결과에 없는 과거 런타임 값은 이어받지 않는다.
            foreach (var seed in initialNpcs)
            {
                if (seed == null || string.IsNullOrWhiteSpace(seed.npcId) || states.ContainsKey(seed.npcId)) continue;
                var fresh = new NpcRuntimeState(seed.npcId, seed.companyId);
                if (previous.TryGetValue(seed.npcId, out var existing))
                {
                    existing.CopyFrom(fresh);
                    fresh = existing;
                }
                states.Add(fresh.npcId, fresh);
                orderedStates.Add(fresh);
            }
            if (notify) NotifyAllStatesChanged();
        }

        public void NotifyAllStatesChanged()
        {
            foreach (var item in orderedStates) StateChanged?.Invoke(item);
        }

        /// <summary>저장용 독립 사본을 만든다. 반환 목록과 Memory 항목은 런타임 상태 객체와 분리된다.</summary>
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
            {
                if (initialNpcsInitializing) pendingInitialNotifications.Add(state);
                else StateChanged?.Invoke(state);
            }
        }
    }
}
