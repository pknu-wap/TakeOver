using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapZoom : MonoBehaviour, IScrollHandler
{
    [Tooltip("확대·축소할 지도 (Viewport 안의 MapContent)")]
    [SerializeField] private RectTransform content;

    [Tooltip("현재 배율을 보여줄 글자 (예: 100%). 없으면 비워둬도 됨")]
    [SerializeField] private TMP_Text zoomText;

    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 2f;

    [Tooltip("휠 한 칸당 배율 변화 (0.1 = 10%씩)")]
    [SerializeField] private float zoomStep = 0.1f;

    private float currentZoom = 1f;

    void Start()
    {
        updateZoomText();
    }

    public void OnScroll(PointerEventData eventData)
    {
        float wheel = eventData.scrollDelta.y;
        if (wheel == 0f)
        {
            return;
        }

        if (content == null)
        {
            Debug.LogWarning(
                "[MapZoom] content가 비어 있어서 확대·축소를 멈췄어요. " +
                "Map Zoom 컴포넌트 Content 칸에 Viewport/MapContent를 넣어 주세요.", this);
            return;
        }

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            content, eventData.position, eventData.pressEventCamera, out Vector3 pivotWorld);

        step(Mathf.Sign(wheel), pivotWorld);
    }

    private void step(float direction, Vector3 pivotWorld)
    {
        float newZoom = Mathf.Clamp(currentZoom * (1f + direction * zoomStep), minZoom, maxZoom);
        if (Mathf.Approximately(newZoom, currentZoom))
        {
            return;
        }

        Vector2 before = content.InverseTransformPoint(pivotWorld);

        currentZoom = newZoom;
        content.localScale = new Vector3(currentZoom, currentZoom, 1f);

        Vector2 after = content.InverseTransformPoint(pivotWorld);

        content.anchoredPosition += (after - before) * currentZoom;

        updateZoomText();
    }

    private void updateZoomText()
    {
        if (zoomText != null)
        {
            zoomText.text = $"{currentZoom * 100f:0}%";
        }
    }
}
