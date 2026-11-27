using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TakeOver.NPC;
using TakeOver.Events;
using TakeOver.PlayerMemories;


namespace TakeOver.Events
{
    public sealed partial class EventSystemController : MonoBehaviour, TakeOver.SaveSystem.ISaveParticipant
    {
        [SerializeField] private List<EventDefinition> events = new List<EventDefinition>();
        [SerializeField] private StoryEventDatabase storyDatabase;
        public IReadOnlyList<EventDefinition> StoryEvents => events.Where(item => item.IsStory).ToList().AsReadOnly();
        internal EventDefinition TutorialVisit => events.Find(item => item.IsTutorialOnly);
        [SerializeField] private EventDetailView detailView;
        [SerializeField] private EventNegotiationView negotiationView;
        private sealed class Occurrence
        {
            public EventDefinition definition;
            public string companyId;
            public int firstDay;
            public float greenmailStake;
            public int serial;
        }
        private sealed class NegotiationChoice
        {
            public EventChoice choice;
            public int originalIndex;
            public string companyId = "";
            public string companyName = "";
        }
        private readonly List<NegotiationChoice> negotiationChoices = new List<NegotiationChoice>();
        private readonly List<Occurrence> occurrences = new List<Occurrence>();
        private readonly List<EventDefinition> todayEvents = new List<EventDefinition>();
        private readonly List<TodayEventInfo> todayInfos = new List<TodayEventInfo>();
        private readonly Dictionary<string, int> selectedChoices = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> storyResults = new Dictionary<string, bool>();
        private bool tutorialRewardGranted;
        public bool TutorialRewardGranted => tutorialRewardGranted;
        public bool TryGetStoryResult(string eventId, out bool succeeded) => storyResults.TryGetValue(eventId, out succeeded);
        // 스토리를 해결한 날. 일지가 실제 진행 순서대로 정렬할 때 쓴다. 이전 저장에서 온 기록에는 없다.
        private readonly Dictionary<string, int> storyResolvedDays = new Dictionary<string, int>();
        public bool tryGetStoryResolvedDay(string eventId, out int day) => storyResolvedDays.TryGetValue(eventId, out day);
        private readonly Dictionary<(string, string), int> companyChoices = new Dictionary<(string, string), int>();
        private readonly Dictionary<(string, string), int> availableDays = new Dictionary<(string, string), int>();
        private TodayEventInfo activeInfo;
        private int currentDay;
        private GameTurnClock turnClock;
        private ActionPointService actionPointService;
        private EventWorldReader worldReader;
        private ScreenState screenState;
        private enum ScreenState { Closed, Detail, Negotiation, CompanyExchange, CompanyChoice, Result }
        public event Action<NegotiationStartedInfo> negotiationStarted;
        public event Action<ChoiceConfirmedInfo> choiceConfirmed;
        public event Action todayEventsChanged;
        public bool IsClosed => screenState == ScreenState.Closed;
        public bool IsDetailOpen => screenState == ScreenState.Detail;
        public string ActiveEventId => activeInfo?.definition.getId();

