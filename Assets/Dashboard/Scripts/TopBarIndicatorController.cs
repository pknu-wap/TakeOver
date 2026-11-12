using System.Collections;
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

    private Coroutine moveCoroutine;
    private RectTransform indicatorRect;

    public void AddTab(Button button) => tabButtons.Add(button);

    private void Awake()
    {
        if (markCurrentTap != null)
        {
            indicatorRect = markCurrentTap;

            LayoutElement layout = markCurrentTap.GetComponent<LayoutElement>();
            if (layout == null) layout = markCurrentTap.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }
    }

    private void Start()
    {
        if (tabButtons.Count > 0 && tabButtons[0] != null && markCurrentTap != null)
        {
            SetIndicatorDirect(0);
        }
    }

    public void OnTabClicked(int index)
    {
        if (markCurrentTap == null) return;
        if (index < 0 || index >= tabButtons.Count || tabButtons[index] == null) return;

        if (!markCurrentTap.gameObject.activeSelf)
        {
            markCurrentTap.gameObject.SetActive(true);
        }

        markCurrentTap.SetAsLastSibling();

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(AnimateToTarget(tabButtons[index]));
    }

    private float GetTextCenterWorldX(Button button)
    {
        Graphic textGraphic = button.GetComponent<Graphic>();
        RectTransform targetRect = textGraphic != null ? textGraphic.rectTransform : button.GetComponent<RectTransform>();

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        float textCenterX = (corners[0].x + corners[2].x) * 0.5f;

        float pivotOffset = (0.5f - indicatorRect.pivot.x) * (indicatorRect.rect.width * indicatorRect.lossyScale.x);

        // 캔버스 스케일을 반영한 미세 픽셀 오프셋 보정
        float offsetWorldX = manualOffsetX * indicatorRect.lossyScale.x;

        return textCenterX + pivotOffset + offsetWorldX;
    }

    private void SetIndicatorDirect(int index)
    {
        markCurrentTap.gameObject.SetActive(true);
        markCurrentTap.SetAsLastSibling();

        float targetX = GetTextCenterWorldX(tabButtons[index]);
        markCurrentTap.position = new Vector3(targetX, markCurrentTap.position.y, markCurrentTap.position.z);
    }

    private IEnumerator AnimateToTarget(Button targetButton)
    {
        Vector3 startPos = markCurrentTap.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

            float targetX = GetTextCenterWorldX(targetButton);
            float currentX = Mathf.Lerp(startPos.x, targetX, t);

            markCurrentTap.position = new Vector3(currentX, markCurrentTap.position.y, markCurrentTap.position.z);
            yield return null;
        }

        float finalX = GetTextCenterWorldX(targetButton);
        markCurrentTap.position = new Vector3(finalX, markCurrentTap.position.y, markCurrentTap.position.z);
    }
}
