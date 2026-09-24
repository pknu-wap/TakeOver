using UnityEngine;

public class ShareSellButton : MonoBehaviour
{
    [SerializeField] private ShareTradingSystem shareTradingSystem;
    [SerializeField] private Company targetCompany;

    public void testSell()
    {
        shareTradingSystem.sellShares(targetCompany);
    }

}
