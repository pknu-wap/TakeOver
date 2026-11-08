using System.Collections.Generic;
using UnityEngine;

public class MarketEventEffectControl : MonoBehaviour
{
    [SerializeField] private Companies companies;
    // 선정 순서 유지, 기간이 끝난 영구 효과도 보관
    private readonly List<MarketEventManager.ActiveMarketEvent> inApplyList
        = new List<MarketEventManager.ActiveMarketEvent>();
    private readonly Dictionary<(string companyID, MarketEventTargetType type, int stat),
        (float multiplier, float normalValue)> appliedEffects
        = new Dictionary<(string, MarketEventTargetType, int), (float, float)>();

    public void addEffects(MarketEventManager.ActiveMarketEvent entry)
    {
        inApplyList.Add(entry);
    }

    public void delEffects(int eventOrderID)
    {
        inApplyList.RemoveAll(entry => entry.orderID == eventOrderID);
    }

    public void applyMarketEventEffects(int currentTurn)
    {
        appliedEffects.Clear();
        foreach (MarketEventManager.ActiveMarketEvent entry in inApplyList)
        {
            if (currentTurn < entry.startTurn) continue;

            float progress = entry.definition.isLinear
                ? Mathf.Min((float)(currentTurn - entry.startTurn + 1) / entry.definition.duration, 1f)
                : 1f;
            applyEffects(entry.definition, progress);
        }

        // 회사에 appliedEffects 전달은 미구현
    }

    private void applyEffects(MarketEventDefinition definition, float progress)
    {
        foreach (MarketEventDefinition.EventEffect effect in definition.effects)
        {
            int stat;
            if (effect.targetType == MarketEventTargetType.Stat)
            {
                stat = (int)effect.stat;
            }
            else if (effect.targetType == MarketEventTargetType.DerivedStat)
            {
                stat = (int)effect.derivedStat;
            }
            else
            {
                stat = 0;
            }
            
            var key = (effect.companyID, effect.targetType, stat);
            float multiplier = 1f + effect.changePercentValue * 0.01f * progress;
            float normalValue = effect.changeNormalValue * progress;

            // 기본값 * multiplier + normalValue 형태로 적용 순서 보존
            if (appliedEffects.TryGetValue(key, out var previous))
                appliedEffects[key] = (previous.multiplier * multiplier,
                    previous.normalValue * multiplier + normalValue);
            else
                appliedEffects.Add(key, (multiplier, normalValue));
        }
    }
}
