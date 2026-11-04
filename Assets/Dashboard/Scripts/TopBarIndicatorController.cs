using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TopBarIndicatorController : MonoBehaviour
{
    [System.Serializable]
    public struct TopBarButton
    {
        public Button button;
        public RectTransform targetTransform;
    }

    [Header("Top Bar Settings")]
    [SerializeField] private List<TopBarButton> topBarButtons = new List<TopBarButton>(6);
    [SerializeField] private UnityEngine.Object markCurrentTap;
    [SerializeField] private int defaultIndex = 0;

    [Header("Indicator Position Offset Settings")]
    [Tooltip("인디케이터 위치 미세 조정 (음수: 왼쪽, 양수: 오른쪽)")]
    [SerializeField] private float xOffset = -5f;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.2f;

    private Coroutine moveCoroutine;
    private RectTransform indicatorRect;

    private void Awake()
    {
        if (markCurrentTap != null)
        {
            if (markCurrentTap is GameObject go)
            {
                indicatorRect = go.GetComponent<RectTransform>();
            }
            else if (markCurrentTap is Component comp)
            {
                indicatorRect = comp.GetComponent<RectTransform>();
            }

            if (indicatorRect != null)
            {
                LayoutElement layoutElement = indicatorRect.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = indicatorRect.gameObject.AddComponent<LayoutElement>();
                }
                layoutElement.ignoreLayout = true;
            }
        }
    }

    private void Start()
    {
        for (int i = 0; i < topBarButtons.Count; i++)
        {
            int index = i;
            if (topBarButtons[i].button != null)
            {
                topBarButtons[i].button.onClick.AddListener(() => MoveIndicatorTo(index));
            }
        }

        if (topBarButtons.Count > defaultIndex && indicatorRect != null)
        {
            SetIndicatorPositionDirect(defaultIndex);
        }
    }

    public void MoveIndicatorTo(int index)
    {
        if (index < 0 || index >= topBarButtons.Count || indicatorRect == null) return;

        indicatorRect.SetAsLastSibling();

        RectTransform target = topBarButtons[index].targetTransform != null
            ? topBarButtons[index].targetTransform
            : topBarButtons[index].button.GetComponent<RectTransform>();

        if (target == null) return;

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        float targetLocalX = GetTargetLocalX(target);
        moveCoroutine = StartCoroutine(AnimateMove(targetLocalX));
    }

    private float GetTargetLocalX(RectTransform target)
    {
        float calculatedX = 0f;

        if (indicatorRect.parent != null)
        {
            Vector3 worldPos = target.position;
            Vector3 localPos = indicatorRect.parent.InverseTransformPoint(worldPos);
            calculatedX = localPos.x;
        }
        else
        {
            calculatedX = target.anchoredPosition.x;
        }

        return calculatedX + xOffset;
    }

    private IEnumerator AnimateMove(float targetLocalX)
    {
        Vector2 startPos = indicatorRect.anchoredPosition;
        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / moveDuration);

            float currentX = Mathf.Lerp(startPos.x, targetLocalX, t);
            indicatorRect.anchoredPosition = new Vector2(currentX, indicatorRect.anchoredPosition.y);
            yield return null;
        }

        indicatorRect.anchoredPosition = new Vector2(targetLocalX, indicatorRect.anchoredPosition.y);
    }

    private void SetIndicatorPositionDirect(int index)
    {
        if (indicatorRect == null) return;

        indicatorRect.SetAsLastSibling();

        RectTransform target = topBarButtons[index].targetTransform != null
            ? topBarButtons[index].targetTransform
            : topBarButtons[index].button.GetComponent<RectTransform>();

        if (target != null)
        {
            float targetLocalX = GetTargetLocalX(target);
            indicatorRect.anchoredPosition = new Vector2(targetLocalX, indicatorRect.anchoredPosition.y);
        }
    }
}