using System;
using System.Collections.Generic;
using UnityEngine;

public enum MarketEventApplyType
{
    Permanent,
    PermanentLinear,
    Temporary,
    TemporaryLinear
}

public enum MarketEventTargetType
{
    Stat,
    DerivedStat,
    MarketLiquidity
}

[CreateAssetMenu(fileName = "MarketEventDefinition", menuName = "ShareTrading/MarketEvent Definition")]
public class MarketEventDefinition : ScriptableObject
{
    [Serializable]
    public struct EventEffect
    {
        public string companyID;
        public MarketEventTargetType targetType;
        public CompanyStat stat;
        public CompanyDerivedStat derivedStat;
        public float changeNormalValue;
        public float changePercentValue;
        
    }

    public string MarketEventName;
    public int MarketEventID;
    public MarketEventApplyType applyType;
    [Min(1)] public int duration = 1;
    [Min(0)] public int delayTurns;
    public List<EventEffect> effects = new List<EventEffect>();

    public bool isPermanent => applyType == MarketEventApplyType.Permanent
        || applyType == MarketEventApplyType.PermanentLinear;
    public bool isLinear => applyType == MarketEventApplyType.PermanentLinear
        || applyType == MarketEventApplyType.TemporaryLinear;
}
