using UnityEngine;

[CreateAssetMenu(fileName = "CompanyDefinition", menuName = "Company/CompanyDefinition")]
public class CompanyDefinition : ScriptableObject
{
    public string companyName;

    // public float basePrice;
    public float totalShares;
    public float initialBaseCompanyValue;

    public float initialRevenue;
    public float initialCost;
    public float initialCash;
    public float initialDebt;

    public float initialTangibleAssets;
    public float initialIntangibleAssets;

    public float initialReputationB2C;
    public float initialReputationB2B;
    public float initialReputaionPlayer;
    public float initialMarketPosition;

    public float initialPlayerShares;

    public float initialTangibleCoefficient;
    public float initialIntangibleCoefficient;
}