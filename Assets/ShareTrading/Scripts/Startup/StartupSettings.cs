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
