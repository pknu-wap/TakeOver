using UnityEngine;

public class ShareBuyButton : MonoBehaviour
{
    [SerializeField] private ShareTradingSystem shareTradingSystem;
    [SerializeField] private Company targetCompany;

    public void testBuy()
    {
        shareTradingSystem.buyShares(targetCompany);
    }

}
