using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

public sealed class EventGreenmailAccept : MonoBehaviour
{
    // 그린메일 제안 수락
    [SerializeField] private Companies companies;
    [SerializeField] private ShareTradingSystem tradingSystem;
    [SerializeField] private Player player;
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

    // 0은 즉발, 1은 내일. 임시 웃돈 20%
    public bool applyEventEffect(string companyID, int daysLater, float value = 0.2f)
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
        int amount = (int)company.state.getStat(CompanyStat.PLAYER_SHARES);
        float premium = company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE) * amount * value;
        if (!tradingSystem.sellShares(company, amount)) return false;

        player.state.addCash(premium);
        return true;
    }
}
