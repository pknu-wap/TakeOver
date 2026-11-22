using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentInfoRow : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Text value;
        public void Bind(string heading, string text)
        {
            label.text = heading;
            value.text = text;
            if (string.IsNullOrEmpty(heading))
            {
                label.gameObject.SetActive(false);
                value.alignment = TextAnchor.UpperLeft;
                value.horizontalOverflow = HorizontalWrapMode.Wrap;
                value.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = value.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(0, 4); rect.offsetMax = new Vector2(0, -4);
                var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = Mathf.Max(36, value.preferredHeight + 8);
                ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.preferredHeight);
            }
        }
    }
}
