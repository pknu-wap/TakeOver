using System.Collections.Generic;
using UnityEngine;

public class TmpGameturnSystem : MonoBehaviour
{
    [SerializeField] private List<Company> companies;
    [SerializeField] private int currentTurn = 1;
    [SerializeField] private TmpMarketData marketData;

    private void Start()
    {
        foreach(Company company in companies)
        {
            company.calculator.calculateInitializeStat(company, marketData);
        }
    }

    public void endTurn()
    {
        foreach(Company company in companies)
        {
            company.calculator.calculateTurnend(company, marketData);
        }

        saveAllCompanyHistory();

        foreach(Company company in companies)
        {
            company.calculator.calculateHistoryStat(company);
        }
    
        Debug.Log($"{currentTurn}턴 종료");

        currentTurn++;
    }

    private void saveAllCompanyHistory()
    {
        foreach (Company company in companies)
        {
            company.history.recordHistory(currentTurn);
        }
    }
}
