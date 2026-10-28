using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventNegotiationView : MonoBehaviour
    {
        [SerializeField] private Text speakerText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceTexts;
        [SerializeField] private Button closeButton;

        public void show(TodayEventInfo info, EventSystemController controller)
        {
            int choiceCount = controller.getNegotiationChoiceCount();
            speakerText.text = $"{info.speakerName} · {info.speakerRole}";
            dialogueText.text = info.dialogue;
            if (choiceCount > choiceButtons.Length)
                Debug.LogWarning($"Event {info.definition.getId()} has {choiceCount} choices but only {choiceButtons.Length} choice buttons.");
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int choiceIndex = i;
                bool hasChoice = i < choiceCount;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].gameObject.SetActive(hasChoice);
                if (!hasChoice)
                    continue;

                choiceTexts[i].text = controller.getNegotiationChoiceText(i);
                choiceButtons[i].onClick.AddListener(() => controller.confirmChoice(choiceIndex));
            }
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(controller.closeResult);
            closeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void showResult(string dialogue)
        {
            dialogueText.text = dialogue;
            foreach (Button button in choiceButtons)
                button.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(true);
        }

        public void hide() => gameObject.SetActive(false);
    }
}
