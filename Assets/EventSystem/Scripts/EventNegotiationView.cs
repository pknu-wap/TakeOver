using System;
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
        [SerializeField] private RectTransform companyChoicesRoot;
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
        [SerializeField, Min(0)] private float responseDelay = 0.8f;
        private readonly List<Button> choices = new List<Button>();
        private Vector2 questionPosition;
        private Vector2 responsePosition;
        private Vector2 playerPosition;
        private bool choiceSubmitted;
        private Coroutine resultRoutine;

        private void Awake()
        {
            questionPosition = ((RectTransform)questionBlock.transform).anchoredPosition;
            responsePosition = ((RectTransform)responseBlock.transform).anchoredPosition;
            playerPosition = ((RectTransform)playerBlock.transform).anchoredPosition;
        }

        public void show(TodayEventInfo info, EventSystemController controller, Sprite speakerIllustration = null)
        {
            stopResultRoutine();
            choiceSubmitted = false;
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition;
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
            highlightPlayer(false);
            showChoices(info, controller, false);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(controller.closeResult);
            closeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void showCompanyChoices(TodayEventInfo info, EventSystemController controller) => showChoices(info, controller, true);

        private void showChoices(TodayEventInfo info, EventSystemController controller, bool companies)
        {
            choiceSubmitted = false;
            clearChoices();
            choicesRoot.gameObject.SetActive(!companies);
            companyChoicesRoot.gameObject.SetActive(companies);
            RectTransform container = companies ? companyChoicesRoot : choicesRoot;
            choiceTemplate.gameObject.SetActive(false);
            for (int i = 0; i < controller.getNegotiationChoiceCount(); i++)
            {
                int choiceIndex = i;
                Button button = Instantiate(choiceTemplate, container);
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
        }

        private void submitChoice(EventSystemController controller, int choiceIndex)
        {
            if (choiceSubmitted) return;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            controller.confirmChoice(choiceIndex);
        }

        public void showResult(string dialogue, string playerLine = "") =>
            showExchange(dialogue, playerLine, () => closeButton.gameObject.SetActive(true));

        public void showExchange(string dialogue, string playerLine, Action completed)
        {
            stopResultRoutine();
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            choicesRoot.gameObject.SetActive(false);
            companyChoicesRoot.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
            resultRoutine = StartCoroutine(animateExchange(dialogue, playerLine, completed));
        }

        private IEnumerator animateExchange(string dialogue, string playerLine, Action completed)
        {
            Vector2 previousResponsePosition = ((RectTransform)responseBlock.transform).anchoredPosition;
            for (float elapsed = 0; elapsed < 0.3f; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / 0.3f);
                fadeUp(questionBlock, questionPosition, progress);
                fadeUp(playerBlock, playerPosition, progress);
                fadeUp(responseBlock, previousResponsePosition, progress);
                yield return null;
            }
            setBlock(questionBlock, false);
            setBlock(playerBlock, false);
            setBlock(responseBlock, false);
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            playerLineText.text = playerLine;
            responseText.text = dialogue;
            bool hasPlayerLine = !string.IsNullOrWhiteSpace(playerLine);
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition + (hasPlayerLine ? Vector2.zero :
                Vector2.up * (speakerText.rectTransform.localPosition.y - responseSpeakerText.rectTransform.localPosition.y));
            playerIllustration.sprite = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame);
            if (hasPlayerLine)
            {
                highlightPlayer(true);
                yield return fadeIn(playerBlock);
                yield return new WaitForSecondsRealtime(responseDelay);
            }
            highlightPlayer(false);
            yield return fadeIn(responseBlock);
            resultRoutine = null;
            completed?.Invoke();
        }

        private void fadeUp(CanvasGroup block, Vector2 position, float progress)
        {
            if (!block.gameObject.activeSelf) return;
            block.alpha = 1 - progress;
            ((RectTransform)block.transform).anchoredPosition = position + Vector2.up * (40 * progress);
        }

        private void highlightPlayer(bool highlighted)
        {
            Color dimmed = new Color(0.65f, 0.65f, 0.65f, 1);
            playerIllustration.color = highlighted ? Color.white : dimmed;
            npcIllustration.color = highlighted ? dimmed : Color.white;
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
