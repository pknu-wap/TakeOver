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

        public void show(EventDefinition definition, EventSystemController controller)
        {
            speakerText.text = $"{definition.getSpeakerName()} · {definition.getSpeakerRole()}";
            dialogueText.text = definition.getDialogue();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int choiceIndex = i;
                bool hasChoice = i < definition.getChoices().Count;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].gameObject.SetActive(hasChoice);
                if (!hasChoice)
                    continue;

                choiceTexts[i].text = definition.getChoices()[i].getText();
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
