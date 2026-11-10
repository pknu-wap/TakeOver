using System;
using System.Collections.Generic;
using System.IO;
using TakeOver.SaveSystem;
using UnityEngine;

/// <summary>거래 상태와 저장 형식 사이의 변환·검증·복원 담당</summary>
public sealed class TradingSaveAdapter : MonoBehaviour, ISaveParticipant
{
    [SerializeField] private Player player;
    [SerializeField] private Companies companies;
    [SerializeField] private Marketdata marketData;
    [SerializeField] private TradingEventEffects eventEffects;
    private TradingSaveData prepared;

    public string saveId
    {
        get { return "trading"; }
    }

    public int saveVersion
    {
        get { return 1; }
    }

    public string captureJson()
    {
        resolveReferences();
        TradingSaveData data = new TradingSaveData
        {
            player = new TradingSaveData.PlayerData
            {
                money = player.state.cash, manPower = player.state.manPower,
                information = player.state.information, reputation = player.state.reputation
            },
            market = new TradingSaveData.MarketData
            {
                globalMarketScore = marketData.globalMarketScore, lastUpdatedTurn = marketData.LastUpdatedTurn
            },
            companies = new List<TradingSaveData.CompanyData>(),
            greenmail = captureEffects(eventEffects.GreenmailAccept.ScheduledEffects),
            stockManipulation = captureEffects(eventEffects.StockManipulationAccept.ScheduledEffects)
        };
        foreach (Company company in companies.CompanyList)
        {
            data.companies.Add(captureCompany(company));
        }
        return JsonUtility.ToJson(data);
    }

    private static TradingSaveData.CompanyData captureCompany(Company company)
    {
        CompanyState state = company.state;
        CompanyMarketState market = company.marketState;
        TradingSaveData.CompanyData data = new TradingSaveData.CompanyData
        {
            companyId = company.definition.companyID,
            state = new TradingSaveData.CompanyStateData
            {
                stats = new List<TradingSaveData.StatData>(), totalShares = state.totalShares,
                greenmailBuyBlocked = state.greenmailBuyBlocked,
                initialRevenue = state.initialRevenue, initialCost = state.initialCost,
                initialNetProfit = state.initialNetProfit, initialNetMargin = state.initialNetMargin,
                initialDebtRatio = state.initialDebtRatio, initialLiquidityStrength = state.initialLiquidityStrength,
                initialAssetValue = state.initialAssetValue,
                tangibleCoefficient = state.tangibleCoefficient, intangibleCoefficient = state.intangibleCoefficient
            },
            market = new TradingSaveData.CompanyMarketData
            {
                npcSentiment = market.npcSentiment, eventPriceMultiplier = market.eventPriceMultiplier,
                marketNoise = market.marketNoise, activeEvents = new List<TradingSaveData.MarketEffectData>()
            },
            history = new List<CompanyHistoryEntry>(company.history.History)
        };
        foreach (CompanyStat stat in Enum.GetValues(typeof(CompanyStat)))
        {
            data.state.stats.Add(new TradingSaveData.StatData { id = stat.ToString(), value = state.getStat(stat) });
        }
        foreach (CompanyMarketState.ActiveEventImpact entry in market.ActiveEvents)
        {
            data.market.activeEvents.Add(new TradingSaveData.MarketEffectData { impact = entry.impact, remainingTurns = entry.remainingTurns });
        }
        return data;
    }

    private static List<TradingSaveData.ScheduledEffectData> captureEffects(List<(Company company, int executionDay, float value)> effects)
    {
        List<TradingSaveData.ScheduledEffectData> data = new List<TradingSaveData.ScheduledEffectData>();
        foreach (var effect in effects)
        {
            data.Add(new TradingSaveData.ScheduledEffectData
            {
                companyId = effect.company.definition.companyID, executionDay = effect.executionDay, value = effect.value
            });
        }
        return data;
    }

