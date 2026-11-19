using System;
using UnityEngine;

namespace TakeOver.Startups
{
    [CreateAssetMenu(fileName = "StartupSettings", menuName = "Company/Startup Settings")]
    public sealed class StartupSettings : ScriptableObject
    {
        [Header("만들기")]
        [Tooltip("게임 시작 때 만드는 스타트업 수. 상장이나 파산으로 줄어도 다시 채우지 않는다.")]
        [Min(0)] public int initialCount = 10;

        [Tooltip("0이면 실행할 때마다 다른 스타트업이 나온다. 0이 아니면 항상 같은 스타트업이 나온다 (검증용).")]
        public int randomSeed = 0;

        [Tooltip("스타트업 ID 앞머리. 뒤에 01, 02 ... 가 붙는다 (예: startup-01).")]
        public string idPrefix = "startup-";

        [Header("파산 기준 (임시)")]
        [Tooltip("보유 자금이 이 값 이하이면 '현금이 바닥났다'로 본다.")]
        public float bankruptcyCash = 0f;
        [Tooltip("순자산(현금 + 유형자산 + 무형자산 - 부채)이 이 값 이하이면 파산 조건을 채운다.")]
        public float bankruptcyNetAssets = 0f;

        [Header("결재 문서 (임시 값, 밸런스 때 조정)")]
        [Tooltip("자금 요청 금액 범위. 문서가 올라올 때 이 안에서 무작위로 정한다 (x = 최소, y = 최대).")]
        public Vector2 fundingAmount = new Vector2(200f, 400f);
        [Tooltip("자금 요청 문서의 처리 기한 (턴). 기한이 지나면 하루 넘기기가 막힌다.")]
        [Min(1)] public int fundingDeadlineTurns = 7;

        [Tooltip("매출이 이 값 이상이면 상장 추진 문서가 올라온다. 플레이어에게는 보여 주지 않는다.")]
        public float listingRevenue = 800f;
        [Tooltip("기업가치가 이 값 이상이면 상장 추진 문서가 올라온다 (매출 기준과 둘 중 하나만 채우면 된다).")]
        public float listingCompanyValue = 700f;
        [Tooltip("상장 추진 문서의 처리 기한 (턴)")]
        [Min(1)] public int listingDeadlineTurns = 7;
        [Tooltip("상장 추진을 거절하면 이 턴 수 뒤에 다시 올라온다 (한 달 = 7턴, 1년 84턴 기준)")]
        [Min(1)] public int listingRetryTurns = 7;
        [Tooltip("계약 후 이 턴 수가 지나면 상장을 거절할 수 없다 (문서 없이 바로 상장). 계약 이벤트가 없어 지금은 게임 시작부터 센다.")]
        [Min(0)] public int listingNoRejectTurns = 180;
        [Tooltip("상장하는 순간 넣는 배당 비율 (0.3 = 30%)")]
        [Range(0f, 1f)] public float listedDividendRatio = 0.3f;

        [Tooltip("후속 투자 문서가 올라오는 주기 범위 (턴). 스타트업마다 이 안에서 무작위로 다음 날짜를 정한다 (x = 최소, y = 최대).")]
        public Vector2Int followUpIntervalTurns = new Vector2Int(21, 42);
        [Tooltip("기업가치가 이 값 이상인 스타트업만 후속 투자를 받는다.")]
        public float followUpMinCompanyValue = 300f;
        [Tooltip("후속 투자금 범위. 문서가 올라올 때 이 안에서 무작위로 정한다 (x = 최소, y = 최대).")]
        public Vector2 followUpAmount = new Vector2(100f, 300f);
        [Tooltip("후속 투자 문서의 처리 기한 (턴)")]
        [Min(1)] public int followUpDeadlineTurns = 7;

        [Header("연간 사업 계획 (임시 값, 밸런스 때 조정)")]
        [Tooltip("연간 사업 계획이 올라오는 주기 (턴). 1년 = 84턴")]
        [Min(1)] public int businessPlanPeriodTurns = 84;
        [Tooltip("첫 연간 사업 계획이 올라오는 날짜 범위 (턴). 1 ~ 이 값 사이에서 회사마다 무작위로 정한다")]
        [Min(1)] public int businessPlanFirstMaxTurns = 21;
        [Tooltip("연간 사업 계획 문서의 처리 기한 (턴)")]
        [Min(1)] public int businessPlanDeadlineTurns = 7;
        [Tooltip("채용 소폭: 지금 직원 수 대비 늘리는 비율 (0.1 = 10%)")]
        public float hireSmallRatio = 0.1f;
        [Tooltip("채용 대폭: 지금 직원 수 대비 늘리는 비율")]
        public float hireLargeRatio = 0.25f;
        [Tooltip("마케팅 보통: 회사 현금에서 나가는 비용")]
        public float marketingNormalCost = 50f;
        [Tooltip("마케팅 집중: 회사 현금에서 나가는 비용")]
        public float marketingFocusCost = 120f;
        [Tooltip("마케팅 보통: 평판 0일 때의 소비자 평판 상승량. 평판이 높을수록 덜 오른다")]
        public float marketingNormalGain = 10f;
        [Tooltip("마케팅 집중: 평판 0일 때의 소비자 평판 상승량")]
        public float marketingFocusGain = 25f;
        [Tooltip("운영 기조 공격: 직원 변화 비율")]
        public float aggressiveEmployeeRatio = 0.1f;
        [Tooltip("운영 기조 공격: 소비자 평판 변화")]
        public float aggressiveReputation = -3f;
        [Tooltip("운영 기조 보수: 직원 변화 비율")]
        public float conservativeEmployeeRatio = -0.05f;
        [Tooltip("운영 기조 보수: 소비자 평판 변화")]
        public float conservativeReputation = 3f;
        [Tooltip("시설 투자 중간: 투자금 (고정 금액)")]
        public float facilityMediumCost = 150f;
        [Tooltip("시설 투자 대규모: 투자금 (고정 금액)")]
        public float facilityLargeCost = 400f;
        [Tooltip("시설 투자 100원당 붙는 실적 주기 성장률 보너스 (0.01 = 1%p)")]
        public float facilityGrowthPer100 = 0.01f;
        [Tooltip("시설 투자 후 보너스가 붙기까지 걸리는 턴 (2달 = 14턴)")]
        [Min(0)] public int facilityBonusDelayTurns = 14;
        [Tooltip("보너스가 이어지는 턴. 실적은 7턴마다 갱신되므로 이 기간 안에 든 실적 갱신에만 보너스가 붙는다")]
        [Min(1)] public int facilityBonusDurationTurns = 21;

