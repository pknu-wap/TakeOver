using System;
using System.Collections.Generic;

namespace TakeOver.Startups
{
    [Serializable]
    public sealed class StartupRecord
    {
        public string companyId;
        public string companyName;
        public string industry;
        public string coreValue;
        public string description;

        public int totalShares = 1000;
        public int playerShares;
        public float baseCompanyValue;
        public float revenue;
        public float cost;
        public float cash;
        public float debt;
        public float tangibleAssets;
        public float intangibleAssets;
        public float growthRate;
        public int employeeCount;

        public List<float> revenueHistory = new List<float>();

        public bool fundingRejected;
        public float cumulativeInvestment;

        public bool contracted;
        public int contractTurn;
        public int pendingContractDay;
        // 상장 추진 보고서를 보낸 뒤 상장할 턴. 0이면 아직 보고서를 보내지 않은 상태다.
        public int listingTurn;

        public int nextFollowUpTurn;

        // 다음 연간 사업 계획이 올라올 수 있는 턴. 0이면 아직 정하지 않은 상태다.
        public int nextBusinessPlanTurn;
        // 시설 투자 기록. 스타트업용 실적 규칙이 여기서 성장 보너스를 읽는다.
        public List<StartupFacilityInvestment> facilityInvestments = new List<StartupFacilityInvestment>();
    }

    // 시설 투자 한 건. startTurn부터 endTurn 전까지 실적 주기 성장률에 growthBonus가 더해진다.
    [Serializable]
    public sealed class StartupFacilityInvestment
    {
        public int startTurn;
        public int endTurn;
        public float growthBonus;
    }
}
