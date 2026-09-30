using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;   // IScrollHandler, PointerEventData

// 도시맵 확대·축소 담당.
// 드래그 이동은 Scroll Rect가 해주고, 이 스크립트는 확대·축소만 맡는다.
// Scroll Rect와 같은 오브젝트(CompanyMap)에 붙인다.

// 확대·축소 방법 2가지
//   1) 마우스 휠 : 커서 위치를 중심으로
public class MapZoom : MonoBehaviour, IScrollHandler
{
    [Tooltip("확대·축소할 지도 (Viewport 안의 MapContent)")]
    [SerializeField] private RectTransform content;

    [Tooltip("[추가] 지도가 보이는 창 (CompanyMap 안의 Viewport). 버튼으로 확대할 때 이 가운데가 기준")]
    [SerializeField] private RectTransform viewport;

    [Tooltip("[추가] 현재 배율을 보여줄 글자 (예: 100%). 없으면 비워둬도 됨")]
    [SerializeField] private TMP_Text zoomText;

    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 2f;

    [Tooltip("휠 한 칸 / 버튼 한 번당 배율 변화 (0.1 = 10%씩)")]
    [SerializeField] private float zoomStep = 0.1f;

    private float currentZoom = 1f;

    void Start()
    {
        UpdateZoomText();
    }

    // 마우스 휠 (EventSystem이 자동으로 불러줌)
    public void OnScroll(PointerEventData eventData)
    {
        float wheel = eventData.scrollDelta.y;
        if (wheel == 0f)
        {
            return;
        }

        // 화면 좌표(커서)를 월드 좌표로 바꾼다. 그 지점을 중심으로 확대한다.
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            content, eventData.position, eventData.pressEventCamera, out Vector3 pivotWorld);

        Step(Mathf.Sign(wheel), pivotWorld);
    }

   
    public void ZoomIn()
    {
        Step(1f, viewport.position);    // viewport.position = 보이는 창의 가운데
    }

    // [추가] − 버튼 OnClick에 연결
    public void ZoomOut()
    {
        Step(-1f, viewport.position);
    }

    // [변경] 휠과 버튼이 같이 쓰는 확대 처리.
    // direction : +1이면 확대, -1이면 축소
    // pivotWorld : 이 지점이 확대 전후로 같은 자리에 머문다
    private void Step(float direction, Vector3 pivotWorld)
    {
        float newZoom = Mathf.Clamp(currentZoom * (1f + direction * zoomStep), minZoom, maxZoom);
        if (Mathf.Approximately(newZoom, currentZoom))
        {
            return;
        }

        // 1) 확대 전: 기준점이 지도 위 어디인지 (지도 기준 좌표)
        //    InverseTransformPoint = 월드 좌표를 "이 오브젝트 기준 좌표"로 바꾸는 함수
        Vector2 before = content.InverseTransformPoint(pivotWorld);

        // 2) 배율 적용
        currentZoom = newZoom;
        content.localScale = new Vector3(currentZoom, currentZoom, 1f);

        // 3) 확대 후: 같은 기준점이 이제 지도 위 어디인지
        Vector2 after = content.InverseTransformPoint(pivotWorld);

        // 4) 차이만큼 지도를 옮겨서, 기준점 아래에 원래 보던 곳이 다시 오게 한다
        content.anchoredPosition += (after - before) * currentZoom;

        UpdateZoomText();
    }

    // [추가] 배율 글자 갱신. 1.0 → "100%"
    private void UpdateZoomText()
    {
        if (zoomText != null)
        {
            zoomText.text = $"{currentZoom * 100f:0}%";
        }
    }
}