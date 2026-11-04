using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;

public class DashboardCardSlide : MonoBehaviour
{
    [System.Serializable]
    public struct CardUI
    {
        public RectTransform cardRect;
        public UnityEngine.UI.Image cardImage;
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
    private readonly List<Button> navigationButtons = new List<Button>();
    private const string EmptyEventsText = "참여 가능한 사건이 없습니다";
    private TextMeshProUGUI emptyEventsText;
    private Coroutine rotationRoutine;
    private sealed class CardPartPose
    {
        public RectTransform rect;
        public Vector2 anchorMin, anchorMax, pivot, size, position;
        public Vector3 scale;
        public Quaternion rotation;
        public Graphic graphic;
        public Color color;
        public TMP_Text text;
        public float fontSize;
    }
    private readonly List<Dictionary<string, CardPartPose>> cardPoses = new List<Dictionary<string, CardPartPose>>();
    private readonly List<Image> transitionBackgrounds = new List<Image>();
    private readonly List<CanvasGroup> transitionGroups = new List<CanvasGroup>();

    private void Awake()
    {
        eventController = FindFirstObjectByType<EventSystemController>();

        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == this &&
                    (button.onClick.GetPersistentMethodName(i) == nameof(OnClickNext) ||
                     button.onClick.GetPersistentMethodName(i) == nameof(OnClickPrev)))
                {
                    navigationButtons.Add(button);
                    break;
                }
            }
        }

        if (cardUIList.Count > 2 && cardUIList[2].cardRect != null)
        {
            var mainCard = cardUIList[2].cardRect.gameObject;
            if (!mainCard.TryGetComponent(out UnityEngine.UI.Button button)) button = mainCard.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(OnClickMain);
            foreach (var childButton in mainCard.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (childButton != button) childButton.onClick.AddListener(OnClickMain);
            }
        }

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
        CacheCardPoses();
        if (cardUIList.Count > 2 && cardUIList[2].cardRect != null && cardUIList[2].speakerText != null)
        {
            var mainCard = cardUIList[2];
            emptyEventsText = new GameObject("EmptyEventsText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            emptyEventsText.transform.SetParent(mainCard.cardRect.parent, false);
            emptyEventsText.rectTransform.anchorMin = mainCard.cardRect.anchorMin;
            emptyEventsText.rectTransform.anchorMax = mainCard.cardRect.anchorMax;
            emptyEventsText.rectTransform.anchoredPosition = mainCard.cardRect.anchoredPosition;
            emptyEventsText.rectTransform.sizeDelta = new Vector2(mainCard.cardRect.rect.width + 100f, 120f);
            emptyEventsText.font = mainCard.speakerText.font;
            emptyEventsText.fontSharedMaterial = mainCard.speakerText.fontSharedMaterial;
            emptyEventsText.color = mainCard.speakerText.color;
            emptyEventsText.fontSize = mainCard.speakerText.fontSize;
            emptyEventsText.enableAutoSizing = true;
            emptyEventsText.fontSizeMin = mainCard.speakerText.fontSize * 0.6f;
            emptyEventsText.fontSizeMax = mainCard.speakerText.fontSize;
            emptyEventsText.alignment = TextAlignmentOptions.Center;
            emptyEventsText.raycastTarget = false;
            emptyEventsText.text = EmptyEventsText;
            emptyEventsText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (eventController != null)
        {
            eventController.todayEventsChanged += RefreshCards;
            if (targetPositions.Count == cardUIList.Count) StartCoroutine(RefreshAfterEnable());
        }
    }

    private void OnDisable()
    {
        if (eventController != null)
        {
            eventController.todayEventsChanged -= RefreshCards;
        }
        CancelRotation(false);
    }

    private IEnumerator RefreshAfterEnable()
    {
        // 부모 활성화 콜백이 끝난 뒤에만 자식의 겹침 순서를 바꾼다.
        yield return null;
        RefreshCards();
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
        CancelRotation();
        displayEventList.Clear();
        currentIndex = 0;
        if (infos != null && infos.Count > 0)
        {
            List<TodayEventInfo> shuffledInfos = ShuffleList(infos);
            int displayCount = Mathf.Min(targetDisplayCount, shuffledInfos.Count);
            for (int i = 0; i < displayCount; i++)
            {
                displayEventList.Add(shuffledInfos[i]);
            }
        }
        SetAllCardsUI();
    }

    public void OnClickNext()
    {
        if (displayEventList.Count < 2 || isAnimating) return;

        currentIndex = (currentIndex + 1) % displayEventList.Count;
        rotationRoutine = StartCoroutine(AnimateRotation(true));
    }

    private void OnClickMain()
    {
        if (eventController == null || isAnimating || displayEventList.Count == 0 || cardUIList.Count < 3) return;
        int firstSlot = GetFirstVisibleSlot();
        int mainEventOffset = Mathf.Clamp(2 - firstSlot, 0, displayEventList.Count - 1);
        var info = displayEventList[(currentIndex + mainEventOffset) % displayEventList.Count];
        eventController.openEvent(info.definition.getId());
    }

    public void OnClickPrev()
    {
        if (displayEventList.Count < 2 || isAnimating) return;

        currentIndex = (currentIndex - 1 + displayEventList.Count) % displayEventList.Count;
        rotationRoutine = StartCoroutine(AnimateRotation(false));
    }

    private IEnumerator AnimateRotation(bool isNext)
    {
        isAnimating = true;

        int firstSlot = GetFirstVisibleSlot();
        int activeCount = displayEventList.Count;
        int wrapSlot = isNext ? firstSlot : firstSlot + activeCount - 1;

        float elapsedTime = 0f;
        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / slideDuration);

            for (int offset = 0; offset < activeCount; offset++)
            {
                int slot = firstSlot + offset;
                int nextOffset = isNext ? (offset - 1 + activeCount) % activeCount : (offset + 1) % activeCount;
                int nextSlot = firstSlot + nextOffset;
                if (slot < cardUIList.Count && nextSlot < cardUIList.Count && cardUIList[slot].cardRect != null)
                {
                    Vector2 currentPos = Vector2.Lerp(targetPositions[slot], targetPositions[nextSlot], t);
                    bool wrapping = slot == wrapSlot;
                    currentPos.y += Mathf.Sin(t * Mathf.PI) * (wrapping ? -90f : 15f);

                    cardUIList[slot].cardRect.anchoredPosition = currentPos;

                    float currentScale = Mathf.Lerp(cardScales[slot], cardScales[nextSlot], t);
                    if (wrapping) currentScale *= 1f - 0.25f * Mathf.Sin(t * Mathf.PI);
                    cardUIList[slot].cardRect.localScale = new Vector3(currentScale, currentScale, 1f);

                    float currentRot = Mathf.LerpAngle(cardRotations[slot], cardRotations[nextSlot], t);
                    cardUIList[slot].cardRect.localRotation = Quaternion.Euler(0f, 0f, currentRot);
                    BlendCardPose(slot, nextSlot, t);
                    transitionGroups[slot].alpha = wrapping ? Mathf.Abs(2f * t - 1f) : 1f;
                }
            }
            // Wrapping cards travel behind the fan. The largest incoming card stays in front.
            var drawOrder = new List<int>();
            for (int slot = firstSlot; slot < firstSlot + activeCount; slot++) drawOrder.Add(slot);
            drawOrder.Sort((a, b) =>
            {
                if (a == wrapSlot) return b == wrapSlot ? 0 : -1;
                if (b == wrapSlot) return 1;
                var ar = cardUIList[a].cardRect;
                var br = cardUIList[b].cardRect;
                return (ar.rect.width * ar.localScale.x).CompareTo(br.rect.width * br.localScale.x);
            });
            foreach (int slot in drawOrder) cardUIList[slot].cardRect.SetAsLastSibling();
            yield return null;
        }

        SetAllCardsUI();
        ApplyCardTransforms();

        isAnimating = false;
        rotationRoutine = null;
    }

    private void ApplyCardTransforms()
    {
        RestoreCardPoses();
        for (int i = 0; i < cardUIList.Count; i++)
        {
            if (cardUIList[i].cardRect != null)
            {
                cardUIList[i].cardRect.anchoredPosition = targetPositions[i];
                cardUIList[i].cardRect.localScale = new Vector3(cardScales[i], cardScales[i], 1f);
                cardUIList[i].cardRect.localRotation = Quaternion.Euler(0f, 0f, cardRotations[i]);
            }
        }

        var drawOrder = new List<int>();
        for (int i = 0; i < cardUIList.Count; i++) drawOrder.Add(i);
        drawOrder.Sort((a, b) => (cardUIList[a].cardRect.rect.width * cardScales[a]).CompareTo(cardUIList[b].cardRect.rect.width * cardScales[b]));
        foreach (int slot in drawOrder)
            if (cardUIList[slot].cardRect != null) cardUIList[slot].cardRect.SetAsLastSibling();
    }

    private void CancelRotation(bool restoreLayout = true)
    {
        if (rotationRoutine != null) StopCoroutine(rotationRoutine);
        rotationRoutine = null;
        isAnimating = false;
        if (restoreLayout && targetPositions.Count == cardUIList.Count) ApplyCardTransforms();
    }

    private string CardPartKey(RectTransform rect, CardUI card)
    {
        if (rect == card.cardRect) return "root";
        if (rect == card.speakerText?.rectTransform) return "speaker";
        if (rect == card.apCostText?.rectTransform) return "ap";
        string name = rect.name.ToLowerInvariant();
        if (name.Contains("background")) return "background";
        if (name == "selectbutton") return "select";
        if (name == "button_image") return "selectImage";
        if (name == "text (tmp)") return "selectText";
        if (name.Contains("devider")) return "divider";
        if (name.Contains("image")) return "art";
        return name;
    }

    private void CacheCardPoses()
    {
        foreach (CardUI card in cardUIList)
        {
            var poses = new Dictionary<string, CardPartPose>();
            foreach (var rect in card.cardRect.GetComponentsInChildren<RectTransform>(true))
            {
                var graphic = rect.GetComponent<Graphic>();
                var text = rect.GetComponent<TMP_Text>();
                poses[CardPartKey(rect, card)] = new CardPartPose {
                    rect = rect, anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
                    size = rect.sizeDelta, position = rect.anchoredPosition, scale = rect.localScale,
                    rotation = rect.localRotation, graphic = graphic, color = graphic != null ? graphic.color : Color.white,
                    text = text, fontSize = text != null ? text.fontSize : 0f
                };
            }
            cardPoses.Add(poses);
            var overlay = new GameObject("CardTransitionBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            overlay.transform.SetParent(card.cardRect, false);
            overlay.transform.SetAsFirstSibling();
            overlay.rectTransform.anchorMin = Vector2.zero;
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.rectTransform.sizeDelta = Vector2.zero;
            overlay.raycastTarget = false;
            overlay.gameObject.SetActive(false);
            transitionBackgrounds.Add(overlay);
            var group = card.cardRect.GetComponent<CanvasGroup>();
            if (group == null) group = card.cardRect.gameObject.AddComponent<CanvasGroup>();
            transitionGroups.Add(group);
        }
    }

    private void BlendCardPose(int sourceSlot, int destinationSlot, float t)
    {
        foreach (var pair in cardPoses[sourceSlot])
        {
            if (!cardPoses[destinationSlot].TryGetValue(pair.Key, out var destination)) continue;
            var source = pair.Value;
            if (pair.Key != "root")
            {
                source.rect.anchorMin = Vector2.Lerp(source.anchorMin, destination.anchorMin, t);
                source.rect.anchorMax = Vector2.Lerp(source.anchorMax, destination.anchorMax, t);
                source.rect.pivot = Vector2.Lerp(source.pivot, destination.pivot, t);
                source.rect.anchoredPosition = Vector2.Lerp(source.position, destination.position, t);
                source.rect.localScale = Vector3.Lerp(source.scale, destination.scale, t);
                source.rect.localRotation = Quaternion.Slerp(source.rotation, destination.rotation, t);
            }
            source.rect.sizeDelta = Vector2.Lerp(source.size, destination.size, t);
            if (source.graphic != null) source.graphic.color = Color.Lerp(source.color, destination.color, t);
            if (source.text != null) source.text.fontSize = Mathf.Lerp(source.fontSize, destination.fontSize, t);
        }
        var targetPose = cardPoses[destinationSlot]["root"];
        var targetImage = targetPose.graphic as Image;
        var overlay = transitionBackgrounds[sourceSlot];
        overlay.sprite = targetImage.sprite;
        overlay.type = targetImage.type;
        overlay.color = new Color(targetPose.color.r, targetPose.color.g, targetPose.color.b, targetPose.color.a * t);
        overlay.gameObject.SetActive(true);
    }

    private void RestoreCardPoses()
    {
        for (int i = 0; i < cardPoses.Count; i++)
        {
            foreach (var pose in cardPoses[i].Values)
            {
                pose.rect.anchorMin = pose.anchorMin; pose.rect.anchorMax = pose.anchorMax; pose.rect.pivot = pose.pivot;
                pose.rect.sizeDelta = pose.size; pose.rect.anchoredPosition = pose.position;
                pose.rect.localScale = pose.scale; pose.rect.localRotation = pose.rotation;
                if (pose.graphic != null) pose.graphic.color = pose.color;
                if (pose.text != null) pose.text.fontSize = pose.fontSize;
            }
            transitionGroups[i].alpha = 1f;
            transitionBackgrounds[i].gameObject.SetActive(false);
        }
    }

    private void SetAllCardsUI()
    {
        int firstSlot = GetFirstVisibleSlot();
        for (int i = 0; i < cardUIList.Count; i++)
        {
            bool visible = displayEventList.Count > 0 && i >= firstSlot && i < firstSlot + displayEventList.Count;
            if (cardUIList[i].cardRect != null) cardUIList[i].cardRect.gameObject.SetActive(visible);
            if (!visible)
            {
                if (cardUIList[i].speakerText != null) cardUIList[i].speakerText.text = "";
                if (cardUIList[i].apCostText != null) cardUIList[i].apCostText.text = "";
                continue;
            }

            int eventIndex = (currentIndex + i - firstSlot) % displayEventList.Count;
            var eventInfo = displayEventList[eventIndex];
            var cardUI = cardUIList[i];

            if (eventInfo != null)
            {
                if (cardUI.cardImage != null)
                    cardUI.cardImage.sprite = eventInfo.cardImage;

                if (cardUI.speakerText != null)
                    cardUI.speakerText.text = eventInfo.speakerLabel;

                if (cardUI.apCostText != null)
                    cardUI.apCostText.text = $"소모 에너지 {eventInfo.definition.getApCost()}";
            }
        }

        if (emptyEventsText != null) emptyEventsText.gameObject.SetActive(displayEventList.Count == 0);

        bool canRotate = displayEventList.Count > 1;
        foreach (Button button in navigationButtons)
            if (button != null) button.interactable = canRotate;
    }

    private int GetFirstVisibleSlot()
    {
        if (cardUIList.Count == 0 || displayEventList.Count >= cardUIList.Count) return 0;
        return (cardUIList.Count - displayEventList.Count) / 2;
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
