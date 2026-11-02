using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기업 목록의 한 줄. 큰 목록(표)과 작은 목록이 같이 쓰고, 연결된 칸만 채운다.
public class CompanyRow : MonoBehaviour
{
    [Header("공통")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button button;

    [Header("있으면 채우는 칸")]
    [SerializeField] private Image logoImage;      // 회사 로고. 비워 둬도 동작한다.
    [SerializeField] private TMP_Text letterText;
    [SerializeField] private TMP_Text industryText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text revenueText;
    [SerializeField] private TMP_Text netProfitText;
    [SerializeField] private TMP_Text debtRatioText;
    [SerializeField] private TMP_Text stakeText;
    [SerializeField] private TMP_Text summaryText;


    public void setup(Company company, Action<Company> onClick)
    {
        string companyName = company.definition.companyName;
        float price = CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE);
        float stake = CompanyUIUtil.getDerived(company, CompanyDerivedStat.STAKE);

        nameText.text = companyName;

        setLogo(logoImage, company.definition.logo);
        setText(letterText, companyName.Length > 0 ? companyName.Substring(0, 1) : "");
        setText(industryText, company.definition.industry);
        setText(priceText, $"{price:N2}");
        setText(revenueText, $"{CompanyUIUtil.getStat(company, CompanyStat.REVENUE):N0}");
        setText(netProfitText, $"{CompanyUIUtil.getDerived(company, CompanyDerivedStat.NET_PROFIT):N0}");
        setText(debtRatioText, $"{CompanyUIUtil.getDerived(company, CompanyDerivedStat.DEBT_RATIO):P1}");
        setText(stakeText, $"{stake:0.#}%");
        setText(summaryText, $"지분 {stake:0.#}%  |  {price:N2}");

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(company));
    }

    private static void setText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    // 로고 칸이 연결돼 있으면 로고를 넣고, 로고가 없으면 Image를 꺼서 빈 흰 사각형이 안 보이게 한다.
    private static void setLogo(Image target, Sprite logo)
    {
        if (target != null)
        {
            target.sprite = logo;
            target.enabled = logo != null;
        }
    }
}
