using UnityEngine;
using TakeOver.NPC;

public class TmpGameturnSystem : MonoBehaviour
{
    [SerializeField] private Companies companies;
    [SerializeField] private TmpMarketData marketData;
    private GameTurnClock turnClock;

    private void Awake()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
    }

    private void OnEnable()
    {
        if (turnClock == null)
        {
            Debug.LogError("GameTurnClock을 찾을 수 없습니다.", this);
            return;
        }

        turnClock.TurnAdvanced += handleTurnAdvanced;
    }

    private void OnDisable()
    {
        if (turnClock != null) turnClock.TurnAdvanced -= handleTurnAdvanced;
    }

    private void Start()
    {
        foreach(Company company in companies.CompanyList)
        {
            company.calculator.calculateInitializeStat(company, marketData);
        }
    }

    private void handleTurnAdvanced(int newTurn)
    {
        // TurnAdvanced는 턴 증가 후 호출되므로 종료된 턴 번호로 이력을 기록한다.
        int endedTurn = newTurn - 1;

        foreach(Company company in companies.CompanyList)
        {
            company.calculator.calculateTurnend(company, marketData);
        }

        saveAllCompanyHistory(endedTurn);

        Debug.Log($"{endedTurn}턴 종료");

        marketData.updateTurn(newTurn);

        foreach (Company company in companies.CompanyList)
        {
            company.calculator.recalculate(company, marketData);
        }
    }

    private void saveAllCompanyHistory(int endedTurn)
    {
        foreach (Company company in companies.CompanyList)
        {
            company.history.recordHistory(endedTurn);
        }
    }
}
