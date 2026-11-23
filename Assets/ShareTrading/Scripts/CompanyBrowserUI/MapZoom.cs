using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapZoom : MonoBehaviour, IScrollHandler
{
    [Tooltip("확대·축소할 지도 (Viewport 안의 MapContent)")]
    [SerializeField] private RectTransform content;

    [Tooltip("현재 배율을 보여줄 글자 (예: 100%). 없으면 비워둬도 됨")]
    [SerializeField] private TMP_Text zoomText;

    [Tooltip("누를 수 있는 지도 영역에 전체 지도가 들어가는 배율을 기본으로 사용합니다.")]
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 2f;

    [Tooltip("휠 한 칸당 배율 변화 (0.1 = 10%씩)")]
    [SerializeField] private float zoomStep = 0.1f;

    private float currentZoom = 1f;

    private float fitZoom = 1f;
    private bool zoomAdjusted;

    private float effectiveMinZoom => Mathf.Max(minZoom, fitZoom);
    private float effectiveMaxZoom => Mathf.Max(maxZoom, effectiveMinZoom);

    void Start()
    {
        updateZoomText();
    }

    void OnEnable()
    {
        fitToViewport();
    }

    // 화면 크기(해상도·창 크기)가 바뀌면 Unity가 자동으로 호출
    void OnRectTransformDimensionsChange()
    {
        fitToViewport();
    }

    private void fitToViewport()
    {
        if (content == null)
        {
            return;
        }

        RectTransform viewport = content.parent as RectTransform;
        if (viewport == null)
        {
            return;
        }

        Vector2 viewportSize = viewport.rect.size;
        Vector2 contentSize = content.rect.size;
        if (contentSize.x <= 0f || contentSize.y <= 0f)
        {
            return;
        }

        if (viewportSize.x <= 0f || viewportSize.y <= 0f) return;
        fitZoom = Mathf.Min(viewportSize.x / contentSize.x, viewportSize.y / contentSize.y);

        float fixedZoom = zoomAdjusted ? Mathf.Clamp(currentZoom, effectiveMinZoom, effectiveMaxZoom) : effectiveMinZoom;
        if (!Mathf.Approximately(fixedZoom, currentZoom))
        {
            setZoom(fixedZoom);
        }
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
        float newZoom = Mathf.Clamp(currentZoom * (1f + direction * zoomStep), effectiveMinZoom, effectiveMaxZoom);
        if (Mathf.Approximately(newZoom, currentZoom))
        {
            return;
        }

        zoomAdjusted = true;
        Vector2 before = content.InverseTransformPoint(pivotWorld);

        setZoom(newZoom);

        Vector2 after = content.InverseTransformPoint(pivotWorld);

        content.anchoredPosition += (after - before) * currentZoom;
    }

    private void setZoom(float zoom)
    {
        currentZoom = zoom;
        content.localScale = new Vector3(currentZoom, currentZoom, 1f);
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
