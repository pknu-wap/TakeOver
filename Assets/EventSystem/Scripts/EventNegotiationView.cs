using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public IReadOnlyList<string> choiceDescriptions;
        public object sceneKey;
        public string npcId;
        public bool allowNpcTransition = true;
    }

    public sealed partial class EventNegotiationView : MonoBehaviour
    {
        internal static readonly Vector2 LayoutSize = new Vector2(1280, 720);
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
        private Action skipTarget;
        private bool skipping, completingSkip, fadeDialogueSkip;
        private bool initialized, dialogueMode, dialogueInput;
        private bool playerNameMeasured;
        private float playerNameWidth, playerNameRight, playerRuleWidth, playerRuleRight;
        private readonly Dictionary<Text, Vector4> npcNameLayouts = new Dictionary<Text, Vector4>();
        private Action dialogueAdvance, dialogueSkip;
        private Func<bool> dialogueCanSkip;
        private Action<int> dialogueChoose;
        private DialoguePresentation presentation;
        private string lastDialogueNpcId;
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
            FitLayout();
            if (initialized) return;
            initialized = true;
            if (defaultBackground == null) defaultBackground = roomBackground.sprite;
            questionPosition = ((RectTransform)questionBlock.transform).anchoredPosition;
            responsePosition = ((RectTransform)responseBlock.transform).anchoredPosition;
            playerPosition = ((RectTransform)playerBlock.transform).anchoredPosition;
            configureBodyLayout(false);
            ensureSkipButton();
            AlignButtonCenters(closeButton, skipButton);
            EnsureDialogueControls();
            closeButton.gameObject.SetActive(false);
        }

        internal static float LayoutScale(Vector2 viewport) =>
            Mathf.Min(1, Mathf.Min(viewport.x / LayoutSize.x, viewport.y / LayoutSize.y));

        private void LateUpdate() { FitLayout(); UpdateDialogueControls(); }

        private void FitLayout()
        {
            var rect = (RectTransform)transform;
            if (!(rect.parent is RectTransform parent)) return;
            Vector2 viewport = parent.rect.size;
            float scale = LayoutScale(viewport);
            if (scale <= 0) return;

            // Keep the authored control positions inside the viewport while the stage still fills it.
            Vector2 extraSize = viewport / scale - viewport;
            if (rect.sizeDelta != extraSize) rect.sizeDelta = extraSize;
            if (rect.localScale.x != scale) rect.localScale = new Vector3(scale, scale, 1);
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

        public Button CreateActionButton(Transform parent, string name, string label, Action clicked, bool useInformationStyle = true)
        {
            var button = Instantiate(closeButton, parent, false);
            button.name = name;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
            button.GetComponentInChildren<Text>(true).text = label;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = true;
            button.gameObject.SetActive(true);
            if (useInformationStyle)
                FindFirstObjectByType<EventDetailView>(FindObjectsInactive.Include)?.CopyActionStyle(button.targetGraphic as Image);
            return button;
        }

        internal static string ProtagonistName() => FindFirstObjectByType<Player>()?.ProtagonistName ?? Player.DefaultName;

        private Text PlayerSpeaker => playerBlock.GetComponentsInChildren<Text>(true)
            .First(label => label != playerLineText);

        private void FitPlayerName()
        {
            var label = PlayerSpeaker;
            var rect = label.rectTransform;
            var rule = (RectTransform)playerBlock.transform.Find("Underline");
            if (!playerNameMeasured)
            {
                playerNameWidth = rect.rect.width; playerRuleWidth = rule.rect.width;
                playerNameRight = rect.anchoredPosition.x + (1 - rect.pivot.x) * playerNameWidth;
                playerRuleRight = rule.anchoredPosition.x + (1 - rule.pivot.x) * playerRuleWidth;
                playerNameMeasured = true;
            }
            float width = Mathf.Clamp(label.preferredWidth + 8, playerNameWidth, 520);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.anchoredPosition = new Vector2(playerNameRight - (1 - rect.pivot.x) * width, rect.anchoredPosition.y);
            float lineWidth = Mathf.Max(playerRuleWidth, width);
            rule.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lineWidth);
            rule.anchoredPosition = new Vector2(playerRuleRight - (1 - rule.pivot.x) * lineWidth, rule.anchoredPosition.y);
        }

        private void FitNpcName(Text label, Text body)
        {
            var rect = label.rectTransform;
            var rule = label.transform.parent.Find("Underline") as RectTransform;
            if (!npcNameLayouts.TryGetValue(label, out var original))
            {
                original = new Vector4(rect.rect.width, rect.anchoredPosition.x - rect.pivot.x * rect.rect.width,
                    rule != null ? rule.rect.width : 0,
                    rule != null ? rule.anchoredPosition.x - rule.pivot.x * rule.rect.width : 0);
                npcNameLayouts.Add(label, original);
            }
            float width = Mathf.Clamp(label.preferredWidth + 8, original.x, Mathf.Max(original.x, body.rectTransform.rect.width));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.anchoredPosition = new Vector2(original.y + rect.pivot.x * width, rect.anchoredPosition.y);
            if (rule != null)
            {
                float ruleWidth = Mathf.Max(original.z, width);
                rule.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ruleWidth);
                rule.anchoredPosition = new Vector2(original.w + rule.pivot.x * ruleWidth, rule.anchoredPosition.y);
            }
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
            Vector2 position = rect.anchoredPosition;
            position.x += (rect.anchorMin.x - 1) * LayoutSize.x;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            position.y = targetPivot.y - parent.rect.yMin;
            rect.anchoredPosition = position;
        }

        public GameObject CreateInformationPanel(Transform parent, out Text heading, out Text body)
        {
            var source = questionBlock.transform.parent.parent;
            var panel = Instantiate(source.gameObject, parent, false);
            panel.name = "Event style information";
            FindFirstObjectByType<EventDetailView>(FindObjectsInactive.Include)?.CopyInformationPanelStyle(panel.GetComponent<Image>());
            var content = panel.transform.Find("Content");
            content.Find("PlayerBlock").gameObject.SetActive(false);
            content.Find("ResponseBlock").gameObject.SetActive(false);
            var question = (RectTransform)content.Find("QuestionBlock");
            question.gameObject.SetActive(true);
            question.anchorMin = Vector2.zero;
            question.anchorMax = Vector2.one;
            question.offsetMin = question.offsetMax = Vector2.zero;
            question.GetComponent<CanvasGroup>().alpha = 1;
            question.GetComponent<CanvasGroup>().blocksRaycasts = false;
            heading = question.Find("Speaker").GetComponent<Text>();
            body = question.Find("Dialogue").GetComponent<Text>();
            heading.gameObject.SetActive(true);
            var rule = (RectTransform)question.Find("Underline");
            rule.gameObject.SetActive(true);
            placeInformationText(heading.rectTransform, 18, 22, 26);
            placeInformationText(rule, 18, 53, 1.3333334f);
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
            ClearDialogueHistory();
            roomBackground.sprite = background != null ? background : defaultBackground;
            roomBackground.color = tint;
            questionBlock.transform.parent.parent.gameObject.SetActive(false);
            npcIllustration.gameObject.SetActive(false); playerIllustration.gameObject.SetActive(false);
            choicesRoot.gameObject.SetActive(false); SetContinueVisible(false);
            skipButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        // Story and event dialogue share this serialized UI and the same choice layout.
        // Only content/callbacks differ; the event prefab geometry, fonts and art are not changed.
        public void ShowDialogue(DialoguePresentation line, Action advance, Action<int> choose, Action skip, Func<bool> canSkip,
            bool fadeSkip = false)
        {
            initialize();
            bool npcChanged = line.allowNpcTransition && isActiveAndEnabled && dialogueMode &&
                Equals(logSceneKey, line.sceneKey) && presentation != null && presentation.background == line.background &&
                !string.IsNullOrEmpty(lastDialogueNpcId) && !string.IsNullOrEmpty(line.npcId) &&
                lastDialogueNpcId != line.npcId;
            if (npcChanged && !EventSceneTransition.IsBusy)
            {
                SetDialogueInput(false);
                EventSceneTransition.Play(() =>
                {
                    if (this == null || !isActiveAndEnabled) return;
                    bool input = dialogueInput;
                    PresentDialogue(line, advance, choose, skip, canSkip, fadeSkip);
                    SetDialogueInput(input);
                }, seconds: .12f);
                return;
            }
            PresentDialogue(line, advance, choose, skip, canSkip, fadeSkip);
        }

        private void PresentDialogue(DialoguePresentation line, Action advance, Action<int> choose, Action skip, Func<bool> canSkip,
            bool fadeSkip)
        {
            initialize();
            cancelSkip(); stopResultRoutine(); clearChoices();
            if (!dialogueMode || !Equals(logSceneKey, line.sceneKey) ||
                presentation != null && presentation.background != line.background)
                ClearDialogueHistory();
            logSceneKey = line.sceneKey;
            if (!string.IsNullOrEmpty(line.npcId)) lastDialogueNpcId = line.npcId;
            dialogueMode = true; dialogueInput = false; controller = null;
            presentation = line; dialogueAdvance = advance; dialogueChoose = choose;
            dialogueSkip = skip; dialogueCanSkip = canSkip;
            fadeDialogueSkip = fadeSkip;
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
            if (line.player) currentSpeaker = PlayerSpeaker;
            else currentSpeaker = line.response ? responseSpeakerText : speakerText;
            if (line.player) currentSpeaker.supportRichText = false;
            currentSpeaker.text = line.speaker;
            if (line.player) FitPlayerName();
            else if (!line.narration) FitNpcName(currentSpeaker, currentDialogue);
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
            text = breakLongDialogueSentences(text);
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
                {
                    int sentenceBreak = text.LastIndexOf('\n', fit - 1, fit);
                    if (sentenceBreak > 0) fit = sentenceBreak + 1;
                    else
                        for (int i = fit - 1; i >= fit / 2; i--)
                            if (char.IsWhiteSpace(text[i]) || text[i] == '.' || text[i] == '?' || text[i] == '!') { fit = i + 1; break; }
                }
                pages.Add(text.Substring(0, fit).TrimEnd());
                text = text.Substring(fit).TrimStart();
            }
            if (pages.Count == 0) pages.Add("");
        }

        private string breakLongDialogueSentences(string text)
        {
            var settings = currentDialogue.GetGenerationSettings(currentDialogue.rectTransform.rect.size);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float width = currentDialogue.rectTransform.rect.width * currentDialogue.pixelsPerUnit;
            var paragraphs = text.Replace("\r\n", "\n").Split('\n');
            const int longParagraphLength = 64;
            for (int i = 0; i < paragraphs.Length; i++)
            {
                if (paragraphs[i].Length < longParagraphLength &&
                    currentDialogue.cachedTextGeneratorForLayout.GetPreferredWidth(paragraphs[i], settings) <= width)
                    continue;
                // Keep authored paragraphs and short lines; long dialogue starts each sentence on a new line.
                paragraphs[i] = System.Text.RegularExpressions.Regex.Replace(paragraphs[i],
                    @"(?<=[.!?。！？])[ \t]+(?=\S)", "\n");
            }
            return string.Join("\n", paragraphs);
        }

        private void showPage()
        {
            currentDialogue.text = pages[page];
            RecordCurrentLine();
            clearChoices();
            bool showChoicesNow = !HasMorePages && presentation.choices != null && presentation.choices.Count > 0;
            choicesRoot.gameObject.SetActive(showChoicesNow);
            if (showChoicesNow)
                for (int i = 0; i < presentation.choices.Count; i++)
                {
                    int index = i;
                    createChoice(i, presentation.choices.Count, presentation.choices[i], presentation.choiceDescriptions != null && i < presentation.choiceDescriptions.Count ? presentation.choiceDescriptions[i] : "", () => dialogueChoose?.Invoke(index));
                }
            SetContinueVisible(!showChoicesNow);
            SetDialogueInput(dialogueInput);
            Canvas.ForceUpdateCanvases();
        }

        private void advanceDialogue()
        {
            if (!dialogueInput || EventSceneTransition.IsBusy || (!HasMorePages && presentation.choices != null && presentation.choices.Count > 0)) return;
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

        private void createChoice(int index, int count, string text, string detail, Action selected)
        {
            Button button = Instantiate(choiceTemplate, choicesRoot);
            button.name = $"Choice{index}";
            RectTransform buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1, 0);
            buttonRect.pivot = new Vector2(0, 1);
            buttonRect.anchoredPosition = new Vector2(1249 * (2f / 3f) - LayoutSize.x, LayoutSize.y - (628 - (count - 1 - index) * 88) * (2f / 3f));
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
            description.rectTransform.sizeDelta = new Vector2(-33f, 23f);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { RecordDialogue(ProtagonistName(), text); selected?.Invoke(); });
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = true;
            button.gameObject.SetActive(true);
            choices.Add(button);
        }

        private void ensureSkipButton()
        {
            if (skipButton != null) return;
            const string name = "Skip to choice";
            skipButton = transform.Find(name)?.GetComponent<Button>();
            if (skipButton == null) skipButton = CreateActionButton(transform, name, "스킵", SkipToNextChoice, useInformationStyle: false);
            else
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipToNextChoice);
                refreshSkipButton();
                return;
            }
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
            if (EventSceneTransition.IsBusy) return;
            if (dialogueMode)
            {
                if (isActiveAndEnabled && dialogueInput && dialogueCanSkip != null && dialogueCanSkip())
                {
                    if (fadeDialogueSkip)
                    {
                        var dialogueTarget = dialogueSkip;
                        EventSceneTransition.Play(() => { if (isActiveAndEnabled) dialogueTarget?.Invoke(); });
                    }
                    else dialogueSkip?.Invoke();
                }
                return;
            }
            if (!isActiveAndEnabled || skipping || skipTarget == null || controller == null) return;
            var target = skipTarget;
            skipTarget = null; skipping = true;
            refreshSkipButton();
            stopResultRoutine();
            closeButton.interactable = false;
            // The controller keeps the fade alive when the final target closes this view.
            if (!EventSceneTransition.Play(() =>
            {
                if (!isActiveAndEnabled) return;
                completingSkip = true;
                try { target(); }
                finally { completingSkip = false; }
            }, completed: finishSkip))
            {
                skipping = false;
                refreshSkipButton();
            }
        }

        private void finishSkip()
        {
            skipping = false;
            skipTarget = null;
            if (closeButton != null) closeButton.interactable = true;
            refreshSkipButton();
        }

        private void cancelSkip()
        {
            skipping = false; skipTarget = null;
            if (closeButton != null) closeButton.interactable = true;
            refreshSkipButton();
        }

        private void OnDisable()
        {
            stopResultRoutine();
            ClearDialogueHistory();
            if (!completingSkip) cancelSkip();
        }

        private void OnDestroy()
        {
            cancelSkip();
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
