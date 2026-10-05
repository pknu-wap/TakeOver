using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Events
{
    public sealed class EventSystemController : MonoBehaviour
    {
        [SerializeField] private List<EventDefinition> events = new List<EventDefinition>();
        [SerializeField] private EventDetailView detailView;
        [SerializeField] private EventNegotiationView negotiationView;
        private sealed class Occurrence
        {
            public EventDefinition definition;
            public string companyId;
            public int firstDay;
        }
        private readonly List<Occurrence> occurrences = new List<Occurrence>();
        private readonly List<EventDefinition> todayEvents = new List<EventDefinition>();
        private readonly List<TodayEventInfo> todayInfos = new List<TodayEventInfo>();
        private readonly Dictionary<string, int> selectedChoices = new Dictionary<string, int>();
        private readonly Dictionary<(string, string), int> companyChoices = new Dictionary<(string, string), int>();
        private readonly Dictionary<(string, string), int> availableDays = new Dictionary<(string, string), int>();
        private TodayEventInfo activeInfo;
        private int currentDay;
        private GameTurnClock turnClock;
        private EventWorldReader worldReader;
        private ScreenState screenState;
        private enum ScreenState { Closed, Detail, Negotiation, Result }
        public event Action<NegotiationStartedInfo> negotiationStarted;
        public event Action<ChoiceConfirmedInfo> choiceConfirmed;
        public event Action todayEventsChanged;

        private void Awake()
        {
            turnClock = FindFirstObjectByType<GameTurnClock>();
            worldReader = new EventWorldReader(GetComponent<EventTempWorldData>(), FindFirstObjectByType<NpcStateRegistry>());
            detailView.hide();
            negotiationView.hide();
        }
        private void OnEnable()
        {
            if (turnClock != null) turnClock.TurnAdvanced += onDayStarted;
        }
        private void OnDisable()
        {
            if (turnClock != null) turnClock.TurnAdvanced -= onDayStarted;
        }
        private void Start()
        {
            if (currentDay == 0) onDayStarted(turnClock != null ? turnClock.CurrentTurn : 1);
        }
        public IReadOnlyList<EventDefinition> getTodayEvents() => todayEvents.AsReadOnly();
        public IReadOnlyList<TodayEventInfo> getTodayEventInfos() => todayInfos.AsReadOnly();
        public int getCurrentDay() => currentDay;
        public bool tryGetChoice(string eventId, out int choiceIndex)
        {
            choiceIndex = -1;
            return eventId != null && selectedChoices.TryGetValue(eventId, out choiceIndex);
        }
        public bool tryGetChoice(string eventId, string companyId, out int choiceIndex) =>
            companyChoices.TryGetValue((eventId, companyId ?? ""), out choiceIndex);
        private string getKeyCompany(EventDefinition definition, string companyId) => definition.hasCompanyConditions() ? companyId : "";
        private bool canAppear(EventDefinition definition, string companyId)
        {
            var key = (definition.getId(), getKeyCompany(definition, companyId));
            return !(definition.hasCompanyConditions() ? companyChoices.ContainsKey(key) : selectedChoices.ContainsKey(definition.getId())) && (!availableDays.TryGetValue(key, out int day) || currentDay >= day);
        }
        private bool meetsConditions(EventDefinition definition, string companyId)
        {
            foreach (EventCondition condition in definition.getConditions())
            {
                float threshold = condition.value;
                if (!string.IsNullOrEmpty(condition.relaxedByEventId))
                {
                    foreach (var selected in companyChoices)
                        if (selected.Key.Item1 == condition.relaxedByEventId && selected.Value == condition.relaxedByChoiceIndex)
                            threshold = condition.relaxedValue;
                }
                float actual;
                if (condition.kind == EventConditionKind.CompanyStake) actual = worldReader.getStake(companyId);
                else if (condition.kind == EventConditionKind.CompanyConsumerReaction) actual = worldReader.getConsumerReaction(companyId);
                else
                {
                    NpcRelationState relation = worldReader.getNpcRelation(condition.npcId);
                    switch (condition.relationAxis)
                    {
                        case EventRelationAxis.trust: actual = relation.trust; break;
                        case EventRelationAxis.respect: actual = relation.respect; break;
                        case EventRelationAxis.fear: actual = relation.fear; break;
                        case EventRelationAxis.hostility: actual = relation.hostility; break;
                        case EventRelationAxis.dependency: actual = relation.dependency; break;
                        default: actual = relation.interest; break;
                    }
                }
                if (condition.comparison == EventComparison.AtLeast ? actual < threshold : actual > threshold) return false;
            }
            return true;
        }
        public void onDayStarted(int dayNumber)
        {
            if (dayNumber <= currentDay) return;
            currentDay = dayNumber;
            for (int i = occurrences.Count - 1; i >= 0; i--)
            {
                Occurrence occurrence = occurrences[i];
                int expiry = occurrence.firstDay + occurrence.definition.getValidDays();
                if (currentDay >= expiry)
                {
                    availableDays[(occurrence.definition.getId(), getKeyCompany(occurrence.definition, occurrence.companyId))] = expiry + 7;
                    occurrences.RemoveAt(i);
                }
                else if (!meetsConditions(occurrence.definition, occurrence.companyId)) occurrences.RemoveAt(i);
            }
            foreach (EventDefinition definition in events)
            {
                if (occurrences.Exists(item => item.definition == definition)) continue;
                string companyId = "";
                if (definition.hasCompanyConditions())
                {
                    var candidates = new List<string>();
                    foreach (EventCompanyData company in worldReader.getCompanies())
                        if (canAppear(definition, company.companyId) && meetsConditions(definition, company.companyId)) candidates.Add(company.companyId);
                    if (candidates.Count == 0) continue;
                    companyId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                }
                else
                {
                    if (!canAppear(definition, "") || !meetsConditions(definition, "")) continue;
                    if (definition.getCompanyIds().Count > 0) companyId = definition.getCompanyIds()[0];
                }
                occurrences.Add(new Occurrence { definition = definition, companyId = companyId, firstDay = currentDay });
            }
            rebuildToday();
            todayEventsChanged?.Invoke();
        }
        private void rebuildToday()
        {
            todayEvents.Clear();
            todayInfos.Clear();
            foreach (Occurrence occurrence in occurrences)
            {
                todayEvents.Add(occurrence.definition);
                todayInfos.Add(new TodayEventInfo(occurrence.definition, worldReader.getCompany(occurrence.companyId),
                    occurrence.firstDay + occurrence.definition.getValidDays() - currentDay));
            }
        }
        public void openEvent(string eventId)
        {
            if (screenState != ScreenState.Closed) return;
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
            if (screenState != ScreenState.Detail) return;
            screenState = ScreenState.Negotiation;
            detailView.hide();
            negotiationStarted?.Invoke(new NegotiationStartedInfo(activeInfo.definition.getId(), activeInfo.definition.getApCost()));
            negotiationView.show(activeInfo, this);
        }
        public void confirmChoice(int choiceIndex)
        {
            if (screenState != ScreenState.Negotiation || choiceIndex < 0 || choiceIndex >= activeInfo.definition.getChoices().Count) return;
            EventDefinition definition = activeInfo.definition;
            EventChoice choice = definition.getChoices()[choiceIndex];
            screenState = ScreenState.Result;
            selectedChoices[definition.getId()] = choiceIndex;
            companyChoices[(definition.getId(), activeInfo.companyId)] = choiceIndex;
            occurrences.RemoveAll(item => item.definition == definition);
            rebuildToday();
            var result = new ChoiceConfirmedInfo(definition.getId(), choiceIndex, choice.getResultValue(),
                currentDay, choice.getNpcResult(), choice.getCashDelta(), activeInfo.companyId);
            if (definition.hasCompanyConditions() && result.npcResult != null)
            {
                result.npcResult.targetNpcIds.RemoveAll(id => worldReader.belongsToOtherCompany(id, activeInfo.companyId));
                result.npcResult.targetReactions.RemoveAll(reaction => worldReader.belongsToOtherCompany(reaction.targetNpcId, activeInfo.companyId));
                if (result.npcResult.targetNpcIds.Count == 0) result.clearNpcResult();
            }
            choiceConfirmed?.Invoke(result);
            todayEventsChanged?.Invoke();
            negotiationView.showResult(activeInfo.resolve(choice.getResultDialogue()));
        }
        public void closeResult()
        {
            if (screenState != ScreenState.Result) return;
            negotiationView.hide();
            activeInfo = null;
            screenState = ScreenState.Closed;
        }
    }
}
