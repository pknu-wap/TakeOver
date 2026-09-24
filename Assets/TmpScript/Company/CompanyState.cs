using UnityEngine;
using System.Collections.Generic;

public class CompanyState
{
    private Dictionary<CompanyStat, float> stats;
    private Dictionary<CompanyDerivedStat, float> derivedStats;

    public float totalShares { get; private set; }

    // 이전값 변수 추가. 생성 시 현재값으로 초기화
    public float beforeRevenue { get; set; }
    public float beforeNetProfit { get; set; }
    public float beforeNetMargin { get; set; }

    // 계산에 필요한 초기값 변수 추가
    public float initialRevenue { get; private set; }
    public float initialNetProfit { get; private set; }
    public float initialNetMargin { get; private set; }
    public float initialDebtRatio { get; private set; }
    public float initialLiquidityStrength { get; private set; }
    public float initialAssetValue { get; private set; }

    // 아직 값이 정해지지 않은 임시 변수. 직접 수정 필요
    public float marketLiquidity { get; private set; }
    public float netMarginTrendCoefficient { get; private set; }
    // public float tangibleCoefficient { get; private set; } // 현재 미사용
    // public float intangibleCoefficient { get; private set; } // 현재 미사용

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

        totalShares = definition.totalShares;

        marketLiquidity = 1f; // 임시값. 직접 수정 필요
        netMarginTrendCoefficient = 1f; // 임시값. 직접 수정 필요
        // tangibleCoefficient = 1f; // 현재 미사용. ASSET_VALUE 정식 계산 시 사용
        // intangibleCoefficient = 1f; // 현재 미사용. ASSET_VALUE 정식 계산 시 사용

        initialRevenue = definition.initialRevenue;
        initialNetProfit = definition.initialRevenue - definition.initialCost;
        initialNetMargin = initialNetProfit / definition.initialRevenue;
        initialAssetValue =
            definition.initialCash +
            definition.initialTangibleAssets +
            definition.initialIntangibleAssets;
        initialDebtRatio = definition.initialDebt / initialAssetValue;
        initialLiquidityStrength = definition.initialCash / definition.initialCost;

        beforeRevenue = initialRevenue;
        beforeNetProfit = initialNetProfit;
        beforeNetMargin = initialNetMargin;

        /* 파생 스탯 초기화는 CompanyStatCalculator.calculateInitializeStat로 옮김
        derivedStats[CompanyDerivedStat.NET_PROFIT] =
            stats[CompanyStat.REVENUE] - stats[CompanyStat.COST];

        derivedStats[CompanyDerivedStat.NET_MARGIN] =
            derivedStats[CompanyDerivedStat.NET_PROFIT] / stats[CompanyStat.REVENUE];

        derivedStats[CompanyDerivedStat.REVENUE_GROWTH] =
            (stats[CompanyStat.REVENUE] - beforeRevenue) / beforeRevenue;
        derivedStats[CompanyDerivedStat.NET_PROFIT_GROWTH] =
            (derivedStats[CompanyDerivedStat.NET_PROFIT] - beforeNetProfit) / beforeNetProfit;
        derivedStats[CompanyDerivedStat.NET_MARGIN_TREND] =
            derivedStats[CompanyDerivedStat.NET_MARGIN] - beforeNetMargin;

        derivedStats[CompanyDerivedStat.TOTAL_ASSETS] =
            stats[CompanyStat.CASH] +
            stats[CompanyStat.TANGIBLE_ASSETS] +
            stats[CompanyStat.INTANGIBLE_ASSTES];
        derivedStats[CompanyDerivedStat.DEBT_RATIO] =
            stats[CompanyStat.DEBT] / derivedStats[CompanyDerivedStat.TOTAL_ASSETS];
        derivedStats[CompanyDerivedStat.LIQUIDITY_STRENGTH] =
            stats[CompanyStat.CASH] / stats[CompanyStat.COST];

        derivedStats[CompanyDerivedStat.ASSET_VALUE] =
            derivedStats[CompanyDerivedStat.TOTAL_ASSETS]; // 임시로 ASSET_VALUE = TOTAL_ASSETS
        */

        /* 파생 스탯 초기화는 CompanyStatCalculator.calculateInitializeStat에서 수행
        derivedStats[CompanyDerivedStat.EARNINGS_POWER] =
            stats[CompanyStat.REVENUE] / initialRevenue * 0.4f +
            derivedStats[CompanyDerivedStat.NET_PROFIT] / initialNetProfit * 0.3f +
            derivedStats[CompanyDerivedStat.NET_MARGIN] / initialNetMargin * 0.3f;

        derivedStats[CompanyDerivedStat.PERFORMANCE_TREND] =
            1f +
            derivedStats[CompanyDerivedStat.REVENUE_GROWTH] * 0.2f +
            derivedStats[CompanyDerivedStat.NET_PROFIT_GROWTH] * 0.5f +
            derivedStats[CompanyDerivedStat.NET_MARGIN_TREND] * netMarginTrendCoefficient; // Clamp?

        derivedStats[CompanyDerivedStat.FINANCIAL_STRENGTH] =
            initialDebtRatio / derivedStats[CompanyDerivedStat.DEBT_RATIO] * 0.5f +
            derivedStats[CompanyDerivedStat.LIQUIDITY_STRENGTH] / initialLiquidityStrength * 0.5f;

        derivedStats[CompanyDerivedStat.ASSET_FACTOR] =
            derivedStats[CompanyDerivedStat.ASSET_VALUE] / initialAssetValue;
        float fundamentalRatio =
            derivedStats[CompanyDerivedStat.EARNINGS_POWER] * 0.4f +
            derivedStats[CompanyDerivedStat.PERFORMANCE_TREND] * 0.2f +
            derivedStats[CompanyDerivedStat.FINANCIAL_STRENGTH] * 0.2f +
            derivedStats[CompanyDerivedStat.ASSET_FACTOR] * 0.2f;

        derivedStats[CompanyDerivedStat.MARKET_EVALUATION] = 1f; // 임시값. 직접 수정 필요
        derivedStats[CompanyDerivedStat.FUNDAMENTAL_VALUE] =
            stats[CompanyStat.BASE_COMPANY_VALUE] * fundamentalRatio;
        derivedStats[CompanyDerivedStat.MARKET_VALUE] =
            derivedStats[CompanyDerivedStat.FUNDAMENTAL_VALUE] *
            derivedStats[CompanyDerivedStat.MARKET_EVALUATION] *
            marketLiquidity;
        derivedStats[CompanyDerivedStat.SHARE_PRICE] =
            derivedStats[CompanyDerivedStat.MARKET_VALUE] / totalShares;
        derivedStats[CompanyDerivedStat.REMAIN_SHARES] =
            totalShares - stats[CompanyStat.PLAYER_SHARES];
        */
    }
    
    public float getStat(CompanyStat stat) { return stats[stat]; }
    public void setStat(CompanyStat stat, float value) { stats[stat] = value; }
    public void addStat(CompanyStat stat, float value)
    {
        stats[stat] += value;
    }

    public float getDerivedStat(CompanyDerivedStat derivedStat) { return derivedStats[derivedStat]; }
    public void setDerivedStat(CompanyDerivedStat derivedStat, float value) { derivedStats[derivedStat] = value; }

    
}   
