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

    public void SetData(string companyName, float stake, float value, float changeRate, Sprite icon = null)
    {
        if (companyNameText != null)
            companyNameText.text = companyName;

        if (stakeText != null)
            stakeText.text = $"{stake:F1}%";

        if (valueText != null)
            valueText.text = $"{value:N0}₩";

        if (changeRateText != null)
        {
            string prefix = changeRate >= 0 ? "▲ " : "▼ ";
            changeRateText.text = $"{prefix}{Mathf.Abs(changeRate):F1}%";
        }

        if (companyIcon != null && icon != null)
            companyIcon.sprite = icon;
    }
}