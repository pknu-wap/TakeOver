using UnityEngine;

public class ShareTradingSystem : MonoBehaviour
{
    [SerializeField] private Player player;
    public bool buyShares(Company company, int buyAmount)
    {
        float sharePrice = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
        float remainShares = company.state.getDerivedStat(CompanyDerivedStat.REMAIN_SHARES);
        if (buyAmount <= 0 || sharePrice <= 0 || buyAmount > remainShares) {
            Debug.Log("구매 실패");
            return false;
        }

        float cost = sharePrice * buyAmount;
        if (cost > player.state.cash) {
            Debug.Log("구매 실패");
            return false;
        }

        player.state.addCash(-cost);
        company.state.addStat(CompanyStat.PLAYER_SHARES, buyAmount);
        company.calculator.calculateAboutShares(company);
        Debug.Log($"{company}, {buyAmount} 구매");
        player.printLog();
        // company.printLog();
        return true;
    }

    public bool sellShares(Company company, int sellAmount)
    {
        float sharePrice = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
        float playerShares = company.state.getStat(CompanyStat.PLAYER_SHARES);
        if (sellAmount <= 0 || sharePrice <= 0 || sellAmount > playerShares) {
            Debug.Log("판매 실패");
            return false;
        }

        float revenue = sharePrice * sellAmount;
        player.state.addCash(revenue);
        company.state.addStat(CompanyStat.PLAYER_SHARES, -sellAmount);
        company.calculator.calculateAboutShares(company);
        Debug.Log($"{company}, {sellAmount} 판매");
        player.printLog();
        // company.printLog();
        return true;
    }
}
