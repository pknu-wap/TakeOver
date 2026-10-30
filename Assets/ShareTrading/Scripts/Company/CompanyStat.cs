
public enum CompanyStat
{
    // RawData
    REVENUE,            // 매출
    COST,               // 지출

    CASH,               // 현금
    DEBT,               // 부채

    TANGIBLE_ASSETS,    // 유형자산
    INTANGIBLE_ASSTES,  // 무형자산 

    REPUTATION_B2C,     // 이 기업에 대한 소비자들의 평판·호감도 [0~100 기준]
    REPUTATION_B2B,     // 이 기업에 대한 기업들의 평판·호감도 [0~100 기준]
    REPUTAION_PLAYER,   // 플레이어에 대한 해당 회사의 평판·호감도 [미정, 0~100 기준]
    MARKET_POSITION,    // 시장 경쟁력 [0~100 기준]

    PLAYER_SHARES,
    BASE_COMPANY_VALUE  // 기초값
    // 복잡성을 위한 상호작용 등에서 영향을 주고받을 rawdata를 데이터를 추가할 수 있음.
    // ex) Liquidity, CashFlowStability, OperationalRisk, ManagementStability 등
}
