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
        stats[CompanyStat.MARKET_POSITION] = definition.initialMarketPosition; // 현재 미사용
        stats[CompanyStat.REPUTAION_PLAYER] = definition.initialReputaionPlayer;
        stats[CompanyStat.PLAYER_SHARES] = definition.initialPlayerShares;
        stats[CompanyStat.BASE_COMPANY_VALUE] = definition.initialBaseCompanyValue;

        totalShares = definition.totalShares;

        marketLiquidity = 1f; // 임시값. TmpMarketData에 1f로 지정되어있으며, 현재 변동되는 로직 없음
        netMarginTrendCoefficient = 1f; // 이건 뭔값이야 왜넣었는지 기억안남, PerfomanceTrend에 사용되는 계수임.
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
