using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TakeOver.NPC
{
    /// <summary>테스트 화면에서 사건 입력값을 편집하기 위한 설정이다.</summary>
    [Serializable]
    public sealed class NpcTestEventData
    {
        // 같은 사건의 재전달을 판별하는 키. 실제 이벤트 발생 단위로 고유 값을 만들어야 한다.
        public string eventId = "test-event";
        // 사건이 발생한 회사, 행위자, 영향을 받는 NPC의 식별자.
        // 회사 모델은 아직 없으므로 companyId는 기억에 보존하고 대상 NPC는 별도 ID로 지정한다.
        public string companyId = "company";
        public string actorId = "player";
        public string targetNpcId = "npc";
        public string actionId = "GoodFaith";
        // 기억 서비스로 넘길 사건 분류, 강도, 공개 범위와 지속 규칙.
        public NpcMemoryType memoryType = NpcMemoryType.GoodFaith;
        public int memoryStrength = 1;
        public NpcPublicity publicity = NpcPublicity.Company;
        public bool permanent;
        public int durationTurns = 3;
        public float reliability = 1f;
        // 사건 기록 해석과 관계 변화량. 실제 선택 결과에 따른 값은 추후 이벤트 규칙에서 공급한다.
        [TextArea] public string interpretation = "Test event interpretation";
        public NpcRelationDelta relationDelta;
    }

    /// <summary>
    /// 테스트 씬의 값과 버튼을 공용 NPC 사건 처리 경로에 연결하는 도구다.
    /// </summary>
    public sealed class NpcFeatureTestHarness : MonoBehaviour
    {
        // 같은 오브젝트에 두거나 Inspector에서 지정할 기록/관계 서비스.
        [SerializeField] private NpcMemoryService memoryService;
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private GameTurnClock turnClock;
        [SerializeField] private NpcEventChannel eventChannel;
        [SerializeField] private NpcEventProcessor eventProcessor;
        [SerializeField] private NpcScreenNavigation screenNavigation;
        [SerializeField] private NpcSaveService saveService;
        // 현재 선택된 NPC, 회사, 게임 턴과 Inspector에서 편집할 기본 사건.
        [SerializeField] private string npcId = "npc-ceo-01";
        [SerializeField] private string companyId = "portfolio-company-01";
        [SerializeField] private System.Collections.Generic.List<string> testTargetNpcIds = new System.Collections.Generic.List<string>();
        [SerializeField] private NpcTestEventData testEvent = new NpcTestEventData();
        // 테스트용 관계 버튼별 변화량. 실제 게임 규칙의 확정 수치가 아니다.
        [Header("테스트 관계 버튼 변화량")]
        [SerializeField] private NpcRelationDelta trustTestDelta = new NpcRelationDelta { trust = 10f };
        [SerializeField] private NpcRelationDelta respectTestDelta = new NpcRelationDelta { respect = 10f };
        [SerializeField] private NpcRelationDelta fearTestDelta = new NpcRelationDelta { fear = 10f };
        [SerializeField] private NpcRelationDelta hostilityTestDelta = new NpcRelationDelta { hostility = 10f };
        [SerializeField] private NpcRelationDelta dependencyTestDelta = new NpcRelationDelta { dependency = 10f };
        [SerializeField] private NpcRelationDelta interestTestDelta = new NpcRelationDelta { interest = 10f };
        // 테스트용 기억 유형별 효과. 프로토타입에서 관계 변화 흐름을 눈으로 확인하기 위한 예시값이다.
        [Header("테스트 Memory 관계 변화량")]
        [SerializeField] private NpcRelationDelta goodFaithMemoryDelta = new NpcRelationDelta { trust = 5f, respect = 3f, interest = 1f };
        [SerializeField] private NpcRelationDelta contractBreachMemoryDelta = new NpcRelationDelta { trust = -10f, hostility = 8f };
        [SerializeField] private NpcRelationDelta massLayoffMemoryDelta = new NpcRelationDelta { respect = 2f, hostility = 10f };
        [SerializeField] private NpcRelationDelta hostileTakeoverMemoryDelta = new NpcRelationDelta { respect = -4f, fear = 8f, hostility = 4f };
        [SerializeField] private NpcRelationDelta savedCompanyMemoryDelta = new NpcRelationDelta { trust = 8f, dependency = 5f, respect = 2f };
        [SerializeField] private NpcRelationDelta publicHumiliationMemoryDelta = new NpcRelationDelta { hostility = 12f, respect = -6f };
        [SerializeField] private NpcRelationDelta leakedInfoMemoryDelta = new NpcRelationDelta { trust = -7f, interest = 4f, hostility = 3f };
        [SerializeField] private NpcRelationDelta briberyMemoryDelta = new NpcRelationDelta { trust = -6f, dependency = 4f, hostility = 5f };
        // Unity Inspector 연결점과 코드 구독자를 위한 이벤트 알림.
        [SerializeField] private UnityEvent onTestEventRecorded;
        // 기존 테스트 씬 Inspector 연결을 유지하는 호환 진입점이다.
        [SerializeField] private UnityEvent onOpenEventScreen = new UnityEvent();
        [SerializeField] private UnityEvent onOpenPortfolioScreen = new UnityEvent();

        // 화면이 읽는 현재 테스트 상태와 가장 최근에 반환된 기억.
        public NpcRuntimeState RuntimeState { get; private set; }
        public NpcMemoryRecord LastRecordedMemory { get; private set; }
        public int CurrentTurn => turnClock == null ? 1 : turnClock.CurrentTurn;
        public IReadOnlyList<NpcStateRegistry.NpcSeed> NpcProfiles => registry == null
            ? Array.Empty<NpcStateRegistry.NpcSeed>()
            : registry.Profiles;
        public NpcStateRegistry.NpcSeed SelectedProfile => registry != null && registry.TryGetProfile(npcId, out var profile)
            ? profile
            : null;
        public event Action<NpcMemoryRecord> testEventRecorded;

        /// <summary>Awake 때 서비스를 찾거나 만들고, 이 브리지 전용 NPC 상태를 초기화한다.</summary>
        private void Awake()
        {
            if (registry == null) registry = GetComponent<NpcStateRegistry>();
            if (registry == null) registry = gameObject.AddComponent<NpcStateRegistry>();
            registry.EnsureInitialNpcsInitialized();
            if (memoryService == null) memoryService = GetComponent<NpcMemoryService>();
            if (memoryService == null) memoryService = gameObject.AddComponent<NpcMemoryService>();
            memoryService.ConfigureRegistry(registry);
            if (turnClock == null) turnClock = GetComponent<GameTurnClock>();
            if (turnClock == null) turnClock = gameObject.AddComponent<GameTurnClock>();
            turnClock.Configure(registry, memoryService);
            if (eventChannel == null) eventChannel = GetComponent<NpcEventChannel>();
            if (eventChannel == null) eventChannel = gameObject.AddComponent<NpcEventChannel>();
            if (eventProcessor == null) eventProcessor = GetComponent<NpcEventProcessor>();
            if (eventProcessor == null) eventProcessor = gameObject.AddComponent<NpcEventProcessor>();
            if (screenNavigation == null) screenNavigation = GetComponent<NpcScreenNavigation>();
            if (screenNavigation == null) screenNavigation = gameObject.AddComponent<NpcScreenNavigation>();
            if (saveService == null) saveService = GetComponent<NpcSaveService>();
            if (saveService == null) saveService = gameObject.AddComponent<NpcSaveService>();
            saveService.Configure(registry, turnClock);
            if (testTargetNpcIds == null) testTargetNpcIds = new System.Collections.Generic.List<string>();
            if (!registry.TryGetProfile(npcId, out var selectedProfile) && registry.Profiles.Count > 0)
                npcId = registry.Profiles[0].npcId;
            if (registry.TryGetProfile(npcId, out selectedProfile)) companyId = selectedProfile.companyId;
            RuntimeState = registry.GetOrCreate(npcId, companyId);
            SetSelectedTarget(npcId);
        }

        /// <summary>테스트 화면에서 선택한 프로필의 관계·기억 상태를 이후 입력 대상으로 바꾼다.</summary>
        public bool SelectNpcProfile(string selectedNpcId)
        {
            if (registry == null || !registry.TryGetProfile(selectedNpcId, out var profile)) return false;
            npcId = profile.npcId;
            companyId = profile.companyId;
            RuntimeState = registry.GetOrCreate(npcId, companyId);
            SetSelectedTarget(npcId);
            LastRecordedMemory = RuntimeState.memories.Count == 0
                ? null
                : RuntimeState.memories[RuntimeState.memories.Count - 1];
            return true;
        }

        /// <summary>기본 사건 버튼과 관계·기억 테스트가 현재 선택된 NPC 하나를 대상으로 하게 맞춘다.</summary>
        private void SetSelectedTarget(string selectedNpcId)
        {
            if (testTargetNpcIds == null) testTargetNpcIds = new List<string>();
            testTargetNpcIds.Clear();
            testTargetNpcIds.Add(selectedNpcId);
            if (testEvent == null) testEvent = new NpcTestEventData();
            testEvent.companyId = companyId;
            testEvent.targetNpcId = selectedNpcId;
        }

        private void OnEnable()
        {
            if (eventProcessor != null) eventProcessor.MemoryRecorded += HandleMemoryRecorded;
        }

        private void OnDisable()
        {
            if (eventProcessor != null) eventProcessor.MemoryRecorded -= HandleMemoryRecorded;
        }

        /// <summary>Inspector에 설정한 기본 사건을 기억 기록 경로에 넣는다.</summary>
        public void TriggerTestEvent() { PublishConfiguredTestEvent(testEvent); }

        /// <summary>NPC 파트의 이벤트 진입 버튼에서 이벤트 파트 화면을 여는 연결점이다.</summary>
        public void OpenEventScreen()
        {
            // 기존 Inspector 연결과 공용 내비게이션 연결은 각각 호출된다. 같은 화면 전환 함수를 양쪽에 중복 등록하지 않는다.
            onOpenEventScreen?.Invoke();
            screenNavigation.RequestEventScreen();
        }

        /// <summary>NPC 파트의 포트폴리오 진입 버튼에서 지분/포트폴리오 화면을 여는 연결점이다.</summary>
        public void OpenPortfolioScreen()
        {
            // 기존 Inspector 연결과 공용 내비게이션 연결은 각각 호출된다. 같은 화면 전환 함수를 양쪽에 중복 등록하지 않는다.
            onOpenPortfolioScreen?.Invoke();
            screenNavigation.RequestPortfolioScreen();
        }

        /// <summary>테스트 포트폴리오 파트가 다른 게임 파트와 같은 발행 계약으로 사건을 전달한다.</summary>
        public void PublishTestEvent()
        {
            eventChannel.Publish(new NpcEventPayload
            {
                eventId = Guid.NewGuid().ToString("N"), companyId = companyId,
                actorId = testEvent.actorId, actionId = testEvent.actionId,
                targetNpcIds = new System.Collections.Generic.List<string>(testTargetNpcIds),
                memoryType = testEvent.memoryType, strength = testEvent.memoryStrength,
                publicity = testEvent.publicity, permanent = testEvent.permanent,
                durationTurns = testEvent.durationTurns, reliability = testEvent.reliability,
                interpretation = testEvent.interpretation, relationDelta = testEvent.relationDelta
            });
        }

        /// <summary>공용 채널로 전달된 결과를 명시된 NPC별 Memory로 기록한다.</summary>
        private void HandleMemoryRecorded(NpcMemoryRecord recorded)
        {
            if (recorded == null) return;
            if (recorded.targetId == RuntimeState.npcId) LastRecordedMemory = recorded;
            testEventRecorded?.Invoke(recorded);
            onTestEventRecorded?.Invoke();
        }

        /// <summary>관계·기억·턴을 초기 테스트 상태로 되돌린다.</summary>
        public void ResetSelectedNpcState()
        {
            if (RuntimeState == null) return;
            memoryService.ResetState(RuntimeState);
            LastRecordedMemory = null;
        }

        public void ResetAllNpcStates()
        {
            foreach (var state in registry.States) memoryService.ResetState(state);
            LastRecordedMemory = null;
            turnClock.RestoreTurn(1);
        }

        public void ResetTestState() => ResetAllNpcStates();

        /// <summary>기억과 별개로 단일 관계 변화량을 적용한다.</summary>
        public void ApplyTestRelationDelta(NpcRelationDelta delta)
        {
            memoryService.ApplyRelationDelta(RuntimeState, delta, CurrentTurn);
        }

        // 아래 여섯 함수는 UI 버튼/Inspector에서 연결하기 위한 관계별 진입점이다.
        public void ApplyTrustTestDelta() => ApplyTestRelationDelta(trustTestDelta);
        public void ApplyRespectTestDelta() => ApplyTestRelationDelta(respectTestDelta);
        public void ApplyFearTestDelta() => ApplyTestRelationDelta(fearTestDelta);
        public void ApplyHostilityTestDelta() => ApplyTestRelationDelta(hostilityTestDelta);
        public void ApplyDependencyTestDelta() => ApplyTestRelationDelta(dependencyTestDelta);
        public void ApplyInterestTestDelta() => ApplyTestRelationDelta(interestTestDelta);

        /// <summary>
        /// 선택한 기억 유형에 맞는 테스트 ID·해석·효과를 채운 뒤 공통 이벤트 처리 경로를 호출한다.
        /// 테스트 입력마다 새 사건 ID를 부여해 반복 클릭도 각각의 발생 사건으로 기록한다.
        /// </summary>
        public void TriggerTestMemory(NpcMemoryType memoryType)
        {
            var memory = testEvent;
            memory.eventId = Guid.NewGuid().ToString("N");
            memory.memoryType = memoryType;
            memory.actionId = memoryType.ToString();
            memory.interpretation = $"테스트용 {memoryType} 기억";
            memory.relationDelta = GetTestMemoryDelta(memoryType);
            PublishConfiguredTestEvent(memory);
        }

        /// <summary>테스트 화면용 기억 유형별 관계 변화 예시를 반환한다.</summary>
        private NpcRelationDelta GetTestMemoryDelta(NpcMemoryType memoryType)
        {
            switch (memoryType)
            {
                case NpcMemoryType.GoodFaith:
                    return goodFaithMemoryDelta;
                case NpcMemoryType.ContractBreach:
                    return contractBreachMemoryDelta;
                case NpcMemoryType.MassLayoff:
                    return massLayoffMemoryDelta;
                case NpcMemoryType.HostileTakeover:
                    return hostileTakeoverMemoryDelta;
                case NpcMemoryType.SavedCompany:
                    return savedCompanyMemoryDelta;
                case NpcMemoryType.PublicHumiliation:
                    return publicHumiliationMemoryDelta;
                case NpcMemoryType.LeakedInfo:
                    return leakedInfoMemoryDelta;
                case NpcMemoryType.Bribery:
                    return briberyMemoryDelta;
                default:
                    return NpcRelationDelta.Zero;
            }
        }

        /// <summary>
        /// 사건 전달 데이터를 Memory 레코드로 바꾸어 서비스에 기록하고, UI/Inspector 알림을 발생시킨다.
        /// 실제 포트폴리오 시스템은 사건 결과가 확정된 지점에서 이 진입점을 호출하도록 연결한다.
        /// </summary>
        public void PublishConfiguredTestEvent(NpcTestEventData testData)
        {
            if (testData == null) throw new ArgumentNullException(nameof(testData));
            eventChannel.Publish(new NpcEventPayload
            {
                eventId = string.IsNullOrWhiteSpace(testData.eventId) || testData.eventId == "test-event"
                    ? Guid.NewGuid().ToString("N") : testData.eventId,
                companyId = string.IsNullOrWhiteSpace(testData.companyId) || testData.companyId == "company"
                    ? companyId : testData.companyId,
                actorId = testData.actorId,
                targetNpcIds = new System.Collections.Generic.List<string>
                {
                    string.IsNullOrWhiteSpace(testData.targetNpcId) || testData.targetNpcId == "npc"
                        ? npcId : testData.targetNpcId
                },
                actionId = testData.actionId,
                memoryType = testData.memoryType, strength = testData.memoryStrength,
                publicity = testData.publicity, permanent = testData.permanent,
                durationTurns = testData.durationTurns, reliability = testData.reliability,
                interpretation = testData.interpretation,
                relationDelta = testData.relationDelta
            });
        }

        /// <summary>테스트 게임 날짜를 하루 진행하고 비영구 기억의 영향력 감소를 요청한다.</summary>
        public void AdvanceTurn()
        {
            turnClock.AdvanceTurn();
        }

        public void SaveNpcState() => saveService.Save();
        public bool LoadNpcState()
        {
            if (!saveService.Load()) return false;
            RuntimeState = registry.GetOrCreate(npcId, companyId);
            LastRecordedMemory = RuntimeState.memories.Count == 0 ? null : RuntimeState.memories[RuntimeState.memories.Count - 1];
            return true;
        }
    }
}
