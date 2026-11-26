using System;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentButton : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Button button;
        private RectTransform chevron;

        public void Bind(string text, Action clicked, bool interactable = true)
        {
            label.text = text;
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
        }

        public void SetColor(Color color) => label.color = color;

        public void SetDropdownStyle(bool showBackground, Color labelColor, bool showChevron)
        {
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.color = labelColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = showChevron ? new Vector2(-24f, 0f) : Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float availableWidth = label.rectTransform.rect.width;
            for (int size = 20; size >= 14; size--)
            {
                label.fontSize = size;
                if (label.preferredWidth <= availableWidth) break;
            }

            Image background = button.targetGraphic as Image;
            if (background != null)
            {
                background.type = Image.Type.Sliced;
                background.color = showBackground ? new Color32(216, 216, 216, 255) : new Color(1f, 1f, 1f, 0f);
                background.raycastTarget = true;
            }
            button.transition = Selectable.Transition.None;

            if (showChevron)
            {
                if (chevron == null) chevron = createChevron();
                chevron.gameObject.SetActive(true);
            }
            else if (chevron != null) chevron.gameObject.SetActive(false);
        }

        private RectTransform createChevron()
        {
            var root = new GameObject("Dropdown chevron", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, .5f);
            rootRect.pivot = new Vector2(1f, .5f);
            rootRect.sizeDelta = new Vector2(13f, 13f);
            rootRect.anchoredPosition = new Vector2(-11f, 0f);
            createChevronStroke(rootRect, "Left stroke", new Vector2(-2.3f, 1.3f), 45f);
            createChevronStroke(rootRect, "Right stroke", new Vector2(2.3f, 1.3f), -45f);
            return rootRect;
        }

        private static void createChevronStroke(RectTransform parent, string name, Vector2 position, float angle)
        {
            var stroke = new GameObject(name, typeof(RectTransform), typeof(Image));
            stroke.transform.SetParent(parent, false);
            var rect = (RectTransform)stroke.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(2f, 7f);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = stroke.GetComponent<Image>();
            image.color = new Color32(17, 17, 17, 255);
            image.raycastTarget = false;
        }
    }
}
