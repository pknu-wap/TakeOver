using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

public sealed class EventStockmanipulationAccept : MonoBehaviour
{
    // 주가조작 제안
    [SerializeField] private Companies companies;
    [SerializeField] private Marketdata marketData;
    private GameTurnClock turnClock;

    private readonly List<(Company company, int executionDay, float value)> scheduledEffects
        = new List<(Company company, int executionDay, float value)>();

    private void Awake()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
    }

    private void OnDestroy()
    {
        if (turnClock != null) turnClock.TurnAdvanced -= executeScheduledEffects;
    }

    // 0은 즉발, 1은 내일. 임시 30%
    public bool applyEventEffect(string companyID, int daysLater, float value = 0.3f)
    {
        Company company = companies.getCompany(companyID);
        if (company == null) return false;
        if (daysLater == 0) return applyEffect(company, value);

        scheduledEffects.Add((company, turnClock.CurrentTurn + daysLater, value));
        if (scheduledEffects.Count == 1) turnClock.TurnAdvanced += executeScheduledEffects;
        return true;
    }

    private void executeScheduledEffects(int currentDay)
    {
        for (int i = 0; i < scheduledEffects.Count;)
        {
            (Company company, int executionDay, float value) effect = scheduledEffects[i];
            if (effect.executionDay > currentDay)
            {
                i++;
                continue;
            }

            scheduledEffects.RemoveAt(i);
            applyEffect(effect.company, effect.value);
        }

        if (scheduledEffects.Count == 0) turnClock.TurnAdvanced -= executeScheduledEffects;
    }

    private bool applyEffect(Company company, float value)
    {
        company.marketState.applyPriceRise(value);
        company.calculator.calculateMarketValue(company, marketData);
        return true;
    }
}
