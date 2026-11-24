using UnityEngine;
using UnityEngine.EventSystems;

// [뉴스 카드 표시 및 호버 전환]
public class NewsRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float hoverSpeed = 30f;
    private const float hoverSnapThreshold = 0.005f;
    private const float thumbnailHorizontalInset = 61f;

    [SerializeField] private UnityEngine.UI.Text titleText;
    [SerializeField] private UnityEngine.UI.Text companyText;
    [SerializeField] private UnityEngine.UI.Text descriptionText;
    [SerializeField] private UnityEngine.UI.Text detailsText;
    [SerializeField] private RectTransform thumbnailMask;
    [SerializeField] private UnityEngine.UI.Image thumbnail;
    [SerializeField] private UnityEngine.UI.Image frame;
    [SerializeField] private UnityEngine.UI.LayoutElement cardLayout;

    private float normalWidth;
    private float hoveredWidth;
    private float hoverProgress;
    private bool isHovered;

    public void setup(string title, string companyNames, string description, bool hasNews = true, Sprite thumbnailSprite = null)
    {
        titleText.text = title;
        descriptionText.text = description;
        detailsText.gameObject.SetActive(hasNews);
        thumbnail.sprite = thumbnailSprite;
        thumbnailMask.gameObject.SetActive(hasNews && thumbnailSprite != null);
        frame.raycastTarget = hasNews;
        if (!hasNews) isHovered = false;

        bool hasCompany = !string.IsNullOrEmpty(companyNames);
        companyText.gameObject.SetActive(hasCompany);
        if (hasCompany)
        {
            companyText.text = companyNames;
        }
    }

    public void setSize(float normalWidth, float hoveredWidth, float height)
    {
        this.normalWidth = normalWidth;
        this.hoveredWidth = hoveredWidth;
        cardLayout.preferredHeight = height;
        float thumbnailWidth = Mathf.Max(0f, normalWidth - thumbnailHorizontalInset);
        thumbnailMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, thumbnailWidth);
        updateWidth();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void LateUpdate()
    {
        float target = isHovered ? 1f : 0f;
        if (hoverProgress == target) return;

        float step = 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime);
        hoverProgress = Mathf.Lerp(hoverProgress, target, step);
        if (Mathf.Abs(target - hoverProgress) < hoverSnapThreshold) hoverProgress = target;

        updateWidth();
    }

    private void OnDisable()
    {
        isHovered = false;
        hoverProgress = 0f;
        updateWidth();
    }

    private void updateWidth()
    {
        cardLayout.preferredWidth = Mathf.Lerp(normalWidth, hoveredWidth, hoverProgress);
    }
}
