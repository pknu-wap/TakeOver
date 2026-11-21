using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TopBarIndicatorController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private List<Button> tabButtons = new List<Button>();
    [SerializeField] private RectTransform markCurrentTap;

    [Header("Settings")]
    [SerializeField] private float moveDuration = 0.15f;
    [Tooltip("미세 오프셋 조절 (음수: 왼쪽, 양수: 오른쪽)")]
    [SerializeField] private float manualOffsetX = -1f;

    private RectTransform selectedTab;
    private float originX;
    private float elapsed;

    public void AddTab(Button button) => tabButtons.Add(button);

    private void Awake()
    {
        if (markCurrentTap != null)
        {
            LayoutElement layout = markCurrentTap.GetComponent<LayoutElement>();
            if (layout == null) layout = markCurrentTap.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            foreach (var graphic in markCurrentTap.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            markCurrentTap.SetAsLastSibling();
        }
    }

    public void OnTabClicked(int index, bool animate = true)
    {
        if (markCurrentTap == null) return;
        if (index < 0 || index >= tabButtons.Count || tabButtons[index] == null) return;

        selectedTab = (RectTransform)tabButtons[index].transform;
        markCurrentTap.gameObject.SetActive(true);
        originX = markCurrentTap.position.x;
        elapsed = animate ? 0f : moveDuration;
    }

    private void LateUpdate()
    {
        if (selectedTab == null) return;
        var position = markCurrentTap.position;
        float targetX = selectedTab.TransformPoint(selectedTab.rect.center).x
            - markCurrentTap.TransformVector(markCurrentTap.rect.center).x
            + markCurrentTap.TransformVector(Vector3.right * manualOffsetX).x;
        if (elapsed < moveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            targetX = Mathf.Lerp(originX, targetX, Mathf.SmoothStep(0f, 1f, elapsed / moveDuration));
        }
        position.x = targetX;
        markCurrentTap.position = position;
    }
}
