using System.Linq;
using TakeOver.Documents;
using TakeOver.Events;
using TakeOver.Story;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TakeOver.Operations
{
    public sealed class EmployeeRenewalStatHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float Width = 340f;
        private const float Height = 222f;
        private const float HorizontalPadding = 32f;
        // 운영 화면의 문서 캔버스(16~22) 위, 이벤트와 설정 화면 아래에 그린다.
        private const int SortOrder = 30;
        private EmployeeRenewalDocument document;
        private GameObject overlay;
        private GameObject panel;
        private UnityEngine.UI.Text comparison;
        private RectTransform canvasRect;

        public void Bind(EmployeeRenewalDocument renewal) => document = renewal;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel == null) createPanel();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, null, out var point))
                return;

            Rect bounds = canvasRect.rect;
            var rect = (RectTransform)panel.transform;
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp(point.x + 16f, bounds.xMin, bounds.xMax - Width),
                Mathf.Clamp(point.y - 12f, bounds.yMin + Height, bounds.yMax));
            comparison.text = document.StatComparison();
            panel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (overlay == null) return;
            overlay.SetActive(false);
            Destroy(overlay);
            overlay = null;
            panel = null;
        }

        private void createPanel()
        {
            overlay = new GameObject("Employee renewal hover", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)overlay.transform;
            var template = FindObjectsByType<EventNegotiationView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(view => view.GetComponentInParent<StoryDirector>() == null);
            panel = template.CreateInformationPanel(canvasRect, out var heading, out comparison);
            panel.name = "Employee renewal stat comparison";
            panel.SetActive(false);

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var background = panel.GetComponent<UnityEngine.UI.Image>();
            var color = background.color;
            color.a = 1f;
            background.color = color;

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = canvasRect.pivot;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Width, Height);
            var rule = (RectTransform)heading.transform.parent.Find("Underline");
            place(heading.rectTransform, 26f, 24f);
            place(rule, 56f, 2f);
            place(comparison.rectTransform, 70f, 134f);
            heading.text = "계약 시작 → 현재";
            heading.fontSize = 19;
            comparison.fontSize = 18;
            comparison.resizeTextForBestFit = false;
            Canvas.ForceUpdateCanvases();
        }

        private void place(RectTransform rect, float top, float height)
        {
            rect.SetParent(panel.transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HorizontalPadding, -top);
            rect.sizeDelta = new Vector2(-2f * HorizontalPadding, height);
        }
    }
}
