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
            public string npcId = "npc-ceo-01";
            public string companyId = "portfolio-company-01";
            public NpcProfile profile = new NpcProfile();
            public List<NpcActionPreference> actionPreferences = new List<NpcActionPreference>();
        }

        [SerializeField] private List<NpcSeed> initialNpcs = new List<NpcSeed>();
        private readonly Dictionary<string, NpcRuntimeState> states = new Dictionary<string, NpcRuntimeState>();
        // 선호/불호는 정적 프로필 데이터이므로 세이브되는 NPC 런타임 상태와 분리한다.
        private readonly Dictionary<string, List<NpcActionPreference>> actionPreferencesByNpc = new Dictionary<string, List<NpcActionPreference>>();
        private readonly List<NpcRuntimeState> orderedStates = new List<NpcRuntimeState>();
        private bool initialNpcsInitialized;

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

        private void Awake() => EnsureInitialNpcsInitialized();

        /// <summary>
        /// 프리팹에 초기 프로필이 없으면 테스트 화면에서도 같은 기본 명단을 사용할 수 있게 채운다.
        /// 정적 프로필과 세이브 대상인 관계·기억 상태는 별도로 보관한다.
        /// </summary>
        public void EnsureInitialNpcsInitialized()
        {
            if (initialNpcsInitialized) return;
            if (initialNpcs == null) initialNpcs = new List<NpcSeed>();
            if (initialNpcs.Count == 0) initialNpcs.AddRange(CreateDefaultNpcRoster());

            foreach (var seed in initialNpcs)
            {
                if (seed == null) continue;
                var state = GetOrCreate(seed.npcId, seed.companyId);
                ConfigureActionPreferences(state.npcId, seed.actionPreferences);
            }
            initialNpcsInitialized = true;
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

        /// <summary>기존 NPC 테스트 씬에서 사용하는 사용자 지정 명단이 없을 때의 기본 5인 프로필이다.</summary>
        public static List<NpcSeed> CreateDefaultNpcRoster()
        {
            return new List<NpcSeed>
            {
                new NpcSeed
                {
                    npcId = "npc-broker-yoon-hajin", companyId = "external-network",
                    profile = new NpcProfile
                    {
                        displayName = "윤하진", role = "M&A 브로커", affiliation = "외부 거래망",
                        personalitySummary = "거래 수수료와 협상 신뢰를 중시한다. 거래를 늘리는 상대를 선호하며, 약속을 깨면 정보 정확도를 낮춘다.",
                        gameplayRoleSummary = "비공개 매물과 거래 상대를 연결한다. 주가조작 제안 사건의 발신자이기도 하다.",
                        eventRelationBiases = new List<NpcEventRelationBias>
                        {
                            Bias(NpcMemoryType.GoodFaith, new NpcRelationDelta { trust = 1f }),
                            Bias(NpcMemoryType.ContractBreach, new NpcRelationDelta { trust = -2f, hostility = 1f })
                        }
                    },
                    actionPreferences = Preferences(
                        Preference("GoodFaith", NpcActionDisposition.Likes, "약속과 거래 신뢰를 지킨 행동"),
                        Preference("ContractBreach", NpcActionDisposition.Dislikes, "협상 약속을 어긴 행동"),
                        Preference("SavedCompany", NpcActionDisposition.Likes, "거래 상대의 기업을 살린 행동"))
                },
                new NpcSeed
                {
                    npcId = "npc-investigative-reporter-jung-sua", companyId = "external-network",
                    profile = new NpcProfile
                    {
                        displayName = "정수아", role = "탐사보도 기자", affiliation = "외부 인물",
                        personalitySummary = "공익 사건을 오래 추적한다. 한 번의 비위보다 정보 유출·뇌물·정치 결탁 같은 사건의 연결고리를 중시한다.",
                        gameplayRoleSummary = "정치 거래나 뇌물 등 공개된 사건을 추적해 조사·폭로 분기로 이어질 수 있다.",
                        anyEventRelationDelta = new NpcRelationDelta { interest = 1f },
                        eventRelationBiases = new List<NpcEventRelationBias>
                        {
                            Bias(NpcMemoryType.LeakedInfo, new NpcRelationDelta { interest = 1f }),
                            Bias(NpcMemoryType.Bribery, new NpcRelationDelta { interest = 1f })
                        }
                    },
                    actionPreferences = Preferences(
                        Preference("GoodFaith", NpcActionDisposition.Likes, "공익을 위한 약속 이행"),
                        Preference("ContractBreach", NpcActionDisposition.Dislikes, "취재 대상으로 보는 계약 위반"),
                        Preference("MassLayoff", NpcActionDisposition.Dislikes, "취재 대상으로 보는 구조조정 피해"),
                        Preference("HostileTakeover", NpcActionDisposition.Dislikes, "취재 대상으로 보는 적대적 인수"),
                        Preference("SavedCompany", NpcActionDisposition.Likes, "기업과 이해관계자에 영향을 준 구제 사건"),
                        Preference("PublicHumiliation", NpcActionDisposition.Dislikes, "공개적 망신을 초래한 행위"),
                        Preference("LeakedInfo", NpcActionDisposition.Dislikes, "취재 대상으로 보는 정보 유출"),
                        Preference("Bribery", NpcActionDisposition.Dislikes, "취재 대상으로 보는 뇌물 행위"))
                },
                new NpcSeed
                {
                    npcId = "npc-institutional-investor-kim-taeseong", companyId = "external-network",
                    profile = new NpcProfile
                    {
                        displayName = "김태성", role = "기관투자자", affiliation = "외부 투자자",
                        personalitySummary = "기업 성장률과 현금흐름을 따져본다. 단기 성과보다 장기 위험을 크게 본다.",
                        gameplayRoleSummary = "예측 가능한 성장과 투명한 재무를 기준으로 투자금 배분을 판단하는 외부 자본 주체다.",
                        eventRelationBiases = new List<NpcEventRelationBias>
                        {
                            Bias(NpcMemoryType.GoodFaith, new NpcRelationDelta { interest = 1f }),
                            Bias(NpcMemoryType.SavedCompany, new NpcRelationDelta { trust = 1f, interest = 1f }),
                            Bias(NpcMemoryType.HostileTakeover, new NpcRelationDelta { hostility = 1f }),
                            Bias(NpcMemoryType.MassLayoff, new NpcRelationDelta { interest = -1f })
                        }
                    },
                    actionPreferences = Preferences(
                        Preference("GoodFaith", NpcActionDisposition.Likes, "투명성과 신뢰를 보여준 행동"),
                        Preference("SavedCompany", NpcActionDisposition.Likes, "기업의 지속 가능성을 높인 행동"),
                        Preference("HostileTakeover", NpcActionDisposition.Dislikes, "불확실성을 키우는 적대적 인수"),
                        Preference("MassLayoff", NpcActionDisposition.Dislikes, "기업 안정성을 흔드는 대규모 구조조정"))
                },
                new NpcSeed
                {
                    npcId = "npc-political-lobbyist-park-sanghyeon", companyId = "external-network",
                    profile = new NpcProfile
                    {
                        displayName = "박상현", role = "정치 로비스트", affiliation = "외부 거래망",
                        personalitySummary = "정책 자문과 위험한 정치 거래 사이에서 기회를 찾지만, 노골적인 위험이 커지면 거리를 둔다.",
                        gameplayRoleSummary = "정부 계약과 규제 환경 관련 기회를 연결하며 정치 결탁 사건의 접점이 될 수 있다.",
                        eventRelationBiases = new List<NpcEventRelationBias>
                        {
                            Bias(NpcMemoryType.GoodFaith, new NpcRelationDelta { interest = 1f }),
                            Bias(NpcMemoryType.Bribery, new NpcRelationDelta { interest = -1f, hostility = 2f })
                        }
                    },
                    actionPreferences = Preferences(
                        Preference("GoodFaith", NpcActionDisposition.Likes, "합법적 자문과 약속 이행"),
                        Preference("Bribery", NpcActionDisposition.Dislikes, "노골적인 불법 정치 거래"))
                },
                new NpcSeed
                {
                    npcId = "npc-future-construction-ceo-kang-junhyeok", companyId = "future-construction",
                    profile = new NpcProfile
                    {
                        displayName = "강준혁", role = "CEO / 창업자", affiliation = "미래건설",
                        personalitySummary = "신중하고 현실적이며 자존심이 강하다. 회사명·계약·현장 인력의 연속성과 문서화된 보장을 중시한다.",
                        gameplayRoleSummary = "미래건설 인수 협상의 핵심 인물이다. 약속 이행은 신뢰를 쌓고, 계약·인력·회사 정체성의 해체는 강한 반발을 부른다.",
                        eventRelationBiases = new List<NpcEventRelationBias>
                        {
                            Bias(NpcMemoryType.GoodFaith, new NpcRelationDelta { trust = 2f, respect = 1f }),
                            Bias(NpcMemoryType.ContractBreach, new NpcRelationDelta { trust = -2f, hostility = 2f }),
                            Bias(NpcMemoryType.MassLayoff, new NpcRelationDelta { respect = -1f, hostility = 2f }),
                            Bias(NpcMemoryType.HostileTakeover, new NpcRelationDelta { trust = -1f, hostility = 2f })
                        }
                    },
                    actionPreferences = Preferences(
                        Preference("GoodFaith", NpcActionDisposition.Likes, "문서화된 약속을 이행한 행동"),
                        Preference("SavedCompany", NpcActionDisposition.Likes, "회사의 지속성을 지킨 행동"),
                        Preference("ContractBreach", NpcActionDisposition.Dislikes, "계약과 약속을 어긴 행동"),
                        Preference("MassLayoff", NpcActionDisposition.Dislikes, "현장 인력의 고용을 흔드는 구조조정"),
                        Preference("HostileTakeover", NpcActionDisposition.Dislikes, "회사의 자율성과 정체성을 위협하는 인수"))
                }
            };
        }

        private static NpcActionPreference Preference(string actionId, NpcActionDisposition disposition, string rationale)
        {
            return new NpcActionPreference { actionId = actionId, disposition = disposition, rationale = rationale };
        }

        private static List<NpcActionPreference> Preferences(params NpcActionPreference[] preferences)
        {
            return new List<NpcActionPreference>(preferences ?? Array.Empty<NpcActionPreference>());
        }

        private static NpcEventRelationBias Bias(NpcMemoryType memoryType, NpcRelationDelta delta)
        {
            return new NpcEventRelationBias { memoryType = memoryType, relationDelta = delta };
        }

        public NpcRelationDelta ApplyProfileEventBias(string npcId, NpcMemoryType memoryType, NpcRelationDelta relationDelta)
        {
            if (!TryGetProfile(npcId, out var seed) || seed.profile == null) return relationDelta;

            relationDelta = Add(relationDelta, seed.profile.anyEventRelationDelta);
            if (seed.profile.eventRelationBiases == null) return relationDelta;
            foreach (var bias in seed.profile.eventRelationBiases)
                if (bias != null && bias.memoryType == memoryType)
                    relationDelta = Add(relationDelta, bias.relationDelta);
            return relationDelta;
        }

        private static NpcRelationDelta Add(NpcRelationDelta left, NpcRelationDelta right)
        {
            left.trust += right.trust;
            left.respect += right.respect;
            left.fear += right.fear;
            left.hostility += right.hostility;
            left.dependency += right.dependency;
            left.interest += right.interest;
            return left;
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

        /// <summary>ID로 등록 상태를 조회한다. 찾지 못하면 false와 null 상태를 반환한다.</summary>
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
                StateChanged?.Invoke(state);
        }
    }
}
