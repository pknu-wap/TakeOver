using UnityEngine;

[CreateAssetMenu(fileName = "CompanyDefinition", menuName = "Company/CompanyDefinition")]
public class CompanyDefinition : ScriptableObject
{
    public string companyName;
    public string companyID;

    // 스토리 기업이면 체크, 시장 기업이면 비워 둔다.
    // 스토리 기업은 도시맵(지도 노드 + 옆 작은 목록)에, 시장 기업은 기업 탭(큰 목록)에 나온다.
    // 코드로 만드는 스타트업은 자동으로 false(시장 기업)가 된다.
    [Tooltip("체크 = 스토리 기업(지도에 표시) / 해제 = 시장 기업(기업 탭에 표시)")]
    public bool isStoryCompany;

    // 지도 노드·기업 목록 등 UI에서 쓰는 회사 로고 (없으면 비워 둠)
    public Sprite logo;

    [Header("상세 패널 소개")]
    public string industry;        // 업종 (예: 건설·도시개발)
    public string coreValue;       // 핵심 가치 (예: 생존과 현실성)
    [TextArea(2, 5)]
    public string description;     // 회사 소개 한두 문장
    public Sprite coverImage;      // 상세 패널 왼쪽 위 대표 사진 (없으면 비워 둠)

    [Header("초기 수치")]
    // public float basePrice;
    public int totalShares = 1000;
    public float initialBaseCompanyValue;

    public float initialRevenue;
    public float initialCost;
    public float initialCash;
    public float initialDebt;

    public float initialTangibleAssets;
    public float initialIntangibleAssets;

    public float initialReputationB2C = 50f;
    public float initialReputationB2B = 50f;
    public float initialReputaionPlayer;
    public float initialMarketPosition = 50f;

    public int initialPlayerShares;

    public float initialTangibleCoefficient = 0.75f;
    public float initialIntangibleCoefficient = 0.75f;

    public float growthRate;
    public int initialEmployeeCount;
    public float dividendRatio;
}
