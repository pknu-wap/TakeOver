using UnityEngine;

[CreateAssetMenu(fileName = "CompanyDefinition", menuName = "Company/CompanyDefinition")]
public class CompanyDefinition : ScriptableObject
{
    public string companyName;
    public string companyID;

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
}