        private void Awake()
        {
            campaignPlan = EarlyCampaignPlan.Load();
            var general = Resources.Load<GeneralEventDatabase>("Events/GeneralEventDatabase");
            if (general != null)
            {
                events = general.Events.ToList();
                if (events.Any(item => item == null || item.IsStory || item.IsTutorialOnly) || events.Select(item => item.getId()).Distinct().Count() != events.Count)
                    throw new InvalidOperationException("일반 이벤트 DB의 분류·ID·참조를 확인해 주십시오.");
            }
            if (storyDatabase == null) storyDatabase = Resources.Load<StoryEventDatabase>("Story/StoryEventDatabase");
            if (storyDatabase == null) throw new InvalidOperationException("스토리 이벤트 DB가 없습니다.");
            // 기존 씬의 등록을 남겨도 스토리는 전용 DB가 유일한 공급원이 된다.
            events.RemoveAll(item => item != null && item.IsStory);
            if (storyDatabase != null)
                foreach (var definition in storyDatabase.Events)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.getId()) || !definition.IsStory || definition.IsTutorialOnly ||
                        definition.getChoiceGeneration() != EventChoiceGeneration.Fixed ||
                        events.Any(item => item == null || item.getId() == definition.getId()))
                        throw new InvalidOperationException("스토리 이벤트 DB의 분류·ID·참조를 확인해 주십시오.");
                    events.Add(definition);
                }
            var lesson = Resources.Load<EventDefinition>("Story/TutorialVisit");
            if (lesson != null && !events.Contains(lesson)) events.Add(lesson);
            InitializeMissionList();
            turnClock = FindFirstObjectByType<GameTurnClock>();
            actionPointService = FindFirstObjectByType<ActionPointService>();
            worldReader = new EventWorldReader(FindFirstObjectByType<Companies>(), FindFirstObjectByType<NpcStateRegistry>(), GetComponent<EventTempWorldData>());
            detailView.hide();
            negotiationView.hide();
        }
        private void OnEnable()
        {
            // 회사 실적·시장 이벤트 재계산이 끝난 새 날짜의 값으로 후보를 판정한다.
            if (turnClock != null) turnClock.DayStarted += onDayStarted;
            if (turnClock != null) turnClock.DayStarted += OnMandatoryDayStarted;
        }
        private void OnDisable()
        {
            if (turnClock != null) turnClock.DayStarted -= onDayStarted;
            if (turnClock != null) turnClock.DayStarted -= OnMandatoryDayStarted;
        }
        private System.Collections.IEnumerator Start()
        {
            // 지분매입팀 Start의 첫 STAKE 계산이 끝난 뒤 첫날을 판정한다.
            yield return null;
            if (TakeOver.SaveSystem.GameSessionCoordinator.Active?.IsRestoring == true) yield break;
            if (currentDay == 0) onDayStarted(turnClock != null ? turnClock.CurrentTurn : 1);
            EvaluateMandatoryDeadline(currentDay);
        }
        public IReadOnlyList<EventDefinition> getTodayEvents() => todayEvents.AsReadOnly();
        public IReadOnlyList<TodayEventInfo> getTodayEventInfos() => todayInfos.AsReadOnly();
        public int getCurrentDay() => currentDay;
        public int getNegotiationChoiceCount() => negotiationChoices.Count;
        public string getNegotiationChoiceText(int choiceIndex) => screenState == ScreenState.CompanyChoice ?
            negotiationChoices[choiceIndex].companyName : negotiationChoices[choiceIndex].choice.getText();
        public string getNegotiationChoiceDescription(int choiceIndex)
        {
            var option = negotiationChoices[choiceIndex];
            if (screenState == ScreenState.CompanyChoice)
                return $"현재 보유 지분 {StakeText.Format(worldReader.getStake(option.companyId))}";
            var choice = option.choice;
            if (!string.IsNullOrWhiteSpace(choice.getDescription()))
                return activeInfo.resolve(choice.getDescription());
            string reply = choice.getPlayerLine();
            if (string.IsNullOrWhiteSpace(reply))
                reply = choice.ResultLines.FirstOrDefault(line => line.player)?.text;
            if (string.IsNullOrWhiteSpace(reply) || reply == choice.getText()) return "";
            reply = activeInfo.resolve(reply).Trim();
            int sentence = reply.IndexOfAny(new[] { '.', '?', '!', '\n' });
            if (sentence >= 0) reply = reply.Substring(0, sentence + 1).Trim();
            const int maxLength = 42;
            if (reply.Length <= maxLength) return reply;
            int lastSpace = reply.LastIndexOf(' ', maxLength - 1);
            return reply.Substring(0, lastSpace >= maxLength / 2 ? lastSpace : maxLength - 1) + "…";
        }
        public bool tryGetChoice(string eventId, out int choiceIndex)
        {
            choiceIndex = -1;
            return eventId != null && selectedChoices.TryGetValue(eventId, out choiceIndex);
        }
        public bool tryGetChoice(string eventId, string companyId, out int choiceIndex) =>
            companyChoices.TryGetValue((eventId, companyId ?? ""), out choiceIndex);
        private string getKeyCompany(EventDefinition definition, string companyId) => definition.hasCompanyConditions() ? companyId : "";
        private List<EventCompanyData> getOwnedCompanies(EventDefinition definition = null)
        {
            var companies = new List<EventCompanyData>();
            foreach (EventCompanyData company in worldReader.getCompanies())
                if (worldReader.getStake(company.companyId) > 0 && CompanyStoryStarted(company.companyId) &&
                    ((definition ?? activeInfo?.definition)?.getId() != "stock_manipulation_01" || worldReader.getConsumerReaction(company.companyId) <= 30)) companies.Add(company);
            return companies;
        }
        private bool hasRequiredHoldings(EventDefinition definition) =>
            definition.getChoiceGeneration() != EventChoiceGeneration.OwnedCompanies || getOwnedCompanies(definition).Count > 0;
        private bool isGreenmailOfferCurrent(EventDefinition definition, string companyId) =>
            !definition.getConditions().Any(condition => condition.kind == EventConditionKind.CompanyGreenmailStake) ||
            meetsConditions(definition, companyId) && occurrences.Exists(item => item.definition == definition && item.companyId == companyId && currentDay < OccurrenceExpiry(definition, item.firstDay));
        private bool canAppear(EventDefinition definition, string companyId)
        {
            var key = (definition.getId(), getKeyCompany(definition, companyId));
            return (definition.Repeatable || !(definition.hasCompanyConditions() && !definition.IsStory ? companyChoices.ContainsKey(key) : selectedChoices.ContainsKey(definition.getId()))) && (IsMandatory(definition) || !availableDays.TryGetValue(key, out int day) || currentDay >= day);
        }
        private bool meetsConditions(EventDefinition definition, string companyId)
        {
            if (!PresentationGate(definition, companyId)) return false;
            if (definition.IsTutorialOnly && !TakeOver.Story.StoryDirector.IsRunning) return false;
            if (definition.hasCompanyConditions() && definition.getCompanyIds().Count > 0 && !definition.getCompanyIds().Contains(companyId)) return false;
            float offeredStake = occurrences.Find(item => item.definition == definition && item.companyId == companyId)?.greenmailStake ?? 0;
            return meetsConditions(definition.getConditions(), companyId, offeredStake);
        }
        private bool meetsConditions(IReadOnlyList<EventCondition> conditions, string eventCompanyId, float offeredGreenmailStake = 0)
        {
            foreach (EventCondition condition in conditions)
            {
                if (condition == null || !Enum.IsDefined(typeof(EventConditionKind), condition.kind) ||
                    !Enum.IsDefined(typeof(EventComparison), condition.comparison) || float.IsNaN(condition.value) || float.IsInfinity(condition.value)) return false;
                string companyId = string.IsNullOrEmpty(condition.companyId) ? eventCompanyId : condition.companyId;
                if (condition.kind == EventConditionKind.PlayerHasMemory || condition.kind == EventConditionKind.PlayerLacksMemory)
                {
                    var memories = FindFirstObjectByType<PlayerMemoryService>();
                    if (memories == null || string.IsNullOrWhiteSpace(condition.playerMemoryKey) ||
                        memories.HasMemory(condition.playerMemoryKey, string.IsNullOrEmpty(condition.companyId) ? null : condition.companyId) != (condition.kind == EventConditionKind.PlayerHasMemory)) return false;
                    continue;
                }
                if ((condition.kind == EventConditionKind.CompanyStake || condition.kind == EventConditionKind.CompanyGreenmailStake || condition.kind == EventConditionKind.CompanyConsumerReaction ||
                     condition.kind == EventConditionKind.CompanyProfitDeteriorating) && worldReader.getCompany(companyId) == null) return false;
                if (condition.kind == EventConditionKind.EventCompleted || condition.kind == EventConditionKind.EventChoice ||
                    condition.kind == EventConditionKind.StorySucceeded || condition.kind == EventConditionKind.StoryFailed)
                {
                    if (string.IsNullOrEmpty(condition.requiredEventId) || !events.Any(item => item.getId() == condition.requiredEventId)) return false;
                    bool met;
                    if (condition.kind == EventConditionKind.StorySucceeded || condition.kind == EventConditionKind.StoryFailed)
                        met = storyResults.TryGetValue(condition.requiredEventId, out bool success) && success == (condition.kind == EventConditionKind.StorySucceeded);
                    else
                        met = (string.IsNullOrEmpty(condition.companyId)
                            ? selectedChoices.TryGetValue(condition.requiredEventId, out int answer)
                            : companyChoices.TryGetValue((condition.requiredEventId, condition.companyId), out answer)) &&
                            (condition.kind == EventConditionKind.EventCompleted || answer == condition.requiredChoiceIndex);
                    if (!met) return false;
                    continue;
                }
                float threshold = condition.value;
                if (!string.IsNullOrEmpty(condition.relaxedByEventId))
                {
                    if (condition.relaxedByEventId == "political_donation_01" && politicalFavorGranted) threshold = condition.relaxedValue;
                    foreach (var selected in companyChoices)
                        if (selected.Key.Item1 == condition.relaxedByEventId && selected.Value == condition.relaxedByChoiceIndex)
                            threshold = condition.relaxedValue;
                }
                float actual;
                if (condition.kind == EventConditionKind.CompanyGreenmailStake)
                {
                    if (condition.comparison != EventComparison.AtLeast || !worldReader.tryGetGreenmailStake(companyId, out actual, out threshold)) return false;
                    // 발행한 제안의 요구 지분은 유효기간 동안 유지한다. 보유량과 거래 가능 여부는 재검사한다.
                    if (companyId == eventCompanyId && offeredGreenmailStake > 0) threshold = offeredGreenmailStake;
                }
                else if (condition.kind == EventConditionKind.Day) actual = currentDay;
                else if (condition.kind == EventConditionKind.CompanyStake) actual = worldReader.getStake(companyId);
                else if (condition.kind == EventConditionKind.CompanyConsumerReaction) actual = worldReader.getConsumerReaction(companyId);
                else if (condition.kind == EventConditionKind.CompanyProfitDeteriorating) actual = worldReader.isProfitDeteriorating(companyId) ? 1 : 0;
                else
                {
                    string npcId=string.IsNullOrEmpty(condition.npcId) ? worldReader.getCompany(companyId)?.ceoNpcId : condition.npcId;
                    if(string.IsNullOrEmpty(npcId))return false;
                    NpcRelationState relation = worldReader.getNpcRelation(npcId);
                    switch (condition.relationAxis)
                    {
                        case EventRelationAxis.trust: actual = relation.trust; break;
                        case EventRelationAxis.respect: actual = relation.respect; break;
                        case EventRelationAxis.closeness: actual = relation.closeness; break;
                        default: return false;
                    }
                }
                if (condition.comparison == EventComparison.AtLeast ? actual < threshold : actual > threshold) return false;
            }
            return true;
        }
        public bool GrantTutorialReward(Company company)
        {
            if (tutorialRewardGranted) return true;
            if (company == null || company.definition.companyID != TakeOver.Story.StoryDirector.CompanyId || company.state.totalShares <= 0) return false;
            float amount = company.state.totalShares * .05f;
            float owned = company.state.getBaseStat(CompanyStat.PLAYER_SHARES);
            if (float.IsNaN(owned) || float.IsInfinity(owned) || owned < 0 || owned + amount > company.state.totalShares) return false;
            company.state.addStat(CompanyStat.PLAYER_SHARES, amount);
            company.calculator.calculateAboutShares(company);
            tutorialRewardGranted = true;
            return true;
        }
        public void onDayStarted(int dayNumber)
        {
            if (dayNumber <= currentDay) return;
            currentDay = dayNumber;
            for (int i = occurrences.Count - 1; i >= 0; i--)
            {
                Occurrence occurrence = occurrences[i];
                int expiry = OccurrenceExpiry(occurrence.definition, occurrence.firstDay);
                if (!hasRequiredHoldings(occurrence.definition)) occurrences.RemoveAt(i);
                else if (currentDay >= expiry)
                {
                    availableDays[(occurrence.definition.getId(), getKeyCompany(occurrence.definition, occurrence.companyId))] = expiry + 7;
                    occurrences.RemoveAt(i);
                }
                else if (!meetsConditions(occurrence.definition, occurrence.companyId)) occurrences.RemoveAt(i);
            }
            foreach (EventDefinition definition in events)
            {
                if (definition.Repeatable || definition.CampaignDay > 0) continue;
                if (!hasRequiredHoldings(definition) || occurrences.Exists(item => item.definition == definition)) continue;
                string companyId = "";
                if (definition.hasCompanyConditions())
                {
                    var candidates = new List<string>();
                    foreach (EventCompanyData company in worldReader.getCompanies())
                        if (canAppear(definition, company.companyId) && meetsConditions(definition, company.companyId)) candidates.Add(company.companyId);
                    if (candidates.Count == 0) continue;
                    companyId = candidates.OrderBy(id => DailyOrder("company:" + definition.getId() + ":" + id))
                        .ThenBy(id => id, StringComparer.Ordinal).First();
                }
                else
                {
                    if (!canAppear(definition, "") || !meetsConditions(definition, "")) continue;
                    if (definition.getCompanyIds().Count > 0) companyId = definition.getCompanyIds()[0];
                }
                float offeredStake = 0;
                if (definition.getConditions().Any(condition => condition.kind == EventConditionKind.CompanyGreenmailStake))
                    worldReader.tryGetGreenmailStake(companyId, out _, out offeredStake);
                occurrences.Add(new Occurrence { definition = definition, companyId = companyId, firstDay = currentDay, greenmailStake = offeredStake });
            }
            SpawnFirstContacts();
            SpawnRepeatableOffers();
            if (turnClock == null || dayNumber != turnClock.CurrentTurn) EvaluateMandatoryDeadline(dayNumber);
            rebuildToday();
            todayEventsChanged?.Invoke();
        }
        public void refreshAvailability()
        {
            EnsureMandatoryOffer();
            SpawnFirstContacts();
            if (rebuildToday()) todayEventsChanged?.Invoke();
        }
        private bool rebuildToday()
        {
            var previous = new List<TodayEventInfo>(todayInfos);
            todayEvents.Clear();
            todayInfos.Clear();
            foreach (Occurrence occurrence in occurrences)
            {
                if (!ShowMandatoryOccurrence(occurrence.definition)) continue;
                if (!hasRequiredHoldings(occurrence.definition) || !meetsConditions(occurrence.definition, occurrence.companyId)) continue;
                todayEvents.Add(occurrence.definition);
                todayInfos.Add(worldReader.getEventInfo(occurrence.definition, occurrence.companyId,
                    IsMandatory(occurrence.definition) ? Mathf.Max(0, DueDay(occurrence.definition) - currentDay) : occurrence.firstDay + occurrence.definition.getValidDays() - currentDay));
            }
            if (previous.Count != todayInfos.Count) return true;
            for (int i = 0; i < previous.Count; i++)
                if (previous[i].definition != todayInfos[i].definition || previous[i].companyId != todayInfos[i].companyId
                    || previous[i].remainingDays != todayInfos[i].remainingDays) return true;
            return false;
        }
        public void openEvent(string eventId)
        {
            if (EventSceneTransition.IsBusy) return;
            if (TakeOver.SaveSystem.GameSessionCoordinator.Active?.IsPersistenceBlocked == true) return;
            if (mandatoryState == MandatoryStoryState.Failed || mandatoryState == MandatoryStoryState.Forced && !pendingMandatoryEvents.Contains(eventId)) return;
            if (!TakeOver.Story.StoryDirector.CanOpenEvent(eventId)) return;
            if (screenState != ScreenState.Closed) return;
            Occurrence occurrence = occurrences.Find(item => item.definition.getId() == eventId);
            if (occurrence != null && (!hasRequiredHoldings(occurrence.definition) || !meetsConditions(occurrence.definition, occurrence.companyId) || !isGreenmailOfferCurrent(occurrence.definition, occurrence.companyId)))
            {
                refreshAvailability();
                return;
            }
            activeInfo = todayInfos.Find(info => info.definition.getId() == eventId);
            if (activeInfo == null) return;
            screenState = ScreenState.Detail;
            detailView.show(activeInfo, this);
        }
        public void closeDetail()
        {
            if (screenState != ScreenState.Detail) return;
            detailView.hide();
            activeInfo = null;
            screenState = ScreenState.Closed;
        }
        public void startNegotiation()
        {
            if (EventSceneTransition.IsBusy) return;
            var session = TakeOver.SaveSystem.GameSessionCoordinator.Active;
            if (session?.IsPersistenceBlocked == true) return;
            if (!TakeOver.Story.StoryDirector.CanNegotiate(ActiveEventId)) return;
            if (screenState != ScreenState.Detail) return;
            if(activeInfo==null || !hasRequiredHoldings(activeInfo.definition) || !meetsConditions(activeInfo.definition,activeInfo.companyId) || !isGreenmailOfferCurrent(activeInfo.definition,activeInfo.companyId))
            {closeDetail();refreshAvailability();return;}
            buildNegotiationChoices();
            if (negotiationChoices.Count == 0) return;
            int cost = activeInfo.definition.getApCost();
            if (actionPointService != null && actionPointService.Current < cost)
            {
                detailView.showInsufficientEnergy();
                return;
            }
            if (session != null && !session.SaveBeforeEvent())
            {
                FindFirstObjectByType<FloatingMessage>()?.Show("사건 시작 전 저장에 실패했습니다. 다시 시도해 주세요.");
                return;
            }
            if (actionPointService == null)
                Debug.LogWarning("ActionPointService was not found; starting negotiation without an AP check.");
            else if (!actionPointService.TryConsume(cost))
            {
                detailView.showInsufficientEnergy();
                return;
            }
            screenState = ScreenState.Negotiation;
            negotiationStarted?.Invoke(new NegotiationStartedInfo(activeInfo.definition.getId(), activeInfo.definition.getApCost()));
            Action present = () => {
                detailView.hide();
                if (ActiveOpening.Count > 0) BeginScriptedNegotiation();
                else negotiationView.show(activeInfo, this, worldReader.getSpeakerIllustration(activeInfo), worldReader.getSpeakerDimmedIllustration(activeInfo));
            };
            if (TakeOver.Story.StoryDirector.IsApplyingWholeSkip) present();
            else EventSceneTransition.Play(present);
        }
        private void buildNegotiationChoices(bool companies = false)
        {
            negotiationChoices.Clear();
            IReadOnlyList<EventChoice> choices = ActiveChoices;
            if (companies && choices.Count > 0)
            {
                var ownedCompanies = getOwnedCompanies()
                    .OrderBy(company => DailyOrder("choice:" + ActiveEventId + ":" + company.companyId))
                    .ThenBy(company => company.companyId, StringComparer.Ordinal).Take(3);
                foreach (EventCompanyData company in ownedCompanies)
                    negotiationChoices.Add(new NegotiationChoice { choice = choices[0].createForCompany(company.companyName), originalIndex = 0, companyId = company.companyId, companyName = company.companyName });
                return;
            }
            for (int i = 0; i < choices.Count; i++)
                negotiationChoices.Add(new NegotiationChoice { choice = JsonUtility.FromJson<EventChoice>(JsonUtility.ToJson(choices[i])), originalIndex = i });
        }
        private void showCompanyChoices()
        {
            if (screenState != ScreenState.CompanyExchange) return;
            buildNegotiationChoices(true);
            if (negotiationChoices.Count == 0)
            {
                screenState = ScreenState.Result;
                closeResult();
                return;
            }
            screenState = ScreenState.CompanyChoice;
            negotiationView.showCompanyChoices(activeInfo, this);
        }
        public void confirmChoice(int choiceIndex)
        {
            if (EventSceneTransition.IsBusy) return;
            if ((screenState != ScreenState.Negotiation && screenState != ScreenState.CompanyChoice) || choiceIndex < 0 || choiceIndex >= negotiationChoices.Count) return;
            EventDefinition definition = activeInfo.definition;
            if (!hasRequiredHoldings(definition) || !meetsConditions(definition, activeInfo.companyId) || !isGreenmailOfferCurrent(definition, activeInfo.companyId))
            {
                negotiationView.hide(); activeInfo = null; negotiationChoices.Clear(); screenState = ScreenState.Closed;
                refreshAvailability();
                FindFirstObjectByType<FloatingMessage>()?.Show("제안 조건이나 기한이 변경되어 만남을 종료하였습니다.");
                return;
            }
            NegotiationChoice selected = negotiationChoices[choiceIndex];
            EventChoice choice = selected.choice;
            bool companyChoice = screenState == ScreenState.CompanyChoice;
            if (!companyChoice && definition.getChoiceGeneration() == EventChoiceGeneration.OwnedCompanies && selected.originalIndex == 0)
            {
                if (ActiveOpening.Count > 0) { ShowScriptedCompanyChoices(choice.getPlayerLine()); return; }
                screenState = ScreenState.CompanyExchange;
                negotiationView.showExchange(activeInfo.resolve(definition.getCompanyQuestion()),
                    activeInfo.resolve(choice.getPlayerLine()), showCompanyChoices);
                return;
            }
            // 선택 완료·관계·기억·현금 변경 전에 검사한다. 실패한 답변은 다시 고를 수 있다.
            bool succeeded = !definition.IsStory || meetsConditions(choice.getResultConditions(), activeInfo.companyId);
            EventOutcome outcome = succeeded ? choice : choice.getFailureOutcome();
            if (outcome == null || !canAfford(outcome))
            {
                negotiationView.showChoiceFailure("보유자금이 부족합니다. 다른 답변을 선택해 주십시오.");
                return;
            }
            PlayerMemoryService playerMemories = null;
            PlayerMemoryOrigin memoryOrigin = null;
            if (outcome.getPlayerMemories().Count > 0)
            {
                playerMemories = FindFirstObjectByType<PlayerMemoryService>();
                memoryOrigin = new PlayerMemoryOrigin(PlayerMemorySourceKind.EventChoice, definition.getId(),
                    $"day{currentDay}:{definition.getId()}", activeInfo.companyId, currentDay, selected.originalIndex);
                if (playerMemories == null)
                {
                    negotiationView.showChoiceFailure("플레이어 기억 저장소를 확인할 수 없습니다.");
                    return;
                }
                try { playerMemories.ValidateGrants(outcome.getPlayerMemories(), memoryOrigin); }
                catch (Exception error) when (error is ArgumentException || error is System.IO.InvalidDataException)
                {
                    negotiationView.showChoiceFailure("선택 결과의 플레이어 기억 설정이 올바르지 않습니다.");
                    return;
                }
            }
            string playerLine = companyChoice ? (definition.getCompanyPlayerLine() ?? "").Replace("{company}", selected.companyName) : choice.getPlayerLine();
            string companyId = companyChoice ? selected.companyId : activeInfo.companyId;
            screenState = ScreenState.Result;
            string repeatOccurrence = RecordRepeatChoice(definition, companyId, selected.originalIndex);
            selectedChoices[definition.getId()] = selected.originalIndex;
            companyChoices[(definition.getId(), companyId)] = selected.originalIndex;
            if (definition.IsTutorialOnly) tutorialVisitDay = currentDay;
            if (definition.IsStory)
            {
                storyResults[definition.getId()] = succeeded;
                storyResolvedDays[definition.getId()] = currentDay;
            }
            occurrences.RemoveAll(item => item.definition == definition);
            rebuildToday();
            var result = new ChoiceConfirmedInfo(definition.getId(), selected.originalIndex, outcome.getResultValue(),
                currentDay, outcome.getNpcResult(), outcome.getCashDelta(), companyId,
                selected.companyId, outcome.getStockSurgeMinRatio(), outcome.getStockSurgeMaxRatio(), outcome.getBuybackPremiumRatio(), repeatOccurrence);
            if (definition.hasCompanyConditions() && result.npcResult != null)
            {
                result.npcResult.targetNpcIds.RemoveAll(id => worldReader.belongsToOtherCompany(id, companyId));
                result.npcResult.targetReactions.RemoveAll(reaction => worldReader.belongsToOtherCompany(reaction.targetNpcId, companyId));
                if (result.npcResult.targetNpcIds.Count == 0) result.clearNpcResult();
            }
            var addedPlayerMemories = playerMemories?.Commit(outcome.getPlayerMemories(), memoryOrigin);
            choiceConfirmed?.Invoke(result);
            if (playerMemories != null) playerMemories.NotifyAdded(addedPlayerMemories);
            todayEventsChanged?.Invoke();
            if (ActiveOpening.Count > 0) ShowScriptedResult(outcome, playerLine, companyChoice ? selected.companyName : null);
            else negotiationView.showResult(activeInfo.resolve(outcome.getResultDialogue()), activeInfo.resolve(playerLine));
        }
        public void closeResult()
        {
            if (screenState != ScreenState.Result) return;
            if (EventSceneTransition.IsBusy && !EventSceneTransition.IsChangingScene) return;
            Action finish = () => {
                negotiationView.hide();
                activeInfo = null;
                screenState = ScreenState.Closed;
                CompleteMandatoryPresentation();
                TakeOver.SaveSystem.GameSessionCoordinator.Active?.SaveSettledState();
            };
            if (TakeOver.Story.StoryDirector.IsApplyingWholeSkip || EventSceneTransition.IsChangingScene) finish();
            else EventSceneTransition.Play(finish);
        }

        private bool canAfford(EventOutcome choice)
        {
            float delta = choice.getCashDelta();
            if (float.IsNaN(delta) || float.IsInfinity(delta)) return false;
            if (delta == 0) return true;
            var player = FindFirstObjectByType<Player>();
            return player != null && player.state != null && !double.IsNaN(player.state.cash)
                && !double.IsInfinity(player.state.cash) && (delta >= 0 || player.state.cash >= -delta);
        }
    }
}
