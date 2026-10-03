using UnityEngine;

public class TmpGameturnSystem : MonoBehaviour
{
    [SerializeField] private Companies companies;
    [SerializeField] private int currentTurn = 1;
    [SerializeField] private TmpMarketData marketData;

    private void Start()
    {
        foreach(Company company in companies.CompanyList)
        {
            company.calculator.calculateInitializeStat(company, marketData);
        }
    }

    public void endTurn()
    {
        foreach(Company company in companies.CompanyList)
        {
            company.calculator.calculateTurnend(company, marketData);
        }

        saveAllCompanyHistory();

        foreach(Company company in companies.CompanyList)
        {
            company.calculator.calculateHistoryStat(company);
        }
    
        Debug.Log($"{currentTurn}턴 종료");

        currentTurn++;
    }

    private void saveAllCompanyHistory()
    {
        foreach (Company company in companies.CompanyList)
        {
            company.history.recordHistory(currentTurn);
        }
    }
}
