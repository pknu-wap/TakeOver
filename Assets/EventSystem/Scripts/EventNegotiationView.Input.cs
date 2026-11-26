using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed partial class EventNegotiationView
    {
        private Button dialogueSurface, logLink;
        private RectTransform advanceHint;
        private bool continueVisible;
        private int shownFrame, advancedFrame = -1;
        private DialogueLogWindow logWindow;
        private readonly System.Collections.Generic.List<string> dialogueHistory = new System.Collections.Generic.List<string>();
        private object logSceneKey;
        public string DialogueLogText => string.Join("\n\n", dialogueHistory);
        public System.Collections.Generic.IReadOnlyList<string> DialogueLogEntries => dialogueHistory;

        public void ClearDialogueHistory()
        {
            dialogueHistory.Clear(); logSceneKey = null; lastDialogueNpcId = null;
            if (logWindow != null) Destroy(logWindow.gameObject);
            logWindow = null;
        }

        private void RecordDialogue(string speaker, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                dialogueHistory.Add(string.IsNullOrEmpty(speaker) ? text : speaker + "\n" + text);
        }
        public bool CanAdvanceDialogue => continueVisible && closeButton.interactable &&
            (!dialogueMode || dialogueInput) && !skipping && !EventSceneTransition.IsBusy &&
            logWindow == null && isActiveAndEnabled && Time.timeScale > 0 && CurrentLineVisible() && Time.frameCount > shownFrame;

        private bool CurrentLineVisible()
        {
            if (currentDialogue == null || !currentDialogue.gameObject.activeInHierarchy) return false;
            var block = currentDialogue.GetComponentInParent<CanvasGroup>();
            return block == null || block.alpha >= .99f;
        }

        private void EnsureDialogueControls()
        {
            if (dialogueSurface != null) return;
            foreach (string name in new[] { "Dialogue click area", "Dialogue advance hint", "Dialogue Log", "Dialogue log window" })
            {
                var copied = transform.Find(name);
                if (copied == null) continue;
                copied.gameObject.SetActive(false); Destroy(copied.gameObject);
            }
            var surface = new GameObject("Dialogue click area", typeof(RectTransform), typeof(Image), typeof(Button));
            surface.transform.SetParent(transform, false);
            surface.GetComponent<Image>().color = Color.clear;
            dialogueSurface = surface.GetComponent<Button>();
            dialogueSurface.transition = Selectable.Transition.None;
            dialogueSurface.navigation = new Navigation { mode = Navigation.Mode.None };
            dialogueSurface.onClick.AddListener(RequestDialogueAdvance);
            var hint = CopyDialogueLabel("Dialogue advance hint", "▼  (space)");
            hint.fontSize = 17; hint.alignment = TextAnchor.MiddleRight;
            advanceHint = hint.rectTransform;
            var label = CopyDialogueLabel("Dialogue Log", "Log");
            label.fontSize = 19; label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = true;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(1, -1);
            logLink = label.gameObject.AddComponent<Button>();
            logLink.targetGraphic = label;
            logLink.navigation = new Navigation { mode = Navigation.Mode.None };
            logLink.onClick.AddListener(OpenDialogueLog);
        }

        private Text CopyDialogueLabel(string name, string value)
        {
            var label = Instantiate(closeButton.GetComponentInChildren<Text>(true), transform, false);
            label.name = name; label.text = value; label.gameObject.SetActive(true);
            label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void SetContinueVisible(bool visible)
        {
            continueVisible = visible;
            closeButton.gameObject.SetActive(false);
            shownFrame = Time.frameCount;
        }

        public void RequestDialogueAdvance()
        {
            if (!CanAdvanceDialogue || advancedFrame == Time.frameCount || !DialogueCanvasAllowsInput()) return;
            advancedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            closeButton.onClick.Invoke();
        }

        private bool DialogueCanvasAllowsInput()
        {
            foreach (var group in GetComponentsInParent<CanvasGroup>())
                if (!group.interactable || !group.blocksRaycasts || group.alpha < .99f) return false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected == null || (selected.GetComponent<InputField>() == null && selected.GetComponent<TMPro.TMP_InputField>() == null);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                RequestDialogueAdvance();
        }

        private void UpdateDialogueControls()
        {
            if (dialogueSurface == null) return;
            var band = (RectTransform)questionBlock.transform.parent.parent;
            bool visible = band.gameObject.activeInHierarchy;
            dialogueSurface.gameObject.SetActive(visible && continueVisible);
            advanceHint.gameObject.SetActive(visible && CanAdvanceDialogue);
            logLink.gameObject.SetActive(visible);
            if (!visible) return;
            var corners = new Vector3[4]; band.GetWorldCorners(corners);
            var left = (Vector2)transform.InverseTransformPoint(corners[0]);
            var right = (Vector2)transform.InverseTransformPoint(corners[2]);
            PlaceControl((RectTransform)dialogueSurface.transform, (left + right) / 2, right - left);
            PlaceControl(advanceHint, new Vector2(right.x - 100, left.y + 24 + Mathf.Sin(Time.unscaledTime * 3.5f) * 3), new Vector2(150, 28));
            PlaceControl((RectTransform)logLink.transform, left + new Vector2(55, 24), new Vector2(66, 32));
            dialogueSurface.interactable = CanAdvanceDialogue;
            logLink.interactable = !EventSceneTransition.IsBusy && logWindow == null &&
                (dialogueMode ? dialogueInput : continueVisible && CurrentLineVisible() || choicesRoot.gameObject.activeSelf);
            // Controls are siblings of the band so information-panel copies stay clean.
            dialogueSurface.transform.SetAsLastSibling();
            advanceHint.SetAsLastSibling(); logLink.transform.SetAsLastSibling();
            if (logWindow != null) logWindow.transform.SetAsLastSibling();
        }

        private static void PlaceControl(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        public void OpenDialogueLog()
        {
            if (logWindow != null || EventSceneTransition.IsBusy || Time.timeScale <= 0) return;
            logWindow = DialogueLogWindow.Show(this);
        }

        private void RecordCurrentLine()
        {
            string speaker = currentDialogue == playerLineText ? ProtagonistName() :
                currentDialogue == responseText ? responseSpeakerText.text : speakerText.text;
            if (dialogueMode && presentation != null && presentation.narration) speaker = "";
            RecordDialogue(speaker, currentDialogue.text);
        }
    }
}
