using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("크기")]
    [Tooltip("커질 대상. 비우면 이 오브젝트 자신")]
    [SerializeField] private RectTransform scaleTarget;
    [Tooltip("호버했을 때 배율 (1.08 = 8% 커짐)")]
    [SerializeField] private float hoverScale = 1.08f;

    [Header("위치 이동 (선택)")]
    [Tooltip("호버하면 이만큼 밀린다. (0,0)이면 안 움직임")]
    [SerializeField] private Vector2 hoverOffset = Vector2.zero;

    [Header("색 바꾸기 (선택)")]
    [Tooltip("호버하면 색이 바뀔 그래픽. 비우면 색은 안 바뀜")]
    [SerializeField] private Graphic tintTarget;
    [Tooltip("Tint Target이 비어 있으면, 실행 중에 이 이름의 자식을 찾아 색 대상으로 쓴다")]
    [SerializeField] private string tintTargetChildName = "";
    [Tooltip("호버했을 때 색. 투명도(A)는 원래 값을 유지")]
    [SerializeField] private Color hoverColor = new Color32(242, 205, 130, 255);
    [Tooltip("누르고 있는 동안 밝기 (1 = 그대로, 0.8 = 20% 어둡게)")]
    [SerializeField, Range(0.5f, 1f)] private float pressedBrightness = 0.8f;

    [Header("테두리 빛 (선택)")]
    [Tooltip("같은 오브젝트에 붙인 Outline 컴포넌트")]
    [SerializeField] private Shadow outlineEffect;
    [Tooltip("테두리가 다 켜졌을 때 진하기 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float outlineAlpha = 0.9f;

    [Header("기타")]
    [Tooltip("호버하면 형제들 중 맨 앞에 그린다")]
    [SerializeField] private bool bringToFront = false;
    [Tooltip("애니메이션 빠르기 (10~20 추천)")]
    [SerializeField] private float animationSpeed = 14f;

    private Selectable selectable;
    private Vector3 baseScale;
    private Color baseTintColor;
    private Color baseOutlineColor;

    private Graphic hitGraphic;
    private Vector4 baseRaycastPadding;

    private Vector2 lastWrittenPosition;

    private bool initialized;

    private bool isHovered;
    private bool isPressed;

    private float hoverAmount;
    private float pressAmount;
    private Vector2 appliedOffset;

    void Awake()
    {
        if (outlineEffect != null)
        {
            baseOutlineColor = outlineEffect.effectColor;
            applyOutlineAlpha(0f);
        }
    }

    void Start()
    {
        ensureInitialized();
    }

    private void ensureInitialized()
    {
        if (initialized) return;
        initialized = true;

        if (scaleTarget == null) scaleTarget = (RectTransform)transform;
        selectable = GetComponent<Selectable>();

        if (tintTarget == null && !string.IsNullOrEmpty(tintTargetChildName))
        {
            Transform child = transform.Find(tintTargetChildName);
            if (child != null) tintTarget = child.GetComponent<Graphic>();
            else Debug.LogWarning($"[UIHoverEffect] '{tintTargetChildName}' 자식을 찾지 못해 색은 바꾸지 않습니다.", this);
        }

        baseScale = scaleTarget.localScale;
        if (tintTarget != null) baseTintColor = tintTarget.color;

        hitGraphic = scaleTarget.GetComponent<Graphic>();
        if (hitGraphic != null) baseRaycastPadding = hitGraphic.raycastPadding;

        lastWrittenPosition = scaleTarget.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ensureInitialized();
        if (!canReact()) return;
        if (eventData.dragging) return;

        isHovered = true;
        if (bringToFront) transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ensureInitialized();
        if (!canReact()) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    void Update()
    {
        if (!initialized) return;

        bool active = canReact();
        float targetHover = (isHovered && active) ? 1f : 0f;
        float targetPress = (isPressed && active) ? 1f : 0f;

        if (hoverAmount == targetHover && pressAmount == targetPress) return;

        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        hoverAmount = moveToward(hoverAmount, targetHover, t);
        pressAmount = moveToward(pressAmount, targetPress, t);

        applyVisuals();
    }

    void OnDisable()
    {
        isHovered = false;
        isPressed = false;
        hoverAmount = 0f;
        pressAmount = 0f;
        if (initialized) applyVisuals();
    }

    private bool canReact()
    {
        return selectable == null || selectable.IsInteractable();
    }

    private static float moveToward(float current, float target, float t)
    {
        float next = Mathf.Lerp(current, target, t);
        return Mathf.Abs(next - target) < 0.001f ? target : next;
    }

    private void applyVisuals()
    {
        if (scaleTarget == null) return;

        float scaleFactor = Mathf.Lerp(1f, hoverScale, hoverAmount);
        Vector3 currentScale = baseScale * scaleFactor;
        scaleTarget.localScale = currentScale;

        if (scaleTarget.anchoredPosition != lastWrittenPosition)
        {
            appliedOffset = Vector2.zero;
        }

        Vector2 newOffset = hoverOffset * hoverAmount;
        scaleTarget.anchoredPosition += newOffset - appliedOffset;
        appliedOffset = newOffset;
        lastWrittenPosition = scaleTarget.anchoredPosition;

        if (hitGraphic != null)
        {
            float localX = currentScale.x != 0f ? newOffset.x / currentScale.x : 0f;
            float localY = currentScale.y != 0f ? newOffset.y / currentScale.y : 0f;
            hitGraphic.raycastPadding = baseRaycastPadding + new Vector4(-localX, -localY, localX, localY);
        }

        if (tintTarget != null)
        {
            Color c = Color.Lerp(baseTintColor, hoverColor, hoverAmount);
            float shade = Mathf.Lerp(1f, pressedBrightness, pressAmount);
            c.r *= shade;
            c.g *= shade;
            c.b *= shade;
            c.a = baseTintColor.a;
            tintTarget.color = c;
        }

        if (outlineEffect != null)
        {
            applyOutlineAlpha(outlineAlpha * hoverAmount);
        }
    }

    private void applyOutlineAlpha(float alpha)
    {
        Color c = baseOutlineColor;
        c.a = alpha;
        outlineEffect.effectColor = c;
        outlineEffect.enabled = alpha > 0.001f;
    }
}
