using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using TakeOver.Events;

public class DashboardCardSlide : MonoBehaviour
{
    [System.Serializable]
    public struct CardUI
    {
        public RectTransform cardRect;
        public TextMeshProUGUI speakerText;
        public TextMeshProUGUI apCostText;
    }

    [Header("UI Reference (0:LeftL, 1:LeftR, 2:Main, 3:RightL, 4:RightR)")]
    [SerializeField] private List<CardUI> cardUIList;

    [Header("Random Display Settings")]
    [SerializeField] private int targetDisplayCount = 5;

    [Header("Slide Animation Settings")]
    [SerializeField] private float slideDuration = 0.3f;

    [Header("Card Style Presets")]
    [SerializeField] private List<float> cardScales = new List<float> { 0.7f, 0.85f, 1.0f, 0.85f, 0.7f };
    [SerializeField] private List<float> cardRotations = new List<float> { 12f, 6f, 0f, -6f, -12f };

    private List<TodayEventInfo> displayEventList = new List<TodayEventInfo>();
    private int currentIndex = 0;
    private EventSystemController eventController;

    private List<Vector2> targetPositions = new List<Vector2>();
    private bool isAnimating = false;

    private void Awake()
    {
        eventController = FindFirstObjectByType<EventSystemController>();

        foreach (var cardUI in cardUIList)
        {
            if (cardUI.cardRect != null)
            {
                targetPositions.Add(cardUI.cardRect.anchoredPosition);
            }
            else
            {
                targetPositions.Add(Vector2.zero);
            }
        }
    }

    private void OnEnable()
    {
        if (eventController != null)
        {
            eventController.todayEventsChanged += RefreshCards;
        }
    }

    private void OnDisable()
    {
        if (eventController != null)
        {
            eventController.todayEventsChanged -= RefreshCards;
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.1f);
        ApplyCardTransforms();
        RefreshCards();
    }

    public void RefreshCards()
    {
        if (eventController == null)
            eventController = FindFirstObjectByType<EventSystemController>();

        if (eventController == null) return;

        var infos = eventController.getTodayEventInfos();

        if (infos != null && infos.Count > 0)
        {
            List<TodayEventInfo> shuffledInfos = ShuffleList(infos);

            displayEventList.Clear();
            for (int i = 0; i < targetDisplayCount; i++)
            {
                displayEventList.Add(shuffledInfos[i % shuffledInfos.Count]);
            }

            currentIndex = 0;
            SetAllCardsUI();
        }
    }

    public void OnClickNext()
    {
        if (displayEventList.Count == 0 || isAnimating) return;

        currentIndex = (currentIndex + 1) % displayEventList.Count;
        StartCoroutine(AnimateRotation(true));
    }

    public void OnClickPrev()
    {
        if (displayEventList.Count == 0 || isAnimating) return;

        currentIndex = (currentIndex - 1 + displayEventList.Count) % displayEventList.Count;
        StartCoroutine(AnimateRotation(false));
    }

    private IEnumerator AnimateRotation(bool isNext)
    {
        isAnimating = true;

        int count = cardUIList.Count;
        Vector2[] startPos = new Vector2[count];
        Vector2[] endPos = new Vector2[count];
        float[] startScale = new float[count];
        float[] endScale = new float[count];
        float[] startRot = new float[count];
        float[] endRot = new float[count];

        for (int i = 0; i < count; i++)
        {
            int nextIndex = isNext ? (i - 1 + count) % count : (i + 1) % count;

            startPos[i] = targetPositions[i];
            endPos[i] = targetPositions[nextIndex];

            startScale[i] = cardScales[i];
            endScale[i] = cardScales[nextIndex];

            startRot[i] = cardRotations[i];
            endRot[i] = cardRotations[nextIndex];

            if (isNext && i == 3 && cardUIList[i].cardRect != null)
            {
                cardUIList[i].cardRect.SetAsLastSibling();
            }
            else if (!isNext && i == 1 && cardUIList[i].cardRect != null)
            {
                cardUIList[i].cardRect.SetAsLastSibling();
            }
        }

        float elapsedTime = 0f;
        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / slideDuration);

            for (int i = 0; i < count; i++)
            {
                if (cardUIList[i].cardRect != null)
                {
                    Vector2 currentPos = Vector2.Lerp(startPos[i], endPos[i], t);

                    if ((isNext && i == 3) || (!isNext && i == 1))
                    {
                        float arcOffset = Mathf.Sin(t * Mathf.PI) * 15f;
                        currentPos.y += arcOffset;
                    }

                    cardUIList[i].cardRect.anchoredPosition = currentPos;

                    float currentScale = Mathf.Lerp(startScale[i], endScale[i], t);
                    cardUIList[i].cardRect.localScale = new Vector3(currentScale, currentScale, 1f);

                    float currentRot = Mathf.LerpAngle(startRot[i], endRot[i], t);
                    cardUIList[i].cardRect.localRotation = Quaternion.Euler(0f, 0f, currentRot);
                }
            }
            yield return null;
        }

        SetAllCardsUI();
        ApplyCardTransforms();

        isAnimating = false;
    }

    private void ApplyCardTransforms()
    {
        for (int i = 0; i < cardUIList.Count; i++)
        {
            if (cardUIList[i].cardRect != null)
            {
                cardUIList[i].cardRect.anchoredPosition = targetPositions[i];
                cardUIList[i].cardRect.localScale = new Vector3(cardScales[i], cardScales[i], 1f);
                cardUIList[i].cardRect.localRotation = Quaternion.Euler(0f, 0f, cardRotations[i]);
            }
        }

        if (cardUIList.Count > 2 && cardUIList[2].cardRect != null)
        {
            cardUIList[2].cardRect.SetAsLastSibling();
        }
    }

    private void SetAllCardsUI()
    {
        for (int i = 0; i < cardUIList.Count; i++)
        {
            if (i >= displayEventList.Count) break;

            int eventIndex = (currentIndex + i) % displayEventList.Count;
            var eventInfo = displayEventList[eventIndex];
            var cardUI = cardUIList[i];

            if (eventInfo != null)
            {
                if (cardUI.speakerText != null)
                    cardUI.speakerText.text = $"{eventInfo.speakerRole} {eventInfo.speakerName}";

                if (cardUI.apCostText != null)
                    cardUI.apCostText.text = $"소모 에너지 {eventInfo.definition.getApCost()}";
            }
        }
    }

    private List<T> ShuffleList<T>(IReadOnlyList<T> list)
    {
        List<T> result = new List<T>(list);

        for (int i = result.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);

            T temp = result[i];
            result[i] = result[randomIndex];
            result[randomIndex] = temp;
        }

        return result;
    }
}