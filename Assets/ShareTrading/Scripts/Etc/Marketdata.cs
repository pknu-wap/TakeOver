using UnityEngine;
using System.Collections.Generic;

public class Marketdata : MonoBehaviour
{
    [SerializeField] private Companies companies;
    private System.Random random = new System.Random();
    private int lastUpdatedTurn;

    public float globalMarketScore { get; private set; }

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

    public void setRandomSeed(int seed) { random = new System.Random(seed); }

    public void updateTurn(int turn)
    {
        if (turn <= lastUpdatedTurn) return;
        lastUpdatedTurn = turn;
        globalMarketScore = CompanyCalculationMath.clamp(
            globalMarketScore * 0.85d + randomRange(-0.15f, 0.15f), -1f, 1f);
        foreach (Company company in companies.CompanyList)
        {
            company.marketState.updateTurn(randomRange(-0.02f, 0.02f));
        }
    }

    private float randomRange(float min, float max)
    {
        return (float)(min + random.NextDouble() * (max - min));
    }
}
