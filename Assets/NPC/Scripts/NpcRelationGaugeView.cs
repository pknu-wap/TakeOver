using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.NPC
{
    public class NpcRelationGaugeView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI leftLabelText;
        [SerializeField] private TextMeshProUGUI rightLabelText;
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private RectTransform activeFillRect;
        [SerializeField] private RectTransform indicatorTickRect;

        [Header("Gauge Colors")]
        [SerializeField] private Color positiveFillColor = new Color(0.302f, 0.455f, 0.620f, 1f); // #4D749E
        [SerializeField] private Color negativeFillColor = new Color(0.760f, 0.310f, 0.330f, 1f);

        private Image activeFillImage;

        private void Awake()
        {
            if (activeFillRect != null)
            {
                activeFillImage = activeFillRect.GetComponent<Image>();
            }
        }

        public void SetLabels(string leftLabel, string rightLabel)
        {
            if (leftLabelText != null) leftLabelText.text = leftLabel;
            if (rightLabelText != null) rightLabelText.text = rightLabel;
        }

        public void SetBipolarValue(float value)
        {
            float clamped = Mathf.Clamp(value, -100f, 100f);
            float normalized = Mathf.InverseLerp(-100f, 100f, clamped); // 0.5가 중앙(0)

            if (indicatorTickRect != null)
            {
                indicatorTickRect.anchorMin = new Vector2(normalized, 0.5f);
                indicatorTickRect.anchorMax = new Vector2(normalized, 0.5f);
                indicatorTickRect.anchoredPosition = Vector2.zero;
            }

            if (activeFillRect != null)
            {
                float minX = Mathf.Min(0.5f, normalized);
                float maxX = Mathf.Max(0.5f, normalized);
                activeFillRect.anchorMin = new Vector2(minX, 0f);
                activeFillRect.anchorMax = new Vector2(maxX, 1f);
                activeFillRect.offsetMin = Vector2.zero;
                activeFillRect.offsetMax = Vector2.zero;

                if (activeFillImage == null)
                {
                    activeFillImage = activeFillRect.GetComponent<Image>();
                }

                if (activeFillImage != null)
                {
                    activeFillImage.color = clamped < 0f ? negativeFillColor : positiveFillColor;
                }
            }
        }
    }
}