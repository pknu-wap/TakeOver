using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventDetailView : MonoBehaviour
    {
        [SerializeField] private Image eventImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text apCostText;
        [SerializeField] private Text startButtonText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button startButton;

        public void show(TodayEventInfo info, EventSystemController controller)
        {
            EventDefinition definition = info.definition;
            eventImage.sprite = definition.getImage();
            titleText.text = info.title;
            descriptionText.text = info.description;
            apCostText.text = $"소모 AP  {definition.getApCost()}";
            startButtonText.text = definition.getStartButtonText();
            backButton.onClick.RemoveAllListeners();
            startButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(controller.closeDetail);
            startButton.onClick.AddListener(controller.startNegotiation);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void hide() => gameObject.SetActive(false);
    }
}
