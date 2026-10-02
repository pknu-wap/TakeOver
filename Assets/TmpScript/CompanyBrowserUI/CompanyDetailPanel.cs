using TMPro;
using System;           
using UnityEngine;

// 오른쪽 "선택 기업 상세 정보" 패널.

// 에디터에서 패널을 꺼둔 상태로 둘 것
public class CompanyDetailPanel : MonoBehaviour
{
    [Header("회사 정보")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text revenueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text netProfitText;
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text debtRatioText;
    [SerializeField] private TMP_Text stakeText;

    
    [Header("거래")]
    [SerializeField] private TMP_InputField amountInput;   // 수량 입력칸
    [SerializeField] private TMP_Text playerCashText;      // 내 잔액
    [SerializeField] private TMP_Text messageText;         // "매수 완료", "잔액 부족" 같은 안내

    
    public event Action OnTraded;

    
    public Company current { get; private set; }

    
    private ShareTradingSystem tradingSystem;
    private Player player;

    public void show(Company company)
    {
        current = company;
        findReferences();   

        nameText.text = company.definition.companyName;
        priceText.text = $"주가 {CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE):N2}";
        revenueText.text = $"매출 {CompanyUIUtil.getStat(company, CompanyStat.REVENUE):N0}";
        costText.text = $"비용 {CompanyUIUtil.getStat(company, CompanyStat.COST):N0}";
        netProfitText.text = $"순이익 {CompanyUIUtil.getDerived(company, CompanyDerivedStat.NET_PROFIT):N0}";
        cashText.text = $"현금 {CompanyUIUtil.getStat(company, CompanyStat.CASH):N0}";
        debtText.text = $"부채 {CompanyUIUtil.getStat(company, CompanyStat.DEBT):N0}";
        
        debtRatioText.text = $"부채비율 {CompanyUIUtil.getDerived(company, CompanyDerivedStat.DEBT_RATIO):P1}";
        stakeText.text = $"보유 지분 {CompanyUIUtil.getDerived(company, CompanyDerivedStat.STAKE):0.#}%";

        // [추가] 내 잔액 표시. Player를 못 찾았거나 아직 준비 전이면 0으로 표시한다.
        // (C#의 ?. 연산자로 줄일 수도 있지만, Unity 오브젝트에는 ?.가 제대로 동작하지 않는
        //  경우가 있어서 != null로 직접 확인하는 게 안전하다)
        float myCash = 0f;
        if (player != null && player.state != null)
        {
            myCash = player.state.cash;
        }
        playerCashText.text = $"내 잔액 {myCash:N2}";

        gameObject.SetActive(true);
    }

    
    public void hide()
    {
        gameObject.SetActive(false);
    }

    
    public void buy()
    {
        trade(true);
    }

    
    public void sell()
    {
        trade(false);
    }

    
    private void trade(bool isBuy)
    {
        if (current == null)
        {
            return;   // 선택된 회사가 없으면 아무것도 안 함
        }

        findReferences();
        if (tradingSystem == null)
        {
            messageText.text = "거래 시스템을 찾을 수 없어요";
            return;
        }

        // 입력칸 글자를 정수로 바꾼다. 숫자가 아니면 TryParse가 false를 돌려준다.
        if (!int.TryParse(amountInput.text, out int amount) || amount <= 0)
        {
            messageText.text = "수량을 1 이상의 숫자로 입력하세요";
            return;
        }

        
        bool success = isBuy
            ? tradingSystem.buyShares(current, amount)
            : tradingSystem.sellShares(current, amount);

        string action = isBuy ? "매수" : "매도";
        if (success)
        {
            messageText.text = $"{amount}주 {action} 완료";
            show(current);          
            OnTraded?.Invoke();     
        }
        else
        {
            messageText.text = isBuy
                ? $"{action} 실패 (잔액이나 남은 주식을 확인하세요)"
                : $"{action} 실패 (보유 주식을 확인하세요)";
        }
    }

    
    private void findReferences()
    {
        if (tradingSystem == null)
        {
            tradingSystem = FindFirstObjectByType<ShareTradingSystem>();
        }
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
    }
}