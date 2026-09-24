
public enum CompanyStat
{
    // RawData
    REVENUE,
    COST,

    CASH,
    DEBT,

    TANGIBLE_ASSETS,
    INTANGIBLE_ASSTES,

    REPUTATION_B2C,
    REPUTATION_B2B,
    REPUTAION_PLAYER,
    MARKET_POSITION,

    PLAYER_SHARES,
    BASE_COMPANY_VALUE
    // 복잡성을 위한 상호작용 등에서 영향을 주고받을 rawdata를 데이터를 추가할 수 있음.
    // ex) Liquidity, CashFlowStability, OperationalRisk, ManagementStability 등
    // 상위 계층의 Factors, Martrix로 옮겨감
}
