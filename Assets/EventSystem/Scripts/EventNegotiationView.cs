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
        [SerializeField] private Button choiceTemplate;
        [SerializeField] private CanvasGroup playerBlock;
        [SerializeField] private Text playerLineText;
        [SerializeField] private CanvasGroup responseBlock;
        [SerializeField] private Text responseSpeakerText;
        [SerializeField] private Text responseText;
        [SerializeField] private Image npcIllustration;
        [SerializeField] private Image playerIllustration;
        [SerializeField] private Image roomBackground;
        [SerializeField] private Sprite playerStandingIllustration;
        [SerializeField] private Sprite playerReplyIllustration;
        [SerializeField] private Sprite playerDimmedIllustration;
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
        private Sprite npcStandingIllustration;
        private Sprite npcDimmedIllustration;
        private Sprite currentPlayerIllustration;
        private Sprite defaultBackground;
        private EventSystemController controller;
        private Button skipButton;
        private Image skipCurtain;
        private Canvas skipCanvas;
        private Coroutine skipRoutine;
        private Action skipTarget;
        private bool skipping, completingSkip;

        private void Awake()
        {
            if (defaultBackground == null) defaultBackground = roomBackground.sprite;
            questionPosition = ((RectTransform)questionBlock.transform).anchoredPosition;
            responsePosition = ((RectTransform)responseBlock.transform).anchoredPosition;
            playerPosition = ((RectTransform)playerBlock.transform).anchoredPosition;
            ensureSkipButton();
        }

        public void show(TodayEventInfo info, EventSystemController controller, Sprite speakerIllustration = null,
            Sprite speakerDimmedIllustration = null)
        {
            cancelSkip();
            this.controller = controller;
            skipTarget = null;
            ensureSkipButton();
            stopResultRoutine();
            choiceSubmitted = false;
            if (defaultBackground == null) defaultBackground = roomBackground.sprite;
            Sprite background = info.definition.getNegotiationBackground();
            roomBackground.sprite = background != null ? background : defaultBackground;
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition;
            setBlock(questionBlock, true);
            setBlock(playerBlock, false);
            setBlock(responseBlock, false);
            speakerText.text = info.speakerLabel;
            responseSpeakerText.text = speakerText.text;
            dialogueText.text = info.dialogue;
            npcIllustration.sprite = speakerIllustration;
            npcStandingIllustration = speakerIllustration;
            npcDimmedIllustration = speakerDimmedIllustration;
            npcIllustration.gameObject.SetActive(speakerIllustration != null);
            playerIllustration.sprite = playerStandingIllustration;
            currentPlayerIllustration = playerStandingIllustration;
            setPlayerFrame(playerInitialFrame);
            playerIllustration.gameObject.SetActive(playerStandingIllustration != null);
            highlightPlayer(false);
            showChoices(info, controller);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(controller.closeResult);
            closeButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            refreshSkipButton();
        }

        public void showCompanyChoices(TodayEventInfo info, EventSystemController controller) => showChoices(info, controller);

        private void showChoices(TodayEventInfo info, EventSystemController controller)
        {
            skipTarget = null;
            choiceSubmitted = false;
            clearChoices();
            choicesRoot.gameObject.SetActive(true);
            choiceTemplate.gameObject.SetActive(false);
            int choiceCount = controller.getNegotiationChoiceCount();
            for (int i = 0; i < choiceCount; i++)
            {
                int choiceIndex = i;
                Button button = Instantiate(choiceTemplate, choicesRoot);
                button.name = $"Choice{choiceIndex}";
                RectTransform buttonRect = (RectTransform)button.transform;
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
                buttonRect.pivot = new Vector2(0, 1);
                buttonRect.anchoredPosition = new Vector2(1249, -(628 - (choiceCount - 1 - i) * 88)) * (2f / 3f);
                buttonRect.sizeDelta = new Vector2(454, 70) * (2f / 3f);
                Text title = button.transform.Find("Title").GetComponent<Text>();
                Text description = button.transform.Find("Description").GetComponent<Text>();
                string detail = info.resolve(controller.getNegotiationChoiceDescription(i));
                bool hasDescription = !string.IsNullOrWhiteSpace(detail);
                title.text = info.resolve(controller.getNegotiationChoiceText(i));
                title.rectTransform.anchorMin = new Vector2(0, 1);
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.pivot = new Vector2(0, 1);
                title.rectTransform.anchoredPosition = new Vector2(41, -9) * (2f / 3f);
                title.rectTransform.sizeDelta = new Vector2(-41, 25) * (2f / 3f);
                description.text = detail;
                description.gameObject.SetActive(hasDescription);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => submitChoice(controller, choiceIndex));
                button.interactable = true;
                button.gameObject.SetActive(true);
                choices.Add(button);
            }
            refreshSkipButton();
        }

        private void submitChoice(EventSystemController controller, int choiceIndex)
        {
            if (choiceSubmitted) return;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            controller.confirmChoice(choiceIndex);
        }

        public void showResult(string dialogue, string playerLine = "") =>
            beginExchange(dialogue, playerLine, () => closeButton.gameObject.SetActive(true), false);

        public void showExchange(string dialogue, string playerLine, Action completed) =>
            beginExchange(dialogue, playerLine, completed, true);

        private void beginExchange(string dialogue, string playerLine, Action completed, bool companyQuestion)
        {
            stopResultRoutine();
            // Only presentation is skipped. The controller still owns every choice and effect.
            skipTarget = companyQuestion ? (Action)(() =>
            {
                showCompanyQuestionImmediately(dialogue);
                completed?.Invoke();
            }) : controller.closeResult;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            choicesRoot.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
            resultRoutine = StartCoroutine(animateExchange(dialogue, playerLine, completed, companyQuestion));
            refreshSkipButton();
        }

        private IEnumerator animateExchange(string dialogue, string playerLine, Action completed, bool companyQuestion)
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
            currentPlayerIllustration = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame);
            if (hasPlayerLine)
            {
                highlightPlayer(true);
                yield return fadeIn(playerBlock);
                yield return new WaitForSecondsRealtime(responseDelay);
            }
            if (companyQuestion)
            {
                if (hasPlayerLine)
                {
                    for (float elapsed = 0; elapsed < 0.3f; elapsed += Time.unscaledDeltaTime)
                    {
                        fadeUp(playerBlock, playerPosition, Mathf.SmoothStep(0, 1, elapsed / 0.3f));
                        yield return null;
                    }
                    setBlock(playerBlock, false);
                }
                ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
                dialogueText.text = dialogue;
                highlightPlayer(false);
                yield return fadeIn(questionBlock);
            }
            else
            {
                highlightPlayer(false);
                yield return fadeIn(responseBlock);
            }
            resultRoutine = null;
            if (companyQuestion) skipTarget = null;
            completed?.Invoke();
            refreshSkipButton();
        }

        private void ensureSkipButton()
        {
            if (skipButton != null) return;
            var go = new GameObject("Skip to choice", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(56, 248); rect.sizeDelta = new Vector2(164, 42);
            go.GetComponent<Image>().color = new Color(.08f, .12f, .2f, .97f);
            skipButton = go.GetComponent<Button>();
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            skipButton.onClick.AddListener(SkipToNextChoice);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var text = label.GetComponent<Text>();
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = dialogueText.font; text.fontSize = 17; text.alignment = TextAnchor.MiddleCenter;
            text.text = "스킵  ▸▸"; text.color = new Color(.95f, .82f, .54f); text.raycastTarget = false;
            refreshSkipButton();
        }

        private void refreshSkipButton()
        {
            if (skipButton != null) skipButton.interactable = !skipping && skipTarget != null;
        }

        public void SkipToNextChoice()
        {
            if (!isActiveAndEnabled || skipping || skipTarget == null || controller == null) return;
            var target = skipTarget;
            skipTarget = null; skipping = true;
            refreshSkipButton();
            stopResultRoutine();
            closeButton.interactable = false;
            // The controller keeps the fade alive when the final target closes this view.
            skipRoutine = controller.StartCoroutine(skipToTarget(target));
        }

        private IEnumerator skipToTarget(Action target)
        {
            if (skipCurtain == null)
            {
                var canvasObject = new GameObject("Event skip blackout", typeof(Canvas), typeof(GraphicRaycaster));
                skipCanvas = canvasObject.GetComponent<Canvas>();
                skipCanvas.renderMode = RenderMode.ScreenSpaceOverlay; skipCanvas.sortingOrder = 32760;
                var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
                image.transform.SetParent(canvasObject.transform, false);
                skipCurtain = image.GetComponent<Image>();
                skipCurtain.rectTransform.anchorMin = Vector2.zero; skipCurtain.rectTransform.anchorMax = Vector2.one;
                skipCurtain.rectTransform.offsetMin = skipCurtain.rectTransform.offsetMax = Vector2.zero;
            }
            skipCanvas.gameObject.SetActive(true);
            yield return fadeSkipCurtain(0, 1);
            completingSkip = true;
            try { target(); }
            finally { completingSkip = false; }
            yield return fadeSkipCurtain(1, 0);
            skipCanvas.gameObject.SetActive(false);
            skipRoutine = null; skipping = false;
            if (closeButton != null) closeButton.interactable = true;
            refreshSkipButton();
        }

        private IEnumerator fadeSkipCurtain(float from, float to)
        {
            const float seconds = .35f;
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                skipCurtain.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / seconds)));
                yield return null;
            }
            skipCurtain.color = new Color(0, 0, 0, to);
        }

        private void showCompanyQuestionImmediately(string dialogue)
        {
            setBlock(playerBlock, false); setBlock(responseBlock, false);
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            dialogueText.text = dialogue;
            currentPlayerIllustration = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame); highlightPlayer(false);
            setBlock(questionBlock, true);
        }

        private void cancelSkip()
        {
            if (skipRoutine != null && controller != null) controller.StopCoroutine(skipRoutine);
            skipRoutine = null; skipping = false; skipTarget = null;
            if (skipCanvas != null) skipCanvas.gameObject.SetActive(false);
            if (closeButton != null) closeButton.interactable = true;
            refreshSkipButton();
        }

        private void OnDisable()
        {
            stopResultRoutine();
            if (!completingSkip) cancelSkip();
        }

        private void OnDestroy()
        {
            cancelSkip();
            if (skipCanvas != null) Destroy(skipCanvas.gameObject);
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
            playerIllustration.sprite = !highlighted && playerDimmedIllustration != null ?
                playerDimmedIllustration : currentPlayerIllustration;
            playerIllustration.color = highlighted || playerDimmedIllustration != null ? Color.white : dimmed;
            npcIllustration.sprite = highlighted && npcDimmedIllustration != null ?
                npcDimmedIllustration : npcStandingIllustration;
            npcIllustration.color = !highlighted || npcDimmedIllustration != null ? Color.white : dimmed;
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