    public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
    {
        prepared = null;
        resolveReferences();
        if (savedVersion != saveVersion)
        {
            throw new InvalidDataException("지원하지 않는 거래 저장 버전입니다.");
        }

        TradingSaveData data = JsonUtility.FromJson<TradingSaveData>(json);
        if (data == null)
        {
            throw new InvalidDataException("거래 저장 데이터의 필수 상태 또는 턴이 올바르지 않습니다.");
        }
        if (data.player == null || data.market == null || data.companies == null
            || data.greenmail == null || data.stockManipulation == null)
        {
            throw new InvalidDataException("거래 저장 데이터의 필수 상태 또는 턴이 올바르지 않습니다.");
        }
        if (data.companies.Count != companies.CompanyList.Count)
        {
            throw new InvalidDataException("거래 저장 데이터의 필수 상태 또는 턴이 올바르지 않습니다.");
        }
        if (data.market.lastUpdatedTurn < 0 || data.market.lastUpdatedTurn > dayStart.day)
        {
            throw new InvalidDataException("거래 저장 데이터의 필수 상태 또는 턴이 올바르지 않습니다.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int statCount = Enum.GetValues(typeof(CompanyStat)).Length;
        foreach (TradingSaveData.CompanyData entry in data.companies)
        {
            if (entry == null || ids.Contains(entry.companyId))
            {
                throw new InvalidDataException("회사 ID·원본 수치·시장 효과·이력을 확인하세요.");
            }
            ids.Add(entry.companyId);
            validateCompany(entry, dayStart.day, statCount);
        }
        if (!finite(data.player.money) || !finite(data.player.manPower) || !finite(data.player.information)
            || !finite(data.player.reputation) || !finite(data.market.globalMarketScore))
        {
            throw new InvalidDataException("플레이어 또는 시장 수치가 올바르지 않습니다.");
        }
        foreach (TradingSaveData.ScheduledEffectData effect in data.greenmail)
        {
            validateScheduledEffect(effect, dayStart.day);
        }
        foreach (TradingSaveData.ScheduledEffectData effect in data.stockManipulation)
        {
            validateScheduledEffect(effect, dayStart.day);
        }
        prepared = data;
    }

    private void validateCompany(TradingSaveData.CompanyData entry, int day, int statCount)
    {
        Company company;
        if (!companies.tryGetCompany(entry.companyId, out company))
        {
            throw new InvalidDataException("회사 ID·원본 수치·시장 효과·이력을 확인하세요.");
        }
        if (entry.state == null || entry.market == null || entry.history == null)
        {
            throw new InvalidDataException("회사 ID·원본 수치·시장 효과·이력을 확인하세요.");
        }
        if (entry.state.stats == null || entry.market.activeEvents == null)
        {
            throw new InvalidDataException("회사 ID·원본 수치·시장 효과·이력을 확인하세요.");
        }
        if (entry.state.totalShares < 1 || entry.state.stats.Count != statCount
            || entry.history.Count > company.history.getMaxHistory())
        {
            throw new InvalidDataException("회사 ID·원본 수치·시장 효과·이력을 확인하세요.");
        }

        HashSet<CompanyStat> statIds = new HashSet<CompanyStat>();
        foreach (TradingSaveData.StatData stat in entry.state.stats)
        {
            if (stat == null)
            {
                throw new InvalidDataException("회사 수치의 ID 또는 값이 올바르지 않습니다.");
            }
            CompanyStat statType;
            bool parsed = Enum.TryParse(stat.id, out statType);
            if (!parsed || !Enum.IsDefined(typeof(CompanyStat), statType))
            {
                throw new InvalidDataException("회사 수치의 ID 또는 값이 올바르지 않습니다.");
            }
            if (statIds.Contains(statType) || !finite(stat.value))
            {
                throw new InvalidDataException("회사 수치의 ID 또는 값이 올바르지 않습니다.");
            }
            statIds.Add(statType);
        }

        int previousTurn = 0;
        foreach (CompanyHistoryEntry history in entry.history)
        {
            if (history == null || history.turn <= previousTurn || history.turn >= day)
            {
                throw new InvalidDataException("회사 이력의 턴 순서가 올바르지 않습니다.");
            }
            previousTurn = history.turn;
        }
        foreach (TradingSaveData.MarketEffectData effect in entry.market.activeEvents)
        {
            if (effect == null || effect.impact.durationTurns < 1 || effect.remainingTurns < 1
                || effect.remainingTurns > effect.impact.durationTurns)
            {
                throw new InvalidDataException("지속 중인 시장 효과의 기간이 올바르지 않습니다.");
            }
        }
    }

    public void applyLoad()
    {
        if (prepared == null)
        {
            throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
        }
        TradingSaveData data = prepared;
        player.state.cash = data.player.money;
        player.state.manPower = data.player.manPower;
        player.state.information = data.player.information;
        player.state.reputation = data.player.reputation;
        marketData.globalMarketScore = data.market.globalMarketScore;
        marketData.LastUpdatedTurn = data.market.lastUpdatedTurn;
        foreach (TradingSaveData.CompanyData entry in data.companies)
        {
            applyCompany(entry);
        }
        applyEffects(data.greenmail, eventEffects.GreenmailAccept.ScheduledEffects);
        applyEffects(data.stockManipulation, eventEffects.StockManipulationAccept.ScheduledEffects);
        eventEffects.GreenmailAccept.RefreshScheduleSubscription();
        eventEffects.StockManipulationAccept.RefreshScheduleSubscription();
        prepared = null;
    }

    private void applyCompany(TradingSaveData.CompanyData entry)
    {
        Company company = companies.getCompany(entry.companyId);
        CompanyState state = company.state;
        CompanyMarketState market = company.marketState;
        foreach (TradingSaveData.StatData stat in entry.state.stats)
        {
            CompanyStat statType = (CompanyStat)Enum.Parse(typeof(CompanyStat), stat.id);
            state.setStat(statType, stat.value);
        }
        state.totalShares = entry.state.totalShares;
        state.greenmailBuyBlocked = entry.state.greenmailBuyBlocked;
        state.initialRevenue = entry.state.initialRevenue;
        state.initialCost = entry.state.initialCost;
        state.initialNetProfit = entry.state.initialNetProfit;
        state.initialNetMargin = entry.state.initialNetMargin;
        state.initialDebtRatio = entry.state.initialDebtRatio;
        state.initialLiquidityStrength = entry.state.initialLiquidityStrength;
        state.initialAssetValue = entry.state.initialAssetValue;
        state.tangibleCoefficient = entry.state.tangibleCoefficient;
        state.intangibleCoefficient = entry.state.intangibleCoefficient;
        market.npcSentiment = entry.market.npcSentiment;
        market.eventPriceMultiplier = entry.market.eventPriceMultiplier;
        market.marketNoise = entry.market.marketNoise;
        market.ActiveEvents.Clear();
        foreach (TradingSaveData.MarketEffectData effect in entry.market.activeEvents)
        {
            market.ActiveEvents.Add(new CompanyMarketState.ActiveEventImpact
            {
                impact = effect.impact, remainingTurns = effect.remainingTurns
            });
        }
        company.history.Entries.Clear();
        company.history.Entries.AddRange(entry.history);
    }

    private void applyEffects(List<TradingSaveData.ScheduledEffectData> data, List<(Company company, int executionDay, float value)> effects)
    {
        effects.Clear();
        foreach (TradingSaveData.ScheduledEffectData effect in data)
        {
            effects.Add((companies.getCompany(effect.companyId), effect.executionDay, effect.value));
        }
    }

    public void rebuildAfterLoad()
    {
        foreach (Company company in companies.CompanyList)
        {
            company.marketState.updateEventScores();
            company.calculator.recalculate(company, marketData);
        }
    }

    private void resolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
        if (companies == null)
        {
            companies = FindFirstObjectByType<Companies>();
        }
        if (marketData == null)
        {
            marketData = FindFirstObjectByType<Marketdata>();
        }
        if (eventEffects == null)
        {
            eventEffects = FindFirstObjectByType<TradingEventEffects>();
        }
        if (player == null || player.state == null || companies == null || marketData == null
            || eventEffects == null || eventEffects.GreenmailAccept == null || eventEffects.StockManipulationAccept == null)
        {
            throw new InvalidOperationException("거래 파트 초기화 후 저장 API를 호출하세요.");
        }
    }

    private void validateScheduledEffect(TradingSaveData.ScheduledEffectData effect, int day)
    {
        if (effect == null)
        {
            throw new InvalidDataException("예약 효과의 회사·실행 날짜·수치가 올바르지 않습니다.");
        }
        Company company;
        bool companyExists = companies.tryGetCompany(effect.companyId, out company);
        if (!companyExists || effect.executionDay < day || !finite(effect.value))
        {
            throw new InvalidDataException("예약 효과의 회사·실행 날짜·수치가 올바르지 않습니다.");
        }
    }

    private static bool finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
