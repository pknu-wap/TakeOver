using UnityEngine;

public class ShareBuyButton : MonoBehaviour
{
    [SerializeField] private ShareTradingSystem shareTradingSystem;
    [SerializeField] private TradeCompanySelect targetCompany;
    // [SerializeField] private Company targetCompany;
    [SerializeField] private ShareAmountSelect shareAmount;

    public void testBuy()
    {
        if (!shareAmount.tryGetAmount(out int amount)) return;
        shareTradingSystem.buyShares(targetCompany.selectedCompany, amount);  
    }

    public void testSell()
    {
        if (!shareAmount.tryGetAmount(out int amount)) return;
        shareTradingSystem.sellShares(targetCompany.selectedCompany, amount);  
    }
}
