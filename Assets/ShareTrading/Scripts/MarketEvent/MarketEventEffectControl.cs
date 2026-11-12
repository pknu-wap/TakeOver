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

    // 보정값 전달 뒤 호출측에서 회사 수치 재계산
    public void applyMarketEventEffects(int currentTurn)
    {
        appliedEffects.Clear();
        foreach (MarketEventManager.ActiveMarketEvent entry in inApplyList)
        {
            if (currentTurn < entry.startTurn) continue;

            float progress = entry.definition.isLinear
                ? Mathf.Min((float)(currentTurn - entry.startTurn + 1) / entry.definition.duration, 1f)
                : 1f;
            applyEffects(entry, progress);
        }

        foreach (Company company in companies.CompanyList)
        {
            company.state.resetMarketEventEffects();
            company.marketState.resetMarketEventEffects();
        }

        foreach (var entry in appliedEffects)
        {
            Company company = companies.getCompany(entry.Key.companyID);
            if (entry.Key.type == MarketEventTargetType.Stat)
            {
                company.state.setMarketEventEffect((CompanyStat)entry.Key.stat,
                    entry.Value.multiplier, entry.Value.normalValue);
            }
            else if (entry.Key.type == MarketEventTargetType.DerivedStat)
            {
                company.state.setMarketEventEffect((CompanyDerivedStat)entry.Key.stat,
                    entry.Value.multiplier, entry.Value.normalValue);
            }
            else
            {
                company.marketState.setMarketLiquidityEffect(entry.Value.multiplier, entry.Value.normalValue);
            }
        }
    }

    private void applyEffects(MarketEventManager.ActiveMarketEvent entry, float progress)
    {
        foreach (MarketEventDefinition.EventEffect effect in entry.definition.effects)
        {
            if (effect.companyTargetType == MarketEventCompanyTargetType.AllCompanies)
            {
                foreach (Company company in companies.CompanyList)
                    applyEffect(company.definition.companyID, effect, progress);
            }
            else if (effect.companyTargetType == MarketEventCompanyTargetType.RandomCompany)
            {
                applyEffect(entry.selectedCompanyID, effect, progress);
            }
            else
            {
                applyEffect(effect.companyID, effect, progress);
            }
        }
    }

    private void applyEffect(string companyID, MarketEventDefinition.EventEffect effect, float progress)
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

        var key = (companyID, effect.targetType, stat);
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
