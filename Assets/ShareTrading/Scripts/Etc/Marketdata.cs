using UnityEngine;
using TakeOver.SaveSystem;

public class Marketdata : MonoBehaviour
{
    [SerializeField] private Companies companies;
    private int lastUpdatedTurn;

    public float globalMarketScore { get; internal set; }
    internal int LastUpdatedTurn { get => lastUpdatedTurn; set => lastUpdatedTurn = value; }

    public bool tryGetCompany(string companyID, out Company company)
    {
        return companies.tryGetCompany(companyID, out company);
    }

    public void updateTurn(int turn)
    {
        if (turn <= lastUpdatedTurn) return;

        globalMarketScore = CompanyCalculationMath.clamp(
            globalMarketScore * 0.85d + FixedDaySeed.range(FixedDaySeed.seed, -0.15f, 0.15f), -1f, 1f);
        foreach (Company company in companies.CompanyList)
        {
            company.marketState.updateTurn(FixedDaySeed.range(FixedDaySeed.seed, -0.02f, 0.02f));
        }
        lastUpdatedTurn = turn;
    }
}
