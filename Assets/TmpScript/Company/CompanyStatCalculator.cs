using UnityEngine;

public class CompanyStatCalculator
{
    public void calculateInitializeStat(Company company, TmpMarketData marketData)
    {
        calculateMetrics(company);
        calculateFactors(company);
        calculateFundamentalValue(company);
        calculateMarketValue(company, marketData);
        calculateAboutShares(company);
    }

    public void calculateTurnend(Company company, TmpMarketData marketData)
    {
        calculateMetrics(company);
        calculateFactors(company);
        calculateFundamentalValue(company);
        calculateMarketValue(company, marketData);
        calculateAboutShares(company);
        // calculateHistoryStat(company);
    }

    public void calculateMetrics(Company company)
    {
        CompanyState state = company.state;
        // CompanyState 생성자에는 별도 예외처리가 없어 계산식을 그대로 가져옴.


        // 주의: initialRevenue 가 0 이하면 오류가 생길 수 있음. 거기까지고려안함
        //          매출, 비용도 0이면 오류가 있을 수 있음
        // NetProfit
        state.setDerivedStat(CompanyDerivedStat.NET_PROFIT,
            state.getStat(CompanyStat.REVENUE) - state.getStat(CompanyStat.COST));
        // NetMargin
        state.setDerivedStat(CompanyDerivedStat.NET_MARGIN,
            state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) / state.getStat(CompanyStat.REVENUE));
        // RevenueGrowth
        state.setDerivedStat(CompanyDerivedStat.REVENUE_GROWTH,
            (state.getStat(CompanyStat.REVENUE) - state.beforeRevenue) /
            (state.beforeRevenue == 0f ? state.initialRevenue : Mathf.Abs(state.beforeRevenue)));
        // NetProfitGrowth
        state.setDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH,
            (state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) - state.beforeNetProfit) /
            (state.beforeNetProfit == 0f ? state.initialRevenue : Mathf.Abs(state.beforeNetProfit)));
        // NetmarginTrend
        state.setDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND,
            state.getDerivedStat(CompanyDerivedStat.NET_MARGIN) - state.beforeNetMargin);
        // TotalAssets
        state.setDerivedStat(CompanyDerivedStat.TOTAL_ASSETS,
            state.getStat(CompanyStat.CASH) +
            state.getStat(CompanyStat.TANGIBLE_ASSETS) +
            state.getStat(CompanyStat.INTANGIBLE_ASSTES));
        // DebtRatio
        state.setDerivedStat(CompanyDerivedStat.DEBT_RATIO,
            state.getStat(CompanyStat.DEBT) / state.getDerivedStat(CompanyDerivedStat.TOTAL_ASSETS));
        // LiquidityStrength
        state.setDerivedStat(CompanyDerivedStat.LIQUIDITY_STRENGTH,
            state.getStat(CompanyStat.CASH) / state.getStat(CompanyStat.COST));
    }

    public void calculateFactors(Company company)
    {
        CompanyState state = company.state;
        state.setDerivedStat(CompanyDerivedStat.ASSET_VALUE,
            state.getDerivedStat(CompanyDerivedStat.TOTAL_ASSETS)); // 임시로 ASSET_VALUE = TOTAL_ASSETS
        state.setDerivedStat(CompanyDerivedStat.EARNINGS_POWER,
            state.getStat(CompanyStat.REVENUE) / state.initialRevenue * 0.4f +
                (1f + (state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) - state.initialNetProfit)
                    / state.initialRevenue) * 0.3f +
                (1f + state.getDerivedStat(CompanyDerivedStat.NET_MARGIN)
                    - state.initialNetMargin) * 0.3f);
        state.setDerivedStat(CompanyDerivedStat.PERFORMANCE_TREND,
            1f +
            state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH) * 0.2f +
            state.getDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH) * 0.5f +
            state.getDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND) * state.netMarginTrendCoefficient); // Clamp?
        state.setDerivedStat(CompanyDerivedStat.FINANCIAL_STRENGTH,
            (1f + state.initialDebtRatio) /
                (1f + state.getDerivedStat(CompanyDerivedStat.DEBT_RATIO)) * 0.5f +
            state.getDerivedStat(CompanyDerivedStat.LIQUIDITY_STRENGTH) /
                state.initialLiquidityStrength * 0.5f);
        state.setDerivedStat(CompanyDerivedStat.ASSET_FACTOR,
            state.getDerivedStat(CompanyDerivedStat.ASSET_VALUE) / state.initialAssetValue);
    }

    public void calculateFundamentalValue(Company company)
    {
        CompanyState state = company.state;
        float fundamentalRatio =
            state.getDerivedStat(CompanyDerivedStat.EARNINGS_POWER) * 0.4f +
            state.getDerivedStat(CompanyDerivedStat.PERFORMANCE_TREND) * 0.2f +
            state.getDerivedStat(CompanyDerivedStat.FINANCIAL_STRENGTH) * 0.2f +
            state.getDerivedStat(CompanyDerivedStat.ASSET_FACTOR) * 0.2f;
        state.setDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE,
            state.getStat(CompanyStat.BASE_COMPANY_VALUE) * fundamentalRatio);
    }

    public void calculateMarketValue(Company company, TmpMarketData marketData)
    {
        CompanyState state = company.state;
        state.setDerivedStat(CompanyDerivedStat.MARKET_EVALUATION, 
            marketData.getMarketEvaluation(company)); // 임시값. 직접 수정 필요
        state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE,
            state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE) *
            state.getDerivedStat(CompanyDerivedStat.MARKET_EVALUATION) *
            marketData.marketLiquidity);
        state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE,
            state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE) / state.totalShares);
    }

    public void calculateAboutShares(Company company)
    {
        CompanyState state = company.state;
        state.setDerivedStat(CompanyDerivedStat.REMAIN_SHARES,
            state.totalShares - state.getStat(CompanyStat.PLAYER_SHARES));
        state.setDerivedStat(CompanyDerivedStat.STAKE,
            state.getStat(CompanyStat.PLAYER_SHARES) / state.totalShares * 100);
    }

    public void calculateHistoryStat(Company company)
    {
        int historyCount = company.history.History.Count;
        if (historyCount == 0) {historyCount = 1;}
        float tmp = 0f;
        foreach (CompanyHistoryEntry entry in company.history.History)
        {
            tmp += entry.revenue;
        }
        company.state.beforeRevenue = tmp / historyCount;

        tmp = 0f;
        foreach (CompanyHistoryEntry entry in company.history.History)
        {
            tmp += entry.netProfit;
        }
        company.state.beforeNetProfit = tmp / historyCount;

        tmp = 0f;
        foreach (CompanyHistoryEntry entry in company.history.History)
        {
            tmp += entry.netMargin;
        }
        company.state.beforeNetMargin = tmp / historyCount;
    }
}
