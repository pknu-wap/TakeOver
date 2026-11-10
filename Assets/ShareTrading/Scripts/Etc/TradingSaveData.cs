using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;

/// <summary>지분매입팀의 저장 전용 데이터 형식</summary>
[Serializable]
public sealed class TradingSaveData
{
    public PlayerData player;
    public MarketData market;
    public List<CompanyData> companies;
    public List<ScheduledEffectData> greenmail;
    public List<ScheduledEffectData> stockManipulation;

    [Serializable]
    public sealed class PlayerData
    {
        public float money, manPower, information, reputation;
    }

    [Serializable]
    public sealed class MarketData
    {
        public float globalMarketScore;
        public int lastUpdatedTurn;
    }

    [Serializable]
    public sealed class CompanyData
    {
        public string companyId;
        public CompanyStateData state;
        public CompanyMarketData market;
        public List<CompanyHistoryEntry> history;
    }

    [Serializable]
    public sealed class CompanyStateData
    {
        public CompanyStatus status;
        public List<StatData> stats;
        public int totalShares;
        public float initialRevenue, initialCost, initialNetProfit, initialNetMargin;
        public float initialDebtRatio, initialLiquidityStrength, initialAssetValue;
        public float tangibleCoefficient, intangibleCoefficient;
    }

    [Serializable]
    public sealed class StatData
    {
        public string id;
        public float value;
    }

    [Serializable]
    public sealed class CompanyMarketData
    {
        public float npcSentiment, eventPriceMultiplier, marketNoise;
        public List<MarketEffectData> activeEvents;
    }

    [Serializable]
    public sealed class MarketEffectData
    {
        public MarketEventImpact impact;
        public int remainingTurns;
    }

    [Serializable]
    public sealed class ScheduledEffectData
    {
        public string companyId;
        public int executionDay;
        public float value;
    }
}