        [Header("화면")]
        [Tooltip("스타트업 탭의 매출 그래프에 보여줄 주 수 (이번 주 + 지난 N주)")]
        [Min(1)] public int revenueHistoryWeeks = 8;

        [Header("이름 목록")]
        public string[] names =
        {
            "노바랩스", "그린셀", "온결", "루미브릿지",
            "모아핀", "델타웨이브", "솔라피크", "바로잇",
            "픽스랩", "아이언리프", "테라큐브", "스윗포트",
            "블루닷랩", "하루온", "코어링크", "제로마일"
        };

        [Header("업종 틀 (범위는 x = 최소, y = 최대)")]
        public IndustryTemplate[] industries =
        {
            new IndustryTemplate
            {
                industry = "AI 물류", coreValue = "속도와 자동화",
                description = "물류 창고의 분류와 배송 경로를 AI로 줄여 주는 회사.",
                revenue = new Vector2(250f, 450f), cash = new Vector2(500f, 900f), debt = new Vector2(0f, 100f),
                tangibleAssets = new Vector2(20f, 60f), intangibleAssets = new Vector2(60f, 160f),
                baseCompanyValue = new Vector2(150f, 350f), employees = new Vector2Int(10, 25),
                growthRate = new Vector2(0.01f, 0.10f)
            },
            new IndustryTemplate
            {
                industry = "배터리 소재", coreValue = "기술과 양산",
                description = "전기차 배터리에 들어가는 소재를 만드는 회사.",
                revenue = new Vector2(200f, 400f), cash = new Vector2(600f, 1100f), debt = new Vector2(50f, 200f),
                tangibleAssets = new Vector2(80f, 200f), intangibleAssets = new Vector2(40f, 100f),
                baseCompanyValue = new Vector2(200f, 450f), employees = new Vector2Int(15, 30),
                growthRate = new Vector2(0f, 0.09f)
            },
            new IndustryTemplate
            {
                industry = "진단 키트", coreValue = "정확도와 신뢰",
                description = "병원 밖에서도 쓸 수 있는 간편 진단 키트를 만드는 회사.",
                revenue = new Vector2(150f, 350f), cash = new Vector2(700f, 1200f), debt = new Vector2(0f, 150f),
                tangibleAssets = new Vector2(30f, 80f), intangibleAssets = new Vector2(100f, 250f),
                baseCompanyValue = new Vector2(200f, 500f), employees = new Vector2Int(10, 25),
                growthRate = new Vector2(-0.01f, 0.12f)
            },
            new IndustryTemplate
            {
                industry = "핀테크", coreValue = "편의와 보안",
                description = "소상공인의 결제와 정산을 한 번에 처리해 주는 회사.",
                revenue = new Vector2(300f, 500f), cash = new Vector2(400f, 800f), debt = new Vector2(0f, 100f),
                tangibleAssets = new Vector2(10f, 40f), intangibleAssets = new Vector2(80f, 200f),
                baseCompanyValue = new Vector2(180f, 400f), employees = new Vector2Int(10, 20),
                growthRate = new Vector2(0f, 0.08f)
            },
            new IndustryTemplate
            {
                industry = "푸드테크", coreValue = "맛과 원가",
                description = "식당 주방의 조리 과정을 자동화하는 회사.",
                revenue = new Vector2(250f, 480f), cash = new Vector2(400f, 800f), debt = new Vector2(50f, 150f),
                tangibleAssets = new Vector2(50f, 120f), intangibleAssets = new Vector2(30f, 90f),
                baseCompanyValue = new Vector2(120f, 300f), employees = new Vector2Int(15, 30),
                growthRate = new Vector2(-0.01f, 0.07f)
            }
        };

        [Serializable]
        public sealed class IndustryTemplate
        {
            public string industry = "";
            public string coreValue = "";
            [TextArea(2, 4)] public string description = "";

            public Vector2 revenue = new Vector2(200f, 450f);
            [Tooltip("처음 비용 = 매출 × 이 값. 첫 실적 갱신(7턴) 뒤에는 실적 규칙이 다시 계산한다.")]
            public float initialCostRatio = 1.15f;
            public Vector2 cash = new Vector2(400f, 900f);
            public Vector2 debt = new Vector2(0f, 100f);
            public Vector2 tangibleAssets = new Vector2(20f, 80f);
            public Vector2 intangibleAssets = new Vector2(40f, 150f);
            public Vector2 baseCompanyValue = new Vector2(150f, 400f);
            public Vector2Int employees = new Vector2Int(10, 25);
            [Tooltip("회사 고유 성장률 (7턴마다 매출이 늘어나는 비율). 0.05 = 5%")]
            public Vector2 growthRate = new Vector2(0.03f, 0.08f);
        }
    }
}
