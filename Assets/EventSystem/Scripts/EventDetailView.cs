using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventDetailView : MonoBehaviour
    {
        [SerializeField] private RectTransform layoutRoot;
        [SerializeField] private GameObject fallbackPanel;
        [SerializeField] private GameObject artMask;
        [SerializeField] private Image eventImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text apCostText;
        [SerializeField] private Text startButtonText;
        [SerializeField] private FloatingMessage floatingMessage;
        [SerializeField] private Button backButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button closeButton;
        private bool warnedMissingMessage;

        public void show(TodayEventInfo info, EventSystemController controller)
        {
            if (hasFloatingMessage()) floatingMessage.Hide();
            EventDefinition definition = info.definition;
            eventImage.sprite = info.popupImage;
            bool hasArt = eventImage.sprite != null;
            eventImage.gameObject.SetActive(hasArt);
            fallbackPanel.SetActive(!hasArt);
            artMask.SetActive(!hasArt);
            highlightImage.sprite = definition.getHighlightImage();
            highlightImage.gameObject.SetActive(highlightImage.sprite != null);
            titleText.text = info.title;
            descriptionText.text = info.description;
            apCostText.text = $"소모 에너지 {definition.getApCost()}";
            startButtonText.text = definition.getStartButtonText();
            backButton.onClick.RemoveAllListeners();
            startButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(controller.closeDetail);
            startButton.onClick.AddListener(controller.startNegotiation);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(controller.closeDetail);
            gameObject.SetActive(true);
            resizeLayout();
            transform.SetAsLastSibling();
        }

        private void OnRectTransformDimensionsChange() => resizeLayout();

        private void resizeLayout()
        {
            if (layoutRoot == null) return;
            Rect rect = ((RectTransform)transform).rect;
            layoutRoot.localScale = Vector3.one * Mathf.Min(rect.width / 1920f, rect.height / 1080f);
        }

        public void showInsufficientEnergy()
        {
            if (hasFloatingMessage()) floatingMessage.Show("행동포인트가 부족합니다");
        }

        private bool hasFloatingMessage()
        {
            if (floatingMessage != null) return true;
            if (!warnedMissingMessage)
            {
                warnedMissingMessage = true;
                Debug.LogWarning("Event detail FloatingMessage is not assigned.", this);
            }
            return false;
        }

        private void OnDisable()
        {
            if (hasFloatingMessage()) floatingMessage.Hide();
        }

        public void hide() => gameObject.SetActive(false);
    }
}
