using UnityEngine;

[CreateAssetMenu(fileName = "CompanyDefinition", menuName = "Company/CompanyDefinition")]
public class CompanyDefinition : ScriptableObject
{
    public string companyName;
    public string companyID;

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
