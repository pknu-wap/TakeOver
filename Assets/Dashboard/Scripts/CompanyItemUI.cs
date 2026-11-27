using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompanyItemUI : MonoBehaviour
{
    [SerializeField] private Image companyIcon;
    [SerializeField] private TextMeshProUGUI companyNameText;
    [SerializeField] private TextMeshProUGUI stakeText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI changeRateText;

    [Header("등락 색 (지도 오른쪽 기업 목록 CompanyRowSmall과 같은 값)")]
    [SerializeField] private Color upColor = new Color32(232, 65, 65, 255);
    [SerializeField] private Color downColor = new Color32(64, 120, 235, 255);
    [SerializeField] private Color flatColor = new Color32(175, 192, 216, 255);

    public void SetData(string companyName, float stake, float value, float changeRate, Sprite icon = null)
    {
        if (companyNameText != null)
            companyNameText.text = companyName;

        if (stakeText != null)
            stakeText.text = StakeText.Format(stake);

        if (valueText != null)
            valueText.text = MoneyText.Format(value);

        if (changeRateText != null)
        {
            if (Mathf.Approximately(changeRate, 0f))
            {
                changeRateText.text = "-";
                changeRateText.color = flatColor;
            }
            else
            {
                bool isUp = changeRate > 0f;
                changeRateText.text = $"{(isUp ? "▲" : "▼")} {Mathf.Abs(changeRate):0.0}%";
                changeRateText.color = isUp ? upColor : downColor;
            }
        }

        if (companyIcon != null)
        {
            companyIcon.sprite = icon;
            companyIcon.enabled = icon != null;
        }
    }
}
