using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class DialogueLogWindow : MonoBehaviour
    {
        private static DialogueLogWindow active;
        private static int escapeFrame = -1;

        public static bool CloseForEscape()
        {
            if (escapeFrame == Time.frameCount) return true;
            if (active == null) return false;
            escapeFrame = Time.frameCount;
            active.Close();
            return true;
        }

        public static DialogueLogWindow Show(EventNegotiationView source)
        {
            var root = new GameObject("Dialogue log window", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(source.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0, 0, 0, .16f);
            var window = root.AddComponent<DialogueLogWindow>();
            active = window;
            var panel = source.CreateInformationPanel(root.transform, out var heading, out var body);
            var background = panel.GetComponent<Image>();
            if (background != null)
            {
                var tint = background.color; tint.a = .65f; background.color = tint;
            }
            Place((RectTransform)panel.transform, Vector2.zero, new Vector2(1040, 580));
            heading.text = "대화 로그";
            body.gameObject.SetActive(false);
            var viewport = new GameObject("Log viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            Place((RectTransform)viewport.transform, new Vector2(0, -5), new Vector2(980, 398));
            viewport.GetComponent<Image>().color = Color.clear;
            var contentRect = new GameObject("Log lines", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRect.SetParent(viewport.transform, false);
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f, 1); contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var ruleSource = heading.transform.parent.Find("Underline");
            float cursor = 0;
            int count = source.DialogueLogEntries.Count;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var entry = Instantiate(body, contentRect, false);
                entry.name = "Log entry " + i; entry.gameObject.SetActive(true); entry.fontSize = 20;
                entry.supportRichText = false; entry.alignment = TextAnchor.UpperLeft;
                entry.horizontalOverflow = HorizontalWrapMode.Wrap; entry.verticalOverflow = VerticalWrapMode.Overflow;
                entry.text = count == 0 ? "아직 표시된 대사가 없습니다." : source.DialogueLogEntries[i];
                TopRow(entry.rectTransform, cursor, 0);
                float height = entry.preferredHeight;
                TopRow(entry.rectTransform, cursor, height);
                cursor += height + 14;
                if (count > 0)
                {
                    var rule = Instantiate(ruleSource, contentRect, false);
                    rule.name = "Log separator " + i; rule.gameObject.SetActive(true);
                    TopRow((RectTransform)rule, cursor, ((RectTransform)ruleSource).rect.height);
                    foreach (var graphic in rule.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                    cursor += ((RectTransform)ruleSource).rect.height + 18;
                }
            }
            contentRect.sizeDelta = new Vector2(0, Mathf.Max(398, cursor));
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform; scroll.content = contentRect;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0;
            var close = source.CreateActionButton(panel.transform, "Close dialogue log", "닫기", window.Close);
            Place((RectTransform)close.transform, new Vector2(0, -245), new Vector2(160, 42));
            return window;
        }
        private static void TopRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(0, height); rect.localScale = Vector3.one;
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseForEscape();
        }
        private void OnDestroy() { if (active == this) active = null; }
        private void Close()
        {
            // Keep the modal alive for the closing frame so the same key/click cannot advance a line.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
