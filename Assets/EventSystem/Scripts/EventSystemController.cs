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

        private readonly List<EventDefinition> todayEvents = new List<EventDefinition>();
        private readonly Dictionary<string, int> selectedChoices = new Dictionary<string, int>();
        private EventDefinition activeEvent;
        private int currentDay;
        private GameTurnClock turnClock;
        private ScreenState screenState;

        private enum ScreenState { Closed, Detail, Negotiation, Result }

        public event Action<NegotiationStartedInfo> negotiationStarted;
        public event Action<ChoiceConfirmedInfo> choiceConfirmed;
        public event Action todayEventsChanged;

        private void Awake()
        {
            turnClock = FindFirstObjectByType<GameTurnClock>();
            detailView.hide();
            negotiationView.hide();
        }

        private void OnEnable()
        {
            if (turnClock != null)
                turnClock.TurnAdvanced += onDayStarted;
        }

        private void OnDisable()
        {
            if (turnClock != null)
                turnClock.TurnAdvanced -= onDayStarted;
        }

        private void Start()
        {
            if (currentDay == 0)
                onDayStarted(turnClock != null ? turnClock.CurrentTurn : 1);
        }

        public IReadOnlyList<EventDefinition> getTodayEvents() => todayEvents.AsReadOnly();
        public int getCurrentDay() => currentDay;

        public bool tryGetChoice(string eventId, out int choiceIndex)
        {
            choiceIndex = -1;
            return eventId != null && selectedChoices.TryGetValue(eventId, out choiceIndex);
        }

        public void onDayStarted(int dayNumber)
        {
            if (dayNumber <= currentDay)
                return;

            currentDay = dayNumber;
            todayEvents.Clear();
            foreach (EventDefinition definition in events)
            {
                if (!selectedChoices.ContainsKey(definition.getId()))
                    todayEvents.Add(definition);
            }
            todayEventsChanged?.Invoke();
        }

        public void openEvent(string eventId)
        {
            if (screenState != ScreenState.Closed)
                return;

            activeEvent = todayEvents.Find(definition => definition.getId() == eventId);
            if (activeEvent == null)
                return;

            screenState = ScreenState.Detail;
            detailView.show(activeEvent, this);
        }

        public void closeDetail()
        {
            if (screenState != ScreenState.Detail)
                return;

            detailView.hide();
            activeEvent = null;
            screenState = ScreenState.Closed;
        }

        public void startNegotiation()
        {
            if (screenState != ScreenState.Detail)
                return;

            screenState = ScreenState.Negotiation;
            detailView.hide();
            negotiationStarted?.Invoke(new NegotiationStartedInfo(activeEvent.getId(), activeEvent.getApCost()));
            negotiationView.show(activeEvent, this);
        }

        public void confirmChoice(int choiceIndex)
        {
            if (screenState != ScreenState.Negotiation ||
                choiceIndex < 0 || choiceIndex >= activeEvent.getChoices().Count)
                return;

            EventChoice choice = activeEvent.getChoices()[choiceIndex];
            screenState = ScreenState.Result;
            selectedChoices.Add(activeEvent.getId(), choiceIndex);
            todayEvents.Remove(activeEvent);
            choiceConfirmed?.Invoke(new ChoiceConfirmedInfo(activeEvent.getId(), choiceIndex, choice.getResultValue(),
                currentDay, choice.getNpcResult(), choice.getCashDelta()));
            todayEventsChanged?.Invoke();
            negotiationView.showResult(choice.getResultDialogue());
        }

        public void closeResult()
        {
            if (screenState != ScreenState.Result)
                return;

            negotiationView.hide();
            activeEvent = null;
            screenState = ScreenState.Closed;
        }
    }
}
