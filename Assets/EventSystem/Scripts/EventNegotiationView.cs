using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventNegotiationView : MonoBehaviour
    {
        [SerializeField] private Text speakerText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private CanvasGroup questionBlock;
        [SerializeField] private RectTransform choicesRoot;
        [SerializeField] private Button choiceTemplate;
        [SerializeField] private CanvasGroup playerBlock;
        [SerializeField] private Text playerLineText;
        [SerializeField] private CanvasGroup responseBlock;
        [SerializeField] private Text responseSpeakerText;
        [SerializeField] private Text responseText;
        [SerializeField] private Image npcIllustration;
        [SerializeField] private Image playerIllustration;
        [SerializeField] private Sprite playerStandingIllustration;
        [SerializeField] private Sprite playerReplyIllustration;
        [SerializeField] private RectTransform playerInitialFrame;
        [SerializeField] private RectTransform playerReplyFrame;
        [SerializeField] private Button closeButton;
        private readonly List<Button> choices = new List<Button>();
        private Vector2 questionPosition;
        private bool choiceSubmitted;
        private Coroutine resultRoutine;

        private void Awake() => questionPosition = ((RectTransform)questionBlock.transform).anchoredPosition;

        public void show(TodayEventInfo info, EventSystemController controller, Sprite speakerIllustration = null)
        {
            stopResultRoutine();
            choiceSubmitted = false;
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            setBlock(questionBlock, true);
            setBlock(playerBlock, false);
            setBlock(responseBlock, false);
            speakerText.text = $"{info.speakerRole} {info.speakerName}".Trim();
            responseSpeakerText.text = speakerText.text;
            dialogueText.text = info.dialogue;
            npcIllustration.sprite = speakerIllustration;
            npcIllustration.gameObject.SetActive(speakerIllustration != null);
            playerIllustration.sprite = playerStandingIllustration;
            setPlayerFrame(playerInitialFrame);
            playerIllustration.gameObject.SetActive(playerStandingIllustration != null);
            playerIllustration.color = new Color(0.65f, 0.65f, 0.65f, 1);
            clearChoices();
            choicesRoot.gameObject.SetActive(true);
            choiceTemplate.gameObject.SetActive(false);
            for (int i = 0; i < controller.getNegotiationChoiceCount(); i++)
            {
                int choiceIndex = i;
                Button button = Instantiate(choiceTemplate, choicesRoot);
                button.name = $"Choice{choiceIndex}";
                Text title = button.transform.Find("Title").GetComponent<Text>();
                Text description = button.transform.Find("Description").GetComponent<Text>();
                string detail = info.resolve(controller.getNegotiationChoiceDescription(i));
                bool hasDescription = !string.IsNullOrWhiteSpace(detail);
                title.text = info.resolve(controller.getNegotiationChoiceText(i));
                title.rectTransform.anchorMin = new Vector2(0, hasDescription ? 0.44f : 0);
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.offsetMin = new Vector2(12, hasDescription ? 0 : 12);
                title.rectTransform.offsetMax = new Vector2(-12, -12);
                description.text = detail;
                description.gameObject.SetActive(hasDescription);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => submitChoice(controller, choiceIndex));
                button.interactable = true;
                button.gameObject.SetActive(true);
                choices.Add(button);
            }
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(controller.closeResult);
            closeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void submitChoice(EventSystemController controller, int choiceIndex)
        {
            if (choiceSubmitted) return;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            controller.confirmChoice(choiceIndex);
        }

        public void showResult(string dialogue, string playerLine = "")
        {
            stopResultRoutine();
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            choicesRoot.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
            playerLineText.text = playerLine;
            responseText.text = dialogue;
            resultRoutine = StartCoroutine(animateResult(!string.IsNullOrWhiteSpace(playerLine)));
        }

        private IEnumerator animateResult(bool hasPlayerLine)
        {
            RectTransform questionRect = (RectTransform)questionBlock.transform;
            for (float elapsed = 0; elapsed < 0.3f; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / 0.3f);
                questionBlock.alpha = 1 - progress;
                questionRect.anchoredPosition = questionPosition + Vector2.up * (40 * progress);
                yield return null;
            }
            setBlock(questionBlock, false);
            playerIllustration.sprite = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame);
            playerIllustration.color = Color.white;
            npcIllustration.color = new Color(0.65f, 0.65f, 0.65f, 1);
            if (hasPlayerLine) yield return fadeIn(playerBlock);
            yield return fadeIn(responseBlock);
            npcIllustration.color = Color.white;
            closeButton.gameObject.SetActive(true);
            resultRoutine = null;
        }

        private IEnumerator fadeIn(CanvasGroup block)
        {
            block.gameObject.SetActive(true);
            for (float elapsed = 0; elapsed < 0.15f; elapsed += Time.unscaledDeltaTime)
            {
                block.alpha = Mathf.Clamp01(elapsed / 0.15f);
                yield return null;
            }
            block.alpha = 1;
        }

        private void setPlayerFrame(RectTransform frame)
        {
            playerIllustration.transform.SetParent(frame, false);
            playerIllustration.rectTransform.anchorMin = Vector2.zero;
            playerIllustration.rectTransform.anchorMax = Vector2.one;
            playerIllustration.rectTransform.offsetMin = Vector2.zero;
            playerIllustration.rectTransform.offsetMax = Vector2.zero;
        }

        private void setBlock(CanvasGroup block, bool visible)
        {
            block.alpha = visible ? 1 : 0;
            block.interactable = false;
            block.blocksRaycasts = false;
            block.gameObject.SetActive(visible);
        }

        private void clearChoices()
        {
            foreach (Button button in choices)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            choices.Clear();
        }

        private void stopResultRoutine()
        {
            if (resultRoutine == null) return;
            StopCoroutine(resultRoutine);
            resultRoutine = null;
        }

        public void hide()
        {
            stopResultRoutine();
            gameObject.SetActive(false);
        }
    }
}
