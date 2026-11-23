using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class DividendRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.DividendSettings settings;
        public int PeriodTurns => settings.periodTurns;

        public DividendRule(CompanyRuleSettings.DividendSettings settings)
        {
            this.settings = settings;
        }

        public static float TotalDividend(CompanyState state, float ratio)
        {
            float profit = state.getBaseStat(CompanyStat.REVENUE) - state.getBaseStat(CompanyStat.COST);
            return profit > 0f ? Mathf.Min(Mathf.Max(0f, state.getBaseStat(CompanyStat.CASH)),
                profit * Mathf.Clamp01(ratio)) : 0f;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            RuleResult result = new RuleResult();
            CompanyState state = company.state;
            if (state.getBaseStat(CompanyStat.REVENUE) - state.getBaseStat(CompanyStat.COST) <= 0f) return result;
            float total = TotalDividend(state, state.getBaseStat(CompanyStat.DIVIDEND_RATIO));
            result.stats.Add(new StatChange(CompanyStat.CASH, -total));
            result.playerCash = total * state.getBaseStat(CompanyStat.PLAYER_SHARES) / state.totalShares;
            return result;
        }
    }
}
