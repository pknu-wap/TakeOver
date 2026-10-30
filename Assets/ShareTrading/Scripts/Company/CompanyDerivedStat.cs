
public enum CompanyDerivedStat
{
    // Mertircs
    NET_PROFIT,
    NET_MARGIN,
    REVENUE_GROWTH,
    NET_PROFIT_GROWTH,
    NET_MARGIN_TREND,
    DEBT_RATIO,
    LIQUIDITY_STRENGTH,
    TOTAL_ASSETS,

    // Fundamental Factors
    EARNINGS_POWER,
    PERFORMANCE_TREND,
    FINANCIAL_STRENGTH,
    ASSET_VALUE,

    // etc
    MARKET_EVALUATION,
    ASSET_FACTOR,

    // Values
    FUNDAMENTAL_VALUE,
    MARKET_VALUE,
    SHARE_PRICE,            // 최종 주당 주가

    // OwnerShip
    REMAIN_SHARES,          // int 플레이어가 보유한 주식 수
    STAKE                   // float 지분율
}
