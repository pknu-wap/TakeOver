using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace TakeOver.Operations
{
    public sealed class DocumentDropdownOption
    {
        public string label;
        public bool enabled = true;
        public Action selected;
    }

    public sealed class DocumentDropdownMenu : MonoBehaviour
    {
        private const float boxWidth = 142f;
        private const float headerHeight = 32f;
        private const float optionHeight = 36f;
        private static readonly Color normalText = new Color32(96, 96, 96, 255);
        private static readonly Color disabledText = new Color32(186, 186, 186, 255);
        private static readonly Color separatorColor = new Color32(201, 201, 201, 255);

        private readonly List<GameObject> entries = new List<GameObject>();
        private Image background;

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            background = GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color32(216, 216, 216, 255);
            background.raycastTarget = false;
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            foreach (LayoutGroup group in GetComponents<LayoutGroup>()) group.enabled = false;
            ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        public void Show(DocumentButton valueButton, DocumentButton optionTemplate, string currentText,
            IReadOnlyList<DocumentDropdownOption> options, Action close)
        {
            ClearEntries();
            RectTransform root = (RectTransform)transform;
            RectTransform valueRect = (RectTransform)valueButton.transform;
            Canvas canvas = valueButton.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            root.SetParent(canvasRect, false);
            root.SetAsLastSibling();
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(boxWidth, headerHeight + options.Count * optionHeight);
            Vector3[] corners = new Vector3[4];
            valueRect.GetWorldCorners(corners);
            root.localScale = new Vector3(
                valueRect.lossyScale.x / canvasRect.lossyScale.x,
                valueRect.lossyScale.y / canvasRect.lossyScale.y,
                valueRect.lossyScale.z / canvasRect.lossyScale.z);
            root.rotation = valueRect.rotation;
            root.position = corners[1];
            Image valueImage = valueButton.GetComponent<Image>();
            if (background != null && valueImage != null)
            {
                background.sprite = valueImage.sprite;
                background.type = Image.Type.Sliced;
            }

            DocumentButton header = Instantiate(valueButton, transform, false);
            header.gameObject.name = "Dropdown header";
            SetEntryRect(header.transform, 0f, headerHeight);
            header.Bind(currentText, close);
            header.SetDropdownStyle(false, normalText, true);
            entries.Add(header.gameObject);

            for (int i = 0; i < options.Count; i++)
            {
                DocumentDropdownOption option = options[i];
                DocumentButton item = Instantiate(optionTemplate, transform, false);
                item.gameObject.name = "Dropdown option " + (i + 1);
                item.gameObject.SetActive(true);
                SetEntryRect(item.transform, headerHeight + i * optionHeight, optionHeight);
                item.Bind(option.label, option.enabled ? option.selected : null, option.enabled);
                item.SetDropdownStyle(false, option.enabled ? normalText : disabledText, false);
                entries.Add(item.gameObject);
                if (i < options.Count - 1) createSeparator(i);
            }

            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            ClearEntries();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            RectTransform rect = transform as RectTransform;
            Canvas canvas = GetComponent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue(), camera)) Close();
        }

        private void createSeparator(int index)
        {
            var line = new GameObject("Dropdown separator", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(transform, false);
            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(boxWidth - 22f, 2f);
            rect.anchoredPosition = new Vector2(11f, -(headerHeight + (index + 1) * optionHeight - 1f));
            var image = line.GetComponent<Image>();
            image.color = separatorColor;
            image.raycastTarget = false;
            entries.Add(line);
        }

        private static void SetEntryRect(Transform entry, float top, float height)
        {
            var rect = (RectTransform)entry;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(boxWidth, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private void ClearEntries()
        {
            foreach (GameObject entry in entries)
                if (entry != null) { entry.SetActive(false); Destroy(entry); }
            entries.Clear();
        }
    }
}
