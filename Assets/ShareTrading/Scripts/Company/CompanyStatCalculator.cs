using UnityEngine;

public class CompanyStatCalculator
{
    public void calculateInitializeStat(Company company, TmpMarketData marketData)
    {
        recalculate(company, marketData);
    }

    public void calculateTurnend(Company company, TmpMarketData marketData)
    {
        recalculate(company, marketData);
    }

    public void recalculate(Company company, TmpMarketData marketData)
    {
        calculateHistoryStat(company);
        calculateMetrics(company);
        calculateFactors(company);
        calculateFundamentalValue(company);
        calculateMarketValue(company, marketData);
        calculateAboutShares(company);
    }

    public void calculateMetrics(Company company)
    {
        CompanyState state = company.state;
        float revenue = state.getStat(CompanyStat.REVENUE);
        float cost = state.getStat(CompanyStat.COST);

        state.setDerivedStat(CompanyDerivedStat.NET_PROFIT,
            CompanyCalculationMath.finite((double)revenue - cost));
        state.setDerivedStat(CompanyDerivedStat.NET_MARGIN,
            CompanyCalculationMath.safeDivide(state.getDerivedStat(CompanyDerivedStat.NET_PROFIT),
                CompanyCalculationMath.safeDenominator(revenue, state.initialRevenue)));
        state.setDerivedStat(CompanyDerivedStat.REVENUE_GROWTH,
            CompanyCalculationMath.safeDivide((double)revenue - state.beforeRevenue,
                CompanyCalculationMath.safeDenominator(state.beforeRevenue, state.initialRevenue)));
        state.setDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH,
            CompanyCalculationMath.safeDivide((double)state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) - state.beforeNetProfit,
                CompanyCalculationMath.safeDenominator(state.beforeNetProfit, state.initialRevenue)));
        state.setDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND,
            CompanyCalculationMath.finite((double)state.getDerivedStat(CompanyDerivedStat.NET_MARGIN) - state.beforeNetMargin));
        state.setDerivedStat(CompanyDerivedStat.TOTAL_ASSETS,
            CompanyCalculationMath.finite((double)state.getStat(CompanyStat.CASH)
                + state.getStat(CompanyStat.TANGIBLE_ASSETS) + state.getStat(CompanyStat.INTANGIBLE_ASSTES)));
        state.setDerivedStat(CompanyDerivedStat.ASSET_VALUE,
            CompanyCalculationMath.finite((double)state.getStat(CompanyStat.CASH)
                + state.getStat(CompanyStat.TANGIBLE_ASSETS) * (double)state.tangibleCoefficient
                + state.getStat(CompanyStat.INTANGIBLE_ASSTES) * (double)state.intangibleCoefficient
                - state.getStat(CompanyStat.DEBT)));
        state.setDerivedStat(CompanyDerivedStat.DEBT_RATIO,
            CompanyCalculationMath.safeDivide(state.getStat(CompanyStat.DEBT),
                CompanyCalculationMath.safeDenominator(state.getDerivedStat(CompanyDerivedStat.TOTAL_ASSETS), state.initialAssetValue)));
        state.setDerivedStat(CompanyDerivedStat.LIQUIDITY_STRENGTH,
            CompanyCalculationMath.safeDivide(state.getStat(CompanyStat.CASH),
                CompanyCalculationMath.safeDenominator(cost, state.initialCost)));
    }

    public void calculateFactors(Company company)
    {
        CompanyState state = company.state;
        float revenueFactor = CompanyCalculationMath.clampFactor(
            (double)state.getStat(CompanyStat.REVENUE) / state.initialRevenue, 0.25f, 2f);
        float profitScale = CompanyCalculationMath.safeDenominator(state.initialNetProfit, state.initialRevenue);
        float profitFactor = CompanyCalculationMath.clampFactor(1d
            + ((double)state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) - state.initialNetProfit) / profitScale, 0.25f, 2f);
        float marginScale = Mathf.Max(Mathf.Abs(state.initialNetMargin), 0.1f);
        float marginFactor = CompanyCalculationMath.clampFactor(1d + 0.5d
            * ((double)state.getDerivedStat(CompanyDerivedStat.NET_MARGIN) - state.initialNetMargin) / marginScale, 0.25f, 2f);
        state.setDerivedStat(CompanyDerivedStat.EARNINGS_POWER,
            CompanyCalculationMath.clampFactor(revenueFactor * 0.4f + profitFactor * 0.35f + marginFactor * 0.25f, 0.25f, 2f));

        float revenueTrendFactor = 1f + CompanyCalculationMath.clamp(
            state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH), -0.5f, 0.5f);
        float profitTrendFactor = 1f + 0.5f * CompanyCalculationMath.clamp(
            state.getDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH), -1f, 1f);
        float marginTrendFactor = 1f + 0.5f * CompanyCalculationMath.clamp(
            state.getDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND) / 0.1d, -1f, 1f);
        state.setDerivedStat(CompanyDerivedStat.PERFORMANCE_TREND,
            CompanyCalculationMath.clampFactor(
                revenueTrendFactor * 0.3f + profitTrendFactor * 0.5f + marginTrendFactor * 0.2f, 0.5f, 1.5f));

        float debtFactor = CompanyCalculationMath.clampFactor(CompanyCalculationMath.safeDivide(
            1d + state.initialDebtRatio,
            CompanyCalculationMath.positiveDenominator(1d + state.getDerivedStat(CompanyDerivedStat.DEBT_RATIO)), 1f), 0.5f, 1.5f);
        float liquidityFactor = CompanyCalculationMath.clampFactor(CompanyCalculationMath.safeDivide(
            1d + state.getDerivedStat(CompanyDerivedStat.LIQUIDITY_STRENGTH),
            CompanyCalculationMath.positiveDenominator(1d + state.initialLiquidityStrength), 1f), 0.5f, 1.5f);
        state.setDerivedStat(CompanyDerivedStat.FINANCIAL_STRENGTH,
            CompanyCalculationMath.clampFactor(debtFactor * 0.6f + liquidityFactor * 0.4f, 0.5f, 1.5f));

        float assetScale = CompanyCalculationMath.safeDenominator(state.initialAssetValue, state.initialRevenue, 0.5f);
        state.setDerivedStat(CompanyDerivedStat.ASSET_FACTOR,
            CompanyCalculationMath.clampFactor(1d
                + ((double)state.getDerivedStat(CompanyDerivedStat.ASSET_VALUE) - state.initialAssetValue) / assetScale, 0.25f, 2f));
    }

    public void calculateFundamentalValue(Company company)
    {
        CompanyState state = company.state;
        float fundamentalRatio = CompanyCalculationMath.clampFactor(
            state.getDerivedStat(CompanyDerivedStat.EARNINGS_POWER) * 0.4f +
            state.getDerivedStat(CompanyDerivedStat.PERFORMANCE_TREND) * 0.2f +
            state.getDerivedStat(CompanyDerivedStat.FINANCIAL_STRENGTH) * 0.2f +
            state.getDerivedStat(CompanyDerivedStat.ASSET_FACTOR) * 0.2f);
        state.setDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE,
            CompanyCalculationMath.finite(state.getStat(CompanyStat.BASE_COMPANY_VALUE) * (double)fundamentalRatio));
    }

    public void calculateMarketValue(Company company, TmpMarketData marketData)
    {
        CompanyState state = company.state;
        state.setDerivedStat(CompanyDerivedStat.MARKET_EVALUATION,
            company.marketState.getMarketEvaluation(state, marketData.globalMarketScore));
        float liquidityPriceFactor = 1f
            + (company.marketState.getMarketLiquidity(state, marketData.globalMarketScore) - 1f) * 0.1f;
        state.setDerivedStat(CompanyDerivedStat.MARKET_VALUE,
            CompanyCalculationMath.finite((double)state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE)
                * state.getDerivedStat(CompanyDerivedStat.MARKET_EVALUATION) * liquidityPriceFactor));
        state.setDerivedStat(CompanyDerivedStat.SHARE_PRICE,
            CompanyCalculationMath.safeDivide(state.getDerivedStat(CompanyDerivedStat.MARKET_VALUE), state.totalShares));
    }

    public void calculateAboutShares(Company company)
    {
        CompanyState state = company.state;
        state.setDerivedStat(CompanyDerivedStat.REMAIN_SHARES,
            CompanyCalculationMath.finite((double)state.totalShares - state.getStat(CompanyStat.PLAYER_SHARES)));
        state.setDerivedStat(CompanyDerivedStat.STAKE,
            CompanyCalculationMath.safeDivide(state.getStat(CompanyStat.PLAYER_SHARES) * 100d, state.totalShares));
    }

    public void calculateHistoryStat(Company company)
    {
        CompanyState state = company.state;
        int count = company.history.History.Count;
        if (count == 0)
        {
            state.beforeRevenue = state.initialRevenue;
            state.beforeNetProfit = state.initialNetProfit;
            state.beforeNetMargin = state.initialNetMargin;
            return;
        }

        double revenue = 0d;
        double netProfit = 0d;
        double netMargin = 0d;
        foreach (CompanyHistoryEntry entry in company.history.History)
        {
            revenue += entry.revenue;
            netProfit += entry.netProfit;
            netMargin += entry.netMargin;
        }
        state.beforeRevenue = CompanyCalculationMath.finite(revenue / count, state.initialRevenue);
        state.beforeNetProfit = CompanyCalculationMath.finite(netProfit / count, state.initialNetProfit);
        state.beforeNetMargin = CompanyCalculationMath.finite(netMargin / count, state.initialNetMargin);
    }
}
