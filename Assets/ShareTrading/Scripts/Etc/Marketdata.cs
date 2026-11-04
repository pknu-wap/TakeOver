using UnityEngine;
using TakeOver.SaveSystem;
using System.Collections.Generic;

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

    public bool setNpcSentiment(string companyID, float sentiment)
    {
        if (!tryGetCompany(companyID, out Company company)) return false;
        company.marketState.setNpcSentiment(sentiment);
        company.calculator.recalculate(company, this);
        return true;
    }

    public bool setNpcOpinions(string companyID, IReadOnlyList<NpcMarketOpinion> opinions)
    {
        if (!tryGetCompany(companyID, out Company company)) return false;
        company.marketState.setNpcOpinions(opinions);
        company.calculator.recalculate(company, this);
        return true;
    }

    public bool addEventImpact(string companyID, MarketEventImpact impact)
    {
        if (!tryGetCompany(companyID, out Company company) || !company.marketState.addEventImpact(impact)) return false;
        company.calculator.recalculate(company, this);
        return true;
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
