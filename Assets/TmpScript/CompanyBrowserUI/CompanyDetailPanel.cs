using TMPro;
using UnityEngine;

// 오른쪽 "선택 기업 상세 정보" 패널.

// 에디터에서 패널을 꺼둔 상태로 둘 것
public class CompanyDetailPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text revenueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text netProfitText;
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text debtRatioText;
    [SerializeField] private TMP_Text stakeText;

    // 지금 보여주는 회사. 나중에 상세 패널에서 바로 매수·매도할 때 필요하다.
    // { get; private set; } : 밖에서는 읽기만 되고, 값 변경은 이 클래스 안에서만 가능
    public Company current { get; private set; }

    public void Show(Company company)
    {
        current = company;

        nameText.text = company.definition.companyName;
        priceText.text = $"주가 {CompanyUIUtil.GetDerived(company, CompanyDerivedStat.SHARE_PRICE):N2}";
        revenueText.text = $"매출 {CompanyUIUtil.GetStat(company, CompanyStat.REVENUE):N0}";
        costText.text = $"비용 {CompanyUIUtil.GetStat(company, CompanyStat.COST):N0}";
        netProfitText.text = $"순이익 {CompanyUIUtil.GetDerived(company, CompanyDerivedStat.NET_PROFIT):N0}";
        cashText.text = $"현금 {CompanyUIUtil.GetStat(company, CompanyStat.CASH):N0}";
        debtText.text = $"부채 {CompanyUIUtil.GetStat(company, CompanyStat.DEBT):N0}";
        // DEBT_RATIO는 0.4 같은 비율값이다. P1 → 100을 곱하고 % 붙여서 소수점 1자리 ("40.0%")
        debtRatioText.text = $"부채비율 {CompanyUIUtil.GetDerived(company, CompanyDerivedStat.DEBT_RATIO):P1}";
        stakeText.text = $"보유 지분 {CompanyUIUtil.GetDerived(company, CompanyDerivedStat.STAKE):0.#}%";

        gameObject.SetActive(true);
    }

    // 닫기 버튼 OnClick에 인스펙터에서 연결
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
