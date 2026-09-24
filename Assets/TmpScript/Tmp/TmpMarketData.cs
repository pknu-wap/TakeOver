using UnityEngine;
using System.Collections.Generic;

public class TmpMarketData : MonoBehaviour
{
    [SerializeField] private List<Company> companyList;
    private Dictionary<Company, float> marketEvaluation;
    public float marketLiquidity { get; private set; } =1f;

    private void Awake()
    {
        marketEvaluation = new Dictionary<Company, float>();
        foreach (Company company in companyList)
        {
            marketEvaluation.Add(company, 1f);
        }
    }

    public float getMarketEvaluation(Company company) { return marketEvaluation[company]; }
    public void setMarketEvaluation(Company company, float value) { marketEvaluation[company] = value; }

    public void setMarketLiquidity(float value) { marketLiquidity = value; }
}
