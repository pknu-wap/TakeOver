using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventDebugDashboard : MonoBehaviour
    {
        [SerializeField] private EventSystemController eventSystem;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private Button cardTemplate;
        [SerializeField] private Text dayText;
        [SerializeField] private Text emptyText;
        [SerializeField] private Text resultText;
        [SerializeField] private EventDefinition[] observedEvents;
        [SerializeField] private Button nextDayButton;

        private void OnEnable()
        {
            eventSystem.todayEventsChanged += redraw;
            nextDayButton.onClick.AddListener(nextDay);
            redraw();
        }

        private void OnDisable()
        {
            eventSystem.todayEventsChanged -= redraw;
            nextDayButton.onClick.RemoveListener(nextDay);
        }

        public void nextDay()
        {
            eventSystem.onDayStarted(eventSystem.getCurrentDay() + 1);
        }

        private void redraw()
        {
            foreach (Transform child in cardContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            foreach (EventDefinition definition in eventSystem.getTodayEvents())
            {
                Button card = Instantiate(cardTemplate, cardContainer);
                card.name = definition.getId();
                card.GetComponentInChildren<Text>(true).text = $"{definition.getTitle()}    ·    AP {definition.getApCost()}";
                card.onClick.AddListener(() => eventSystem.openEvent(definition.getId()));
                card.gameObject.SetActive(true);
            }

            dayText.text = $"{eventSystem.getCurrentDay()}일차";
            emptyText.gameObject.SetActive(eventSystem.getTodayEvents().Count == 0);
            resultText.text = "선택 기록";
            foreach (EventDefinition definition in observedEvents)
            {
                bool selected = eventSystem.tryGetChoice(definition.getId(), out int choiceIndex);
                resultText.text += $"\n{definition.getTitle()}: " +
                    (selected ? $"선택지 {choiceIndex} ({definition.getChoices()[choiceIndex].getText()})" : "아직 안 골랐음");
            }
        }
    }
}
