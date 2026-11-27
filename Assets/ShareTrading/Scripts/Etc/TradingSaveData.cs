using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;

/// <summary>지분매입팀의 저장 전용 데이터 형식</summary>
[Serializable]
public class TradingSaveData
{
    public PlayerData player;
    public MarketData market;
    public MarketEventManager.SaveData marketEvents;
    public List<CompanyData> companies;
    public List<ScheduledEffectData> greenmail;
    public List<ScheduledEffectData> stockManipulation;
    public bool hasStartupRoster;
    public int lastStartupId;

    [Serializable]
    public class PlayerData
    {
        public string name;
        public double money;
        public float manPower, information, reputation;
    }

    [Serializable]
    public class MarketData
    {
        public float globalMarketScore;
        public int lastUpdatedTurn;
    }

    [Serializable]
    public class CompanyData
    {
        public string companyId;
        public TakeOver.Startups.StartupRecord startup;
        public bool generated, listed;
        public CompanyStateData state;
        public CompanyMarketData market;
        public List<CompanyHistoryEntry> history;
        public List<float> priceHistory;
    }

    [Serializable]
    public class CompanyStateData
    {
        public CompanyStatus status;
        public List<StatData> stats;
        public int totalShares;
        public bool greenmailBuyBlocked;
        public float initialRevenue, initialCost, initialNetProfit, initialNetMargin;
        public float initialDebtRatio, initialLiquidityStrength, initialAssetValue;
        public float tangibleCoefficient, intangibleCoefficient;
    }

    [Serializable]
    public class StatData
    {
        public string id;
        public float value;
    }

    [Serializable]
    public class CompanyMarketData
    {
        public float npcSentiment, eventPriceMultiplier, marketNoise;
    }

    [Serializable]
    public class ScheduledEffectData
    {
        public string companyId;
        public int executionDay;
        public float value;
    }
}
