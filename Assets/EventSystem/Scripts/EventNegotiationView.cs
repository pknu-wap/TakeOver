using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class DialoguePresentation
    {
        public Sprite background, npcStanding, npcDimmed;
        public Color backgroundTint = Color.white;
        public string speaker, text;
        public bool player, narration, showPlayer = true, response;
        public IReadOnlyList<string> choices;
    }

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
        private bool initialized, dialogueMode, dialogueInput;
        private Action dialogueAdvance, dialogueSkip;
        private Func<bool> dialogueCanSkip;
        private Action<int> dialogueChoose;
        private DialoguePresentation presentation;
        private readonly List<string> pages = new List<string>();
        private int page;
        private Text currentDialogue, currentSpeaker;
        public Text CurrentDialogue => currentDialogue != null ? currentDialogue : dialogueText;
        public Text CurrentSpeaker => currentSpeaker != null ? currentSpeaker : speakerText;
        public Image PlayerImage => playerIllustration;
        public Image NpcImage => npcIllustration;
        public Button ContinueButton => closeButton;
        public Button SkipButton => skipButton;
        public bool HasMorePages => page + 1 < pages.Count;
        public int DialoguePageCount => pages.Count;
        public bool NarrationUnderlineVisible => questionBlock.transform.Find("Underline").gameObject.activeInHierarchy;

        private void Awake()
        {
            initialize();
        }

        private void initialize()
        {
            if (initialized) return;
            initialized = true;
            if (defaultBackground == null) defaultBackground = roomBackground.sprite;
            questionPosition = ((RectTransform)questionBlock.transform).anchoredPosition;
            responsePosition = ((RectTransform)responseBlock.transform).anchoredPosition;
            playerPosition = ((RectTransform)playerBlock.transform).anchoredPosition;
            configureBodyLayout(false);
            ensureSkipButton();
            AlignButtonCenters(closeButton, skipButton);
        }

        private void configureBodyLayout(bool narration)
        {
            // Both story and native events use the full usable width of the existing band.
            // Keep the two native exchange rows separate when both speakers are visible.
            setBodyRect(dialogueText, 307.33334f, -72.66667f, 96, 60);
            setBodyRect(responseText, 307.33334f, -72.66667f, 96, 60);
            var underline = (RectTransform)playerBlock.transform.Find("Underline");
            float rightEdge = underline.anchoredPosition.x + (1 - underline.pivot.x) * underline.rect.width;
            var playerRect = playerLineText.rectTransform;
            playerRect.anchorMin = playerRect.anchorMax = new Vector2(0, 1);
            playerRect.pivot = new Vector2(0, 1);
            playerRect.anchoredPosition = new Vector2(307.33334f, -140.66667f);
            playerRect.sizeDelta = new Vector2(rightEdge - 307.33334f, 60);
            playerLineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            questionBlock.transform.Find("Underline").gameObject.SetActive(!narration);
            if (narration) setBodyRect(dialogueText, 80, -36, 80, 156);
        }

        private static void setBodyRect(Text text, float left, float top, float right, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, top);
            rect.sizeDelta = new Vector2(-left - right, height);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public Button CreateActionButton(Transform parent, string name, string label, Action clicked)
        {
            var button = Instantiate(closeButton, parent, false);
            button.name = name;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
            button.GetComponentInChildren<Text>(true).text = label;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = true;
            button.gameObject.SetActive(true);
            return button;
        }

        public static void ApplySkipButtonStyle(Button button)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            Vector3 center = rect.TransformPoint(rect.rect.center);
            rect.pivot = new Vector2(.5f, .5f);
            rect.localRotation = Quaternion.Euler(0, 0, 180);
            rect.position = center;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        }

        private static void AlignButtonCenters(Button button, Button target)
        {
            var rect = (RectTransform)button.transform;
            var parent = (RectTransform)rect.parent;
            Vector3 targetCenter = target.transform.TransformPoint(((RectTransform)target.transform).rect.center);
            Vector3 targetPivot = parent.InverseTransformPoint(targetCenter) +
                Vector3.Scale(rect.rect.size, rect.pivot - new Vector2(.5f, .5f));
            Vector2 anchor = parent.rect.min + Vector2.Scale(parent.rect.size, rect.anchorMin);
            Vector2 position = rect.anchoredPosition;
            position.y = targetPivot.y - anchor.y;
            rect.anchoredPosition = position;
        }

        public GameObject CreateInformationPanel(Transform parent, out Text heading, out Text body)
        {
            var source = questionBlock.transform.parent.parent;
            var panel = Instantiate(source.gameObject, parent, false);
            panel.name = "Event style information";
            var content = panel.transform.Find("Content");
            content.Find("PlayerBlock").gameObject.SetActive(false);
            content.Find("ResponseBlock").gameObject.SetActive(false);
            var question = content.Find("QuestionBlock");
            question.gameObject.SetActive(true);
            question.GetComponent<CanvasGroup>().alpha = 1;
            question.GetComponent<CanvasGroup>().blocksRaycasts = false;
            heading = question.Find("Speaker").GetComponent<Text>();
            body = question.Find("Dialogue").GetComponent<Text>();
            heading.gameObject.SetActive(true);
            var rule = (RectTransform)question.Find("Underline");
            rule.gameObject.SetActive(true);
            placeInformationText(heading.rectTransform, 18, 16, 26);
            placeInformationText(rule, 18, 50, 1.3333334f);
            placeInformationText(body.rectTransform, 18, 66, 200);
            body.alignment = TextAnchor.UpperLeft;
            panel.SetActive(true);
            return panel;
        }

        private static void placeInformationText(RectTransform rect, float inset, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(inset, -top);
            rect.sizeDelta = new Vector2(-inset * 2, height);
        }

        public void ShowBackdrop(Sprite background, Color tint)
        {
            initialize(); cancelSkip(); stopResultRoutine();
            roomBackground.sprite = background != null ? background : defaultBackground;
            roomBackground.color = tint;
            questionBlock.transform.parent.parent.gameObject.SetActive(false);
            npcIllustration.gameObject.SetActive(false); playerIllustration.gameObject.SetActive(false);
            choicesRoot.gameObject.SetActive(false); closeButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        // Story and event dialogue share this serialized UI and the same choice layout.
        // Only content/callbacks differ; the event prefab geometry, fonts and art are not changed.
        public void ShowDialogue(DialoguePresentation line, Action advance, Action<int> choose, Action skip, Func<bool> canSkip)
        {
            initialize();
            cancelSkip(); stopResultRoutine(); clearChoices();
            dialogueMode = true; dialogueInput = false; controller = null;
            presentation = line; dialogueAdvance = advance; dialogueChoose = choose;
            dialogueSkip = skip; dialogueCanSkip = canSkip;
            configureBodyLayout(line.narration);
            questionBlock.transform.parent.parent.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
            roomBackground.sprite = line.background != null ? line.background : defaultBackground;
            roomBackground.color = line.backgroundTint;
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition;
            setBlock(questionBlock, !line.player && !line.response);
            setBlock(playerBlock, line.player);
            setBlock(responseBlock, line.response);
            currentDialogue = line.player ? playerLineText : line.response ? responseText : dialogueText;
            if (line.player)
            {
                foreach (var label in playerBlock.GetComponentsInChildren<Text>(true))
                    if (label != playerLineText) { currentSpeaker = label; break; }
            }
            else currentSpeaker = line.response ? responseSpeakerText : speakerText;
            currentSpeaker.text = line.speaker;
            currentSpeaker.gameObject.SetActive(!line.narration);
            currentDialogue.alignment = line.narration ? TextAnchor.MiddleCenter : line.player ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            npcStandingIllustration = line.npcStanding; npcDimmedIllustration = line.npcDimmed;
            currentPlayerIllustration = line.player ? playerReplyIllustration : playerStandingIllustration;
            setPlayerFrame(line.player ? playerReplyFrame : playerInitialFrame);
            highlightPlayer(line.player);
            playerIllustration.gameObject.SetActive(!line.narration && line.showPlayer && playerStandingIllustration != null);
            npcIllustration.gameObject.SetActive(!line.narration && line.npcStanding != null);
            choiceTemplate.gameObject.SetActive(false);
            closeButton.name = "Continue";
            closeButton.GetComponentInChildren<Text>(true).text = "계속";
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(advanceDialogue);
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            paginate(line.text ?? "");
            page = 0;
            showPage();
        }

        private void paginate(string text)
        {
            pages.Clear();
            var settings = currentDialogue.GetGenerationSettings(currentDialogue.rectTransform.rect.size);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float height = currentDialogue.rectTransform.rect.height * currentDialogue.pixelsPerUnit;
            while (text.Length > 0)
            {
                int low = 1, high = text.Length, fit = 1;
                while (low <= high)
                {
                    int middle = (low + high) / 2;
                    float preferred = currentDialogue.cachedTextGeneratorForLayout.GetPreferredHeight(text.Substring(0, middle), settings);
                    if (preferred <= height + .1f) { fit = middle; low = middle + 1; }
                    else high = middle - 1;
                }
                if (fit < text.Length)
                    for (int i = fit - 1; i >= fit / 2; i--)
                        if (char.IsWhiteSpace(text[i]) || text[i] == '.' || text[i] == '?' || text[i] == '!') { fit = i + 1; break; }
                pages.Add(text.Substring(0, fit).TrimEnd());
                text = text.Substring(fit).TrimStart();
            }
            if (pages.Count == 0) pages.Add("");
        }

        private void showPage()
        {
            currentDialogue.text = pages[page];
            clearChoices();
            bool showChoicesNow = !HasMorePages && presentation.choices != null && presentation.choices.Count > 0;
            choicesRoot.gameObject.SetActive(showChoicesNow);
            if (showChoicesNow)
                for (int i = 0; i < presentation.choices.Count; i++)
                {
                    int index = i;
                    createChoice(i, presentation.choices.Count, presentation.choices[i], "", () => dialogueChoose?.Invoke(index));
                }
            closeButton.gameObject.SetActive(!showChoicesNow);
            SetDialogueInput(dialogueInput);
            Canvas.ForceUpdateCanvases();
        }

        private void advanceDialogue()
        {
            if (!dialogueInput) return;
            if (HasMorePages) { page++; showPage(); }
            else dialogueAdvance?.Invoke();
        }

        public void SetDialogueInput(bool enabled)
        {
            dialogueInput = enabled;
            closeButton.interactable = enabled;
            foreach (var button in choices) button.interactable = enabled;
            refreshSkipButton();
        }

        public void show(TodayEventInfo info, EventSystemController controller, Sprite speakerIllustration = null,
            Sprite speakerDimmedIllustration = null)
        {
            initialize();
            dialogueMode = false;
            configureBodyLayout(false);
            questionBlock.transform.parent.parent.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
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
                createChoice(i, choiceCount, info.resolve(controller.getNegotiationChoiceText(i)),
                    info.resolve(controller.getNegotiationChoiceDescription(i)), () => submitChoice(controller, choiceIndex));
            }
            refreshSkipButton();
        }

        private void createChoice(int index, int count, string text, string detail, Action selected)
        {
            Button button = Instantiate(choiceTemplate, choicesRoot);
            button.name = $"Choice{index}";
            RectTransform buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
            buttonRect.pivot = new Vector2(0, 1);
            buttonRect.anchoredPosition = new Vector2(1249, -(628 - (count - 1 - index) * 88)) * (2f / 3f);
            buttonRect.sizeDelta = new Vector2(454, 70) * (2f / 3f);
            Text title = button.transform.Find("Title").GetComponent<Text>();
            Text description = button.transform.Find("Description").GetComponent<Text>();
            title.text = text;
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(0, 1);
            title.rectTransform.anchoredPosition = new Vector2(41, -9) * (2f / 3f);
            title.rectTransform.sizeDelta = new Vector2(-41, 25) * (2f / 3f);
            description.text = detail;
            description.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail));
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selected?.Invoke());
            button.interactable = true;
            button.gameObject.SetActive(true);
            choices.Add(button);
        }

        private void submitChoice(EventSystemController controller, int choiceIndex)
        {
            if (choiceSubmitted) return;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            controller.confirmChoice(choiceIndex);
        }

        public void showChoiceFailure(string message)
        {
            choiceSubmitted = false;
            foreach (Button button in choices) button.interactable = true;
            var notice = FindFirstObjectByType<FloatingMessage>();
            if (notice != null) notice.Show(message);
            else dialogueText.text = message;
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
            skipButton = CreateActionButton(transform, "Skip to choice", "스킵", SkipToNextChoice);
            var rect = (RectTransform)skipButton.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(56, 248); rect.sizeDelta = new Vector2(164, 42);
            ApplySkipButtonStyle(skipButton);
            refreshSkipButton();
        }

        private void refreshSkipButton()
        {
            if (skipButton != null) skipButton.interactable = dialogueMode ? dialogueInput && dialogueCanSkip != null && dialogueCanSkip() : !skipping && skipTarget != null;
        }

        public void SkipToNextChoice()
        {
            if (dialogueMode)
            {
                if (isActiveAndEnabled && dialogueInput && dialogueCanSkip != null && dialogueCanSkip()) dialogueSkip?.Invoke();
                return;
            }
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
