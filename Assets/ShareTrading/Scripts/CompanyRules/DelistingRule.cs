using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class DelistingRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.DelistingSettings settings;
        public int PeriodTurns => settings.periodTurns;

        public DelistingRule(CompanyRuleSettings.DelistingSettings settings)
        {
            this.settings = settings;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            RuleResult result = new RuleResult();
            CompanyState state = company.state;
            if (state.status == CompanyStatus.Liquidation)
            {
                float remaining = state.getStat(CompanyStat.LIQUIDATION_TURNS_REMAINING);
                if (remaining > 0f) result.stats.Add(new StatChange(CompanyStat.LIQUIDATION_TURNS_REMAINING,
                    -Mathf.Min(remaining, settings.periodTurns)));
                return result;
            }
            if (state.status == CompanyStatus.Delisted)
            {
                startLiquidation(result, state);
                return result;
            }
            // 스토리 기업의 인수 전 보호 조건은 기업 구분 필드가 생긴 뒤 추가한다.
            float netAssets = state.getStat(CompanyStat.CASH) + state.getStat(CompanyStat.TANGIBLE_ASSETS)
                + state.getStat(CompanyStat.INTANGIBLE_ASSTES) - state.getStat(CompanyStat.DEBT);
            CompanyDefinition definition = company.definition;
            float initial = definition.initialCash + definition.initialTangibleAssets
                + definition.initialIntangibleAssets - definition.initialDebt;
            if (netAssets <= 0f)
            {
                result.states.Add(new StateChange(CompanyStatus.Delisted));
                startLiquidation(result, state);
            }
            else if (netAssets >= initial * settings.watchlistRatio)
            {
                if (state.status == CompanyStatus.Watchlisted)
                {
                    result.states.Add(new StateChange(CompanyStatus.Normal));
                    result.stats.Add(new StatChange(CompanyStat.WATCHLIST_REVIEWS_REMAINING,
                        -state.getStat(CompanyStat.WATCHLIST_REVIEWS_REMAINING)));
                }
            }
            else if (state.status == CompanyStatus.Normal)
            {
                result.states.Add(new StateChange(CompanyStatus.Watchlisted));
                result.stats.Add(new StatChange(CompanyStat.WATCHLIST_REVIEWS_REMAINING,
                    settings.graceReviews - state.getStat(CompanyStat.WATCHLIST_REVIEWS_REMAINING)));
            }
            else
            {
                float remaining = state.getStat(CompanyStat.WATCHLIST_REVIEWS_REMAINING);
                result.stats.Add(new StatChange(CompanyStat.WATCHLIST_REVIEWS_REMAINING, -Mathf.Min(remaining, 1f)));
                if (remaining <= 1f)
                {
                    result.states.Add(new StateChange(CompanyStatus.Delisted));
                    startLiquidation(result, state);
                }
            }
            return result;
        }

        private void startLiquidation(RuleResult result, CompanyState state)
        {
            result.states.Add(new StateChange(CompanyStatus.Liquidation));
            result.stats.Add(new StatChange(CompanyStat.LIQUIDATION_TURNS_REMAINING,
                settings.liquidationTurns - state.getStat(CompanyStat.LIQUIDATION_TURNS_REMAINING)));
        }
    }
}
