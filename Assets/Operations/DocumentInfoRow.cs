using TakeOver.Documents;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentInfoRow : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Text value;
        [SerializeField] private Image warningIcon;
        private int originalValueFontSize;

        private void Awake()
        {
            originalValueFontSize = value.fontSize;
        }

        public void Bind(DocumentInfo info)
        {
            label.text = info.label;
            value.fontSize = originalValueFontSize;
            value.text = info.label == "내 지분" ? StakeText.FormatSaved(info.value) : info.value;
            if (warningIcon != null) warningIcon.gameObject.SetActive(info.isNotice);
            Transform rule = transform.Find("Rule");
            if (rule != null) rule.gameObject.SetActive(!info.isNotice);
            if (info.isNotice)
            {
                label.gameObject.SetActive(false);
                value.color = new Color32(201, 33, 33, 255);
                value.fontStyle = FontStyle.Bold;
                value.fontSize = 20;
                value.alignment = TextAnchor.MiddleLeft;
                value.horizontalOverflow = HorizontalWrapMode.Wrap;
                value.verticalOverflow = VerticalWrapMode.Truncate;
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 14;
                value.resizeTextMaxSize = 20;
                RectTransform rect = value.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(30f, 0f);
                rect.offsetMax = Vector2.zero;
                return;
            }
            if (string.IsNullOrEmpty(info.label))
            {
                label.gameObject.SetActive(false);
                value.alignment = TextAnchor.UpperLeft;
                value.horizontalOverflow = HorizontalWrapMode.Wrap;
                value.verticalOverflow = VerticalWrapMode.Overflow;
                value.resizeTextForBestFit = false;
                var rect = value.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(0, 4); rect.offsetMax = new Vector2(0, -4);
                var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = Mathf.Max(52, value.preferredHeight + 16);
                ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.preferredHeight);
                RectTransform separator = transform.Find("Rule") as RectTransform;
                if (separator != null) separator.anchoredPosition = new Vector2(separator.anchoredPosition.x, -layout.preferredHeight + 1f);
            }
            else
            {
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                value.verticalOverflow = VerticalWrapMode.Overflow;
                value.resizeTextForBestFit = false;
                Canvas.ForceUpdateCanvases();
                float availableWidth = value.rectTransform.rect.width;
                for (int size = originalValueFontSize; size >= 14; size--)
                {
                    value.fontSize = size;
                    if (value.preferredWidth <= availableWidth) break;
                }
            }
        }

        public void BindEmployeeRenewalHover(EmployeeRenewalDocument document)
        {
            value.raycastTarget = true;
            value.color = new Color(.17f, .31f, .48f);
            value.gameObject.AddComponent<EmployeeRenewalStatHover>().Bind(document);
        }
    }
}
