using UnityEngine;
using System.Collections.Generic;
using TakeOver.CompanyRules;

public class CompanyState
{
    private Dictionary<CompanyStat, float> stats;
    private Dictionary<CompanyDerivedStat, float> derivedStats;

    public int totalShares { get; internal set; }
    public CompanyStatus status = CompanyStatus.Normal;

    // 첫 턴 기록이 저장되기 전에 초기값을 과거 평균으로 사용
    public float beforeRevenue { get; set; }
    public float beforeNetProfit { get; set; }
    public float beforeNetMargin { get; set; }

    // 초기 재무 기준값은 일반 턴 계산에서 변경하지 않음
    public float initialRevenue { get; internal set; }
    public float initialCost { get; internal set; }
    public float initialNetProfit { get; internal set; }
    public float initialNetMargin { get; internal set; }
    public float initialDebtRatio { get; internal set; }
    public float initialLiquidityStrength { get; internal set; }
    public float initialAssetValue { get; internal set; }

    public float tangibleCoefficient { get; internal set; }
    public float intangibleCoefficient { get; internal set; }

    public CompanyState(CompanyDefinition definition)
    {
        stats = new Dictionary<CompanyStat, float>();
        derivedStats = new Dictionary<CompanyDerivedStat, float>();

        stats[CompanyStat.REVENUE] = definition.initialRevenue;
        stats[CompanyStat.COST] = definition.initialCost;
        stats[CompanyStat.CASH] = definition.initialCash;
        stats[CompanyStat.DEBT] = definition.initialDebt;
        stats[CompanyStat.TANGIBLE_ASSETS] = definition.initialTangibleAssets;
        stats[CompanyStat.INTANGIBLE_ASSTES] = definition.initialIntangibleAssets;
        stats[CompanyStat.REPUTATION_B2C] = definition.initialReputationB2C;
        stats[CompanyStat.REPUTATION_B2B] = definition.initialReputationB2B;
        stats[CompanyStat.MARKET_POSITION] = definition.initialMarketPosition;
        stats[CompanyStat.REPUTAION_PLAYER] = definition.initialReputaionPlayer;
        stats[CompanyStat.PLAYER_SHARES] = definition.initialPlayerShares;
        stats[CompanyStat.BASE_COMPANY_VALUE] = definition.initialBaseCompanyValue;
        stats[CompanyStat.EMPLOYEE_COUNT] = definition.initialEmployeeCount;
        stats[CompanyStat.WATCHLIST_REVIEWS_REMAINING] = 0f;
        stats[CompanyStat.LIQUIDATION_TURNS_REMAINING] = 0f;

        totalShares = definition.totalShares;

        tangibleCoefficient = definition.initialTangibleCoefficient;
        intangibleCoefficient = definition.initialIntangibleCoefficient;

        initialRevenue = definition.initialRevenue;
        initialCost = definition.initialCost;
        initialNetProfit = CompanyCalculationMath.finite((double)initialRevenue - initialCost);
        initialNetMargin = CompanyCalculationMath.safeDivide(initialNetProfit,
            CompanyCalculationMath.safeDenominator(initialRevenue, initialRevenue));
        initialAssetValue = CompanyCalculationMath.finite((double)definition.initialCash
            + definition.initialTangibleAssets * (double)tangibleCoefficient
            + definition.initialIntangibleAssets * (double)intangibleCoefficient - definition.initialDebt);
        float initialTotalAssets = CompanyCalculationMath.finite((double)definition.initialCash
            + definition.initialTangibleAssets + definition.initialIntangibleAssets);
        initialDebtRatio = CompanyCalculationMath.safeDivide(definition.initialDebt,
            CompanyCalculationMath.safeDenominator(initialTotalAssets, initialAssetValue));
        initialLiquidityStrength = CompanyCalculationMath.safeDivide(definition.initialCash,
            CompanyCalculationMath.safeDenominator(initialCost, initialCost));

        beforeRevenue = initialRevenue;
        beforeNetProfit = initialNetProfit;
        beforeNetMargin = initialNetMargin;
    }
    
    public float getStat(CompanyStat stat) { return stats[stat]; }
    public bool tryGetStat(string statName, out float returnStat)
    {
        CompanyStat stat;
        if (System.Enum.TryParse(statName, out stat))
            return stats.TryGetValue(stat, out returnStat);

        CompanyDerivedStat derivedStat;
        if (System.Enum.TryParse(statName, out derivedStat))
            return derivedStats.TryGetValue(derivedStat, out returnStat);

        returnStat = 0f;
        return false;
    }

    public void setStat(CompanyStat stat, float value) { stats[stat] = value; }
    public void addStat(CompanyStat stat, float value)
    {
        stats[stat] += value;
    }

    public float getDerivedStat(CompanyDerivedStat derivedStat) { return derivedStats[derivedStat]; }
    public void setDerivedStat(CompanyDerivedStat derivedStat, float value) { derivedStats[derivedStat] = value; }
}
