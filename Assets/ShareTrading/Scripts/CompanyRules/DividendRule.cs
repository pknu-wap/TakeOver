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

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            RuleResult result = new RuleResult();
            CompanyState state = company.state;
            float profit = state.getStat(CompanyStat.REVENUE) - state.getStat(CompanyStat.COST);
            if (profit <= 0f) return result;
            float total = Mathf.Min(Mathf.Max(0f, state.getStat(CompanyStat.CASH)),
                profit * Mathf.Clamp01(company.definition.dividendRatio));
            result.stats.Add(new StatChange(CompanyStat.CASH, -total));
            result.playerCash = total * state.getStat(CompanyStat.PLAYER_SHARES) / state.totalShares;
            return result;
        }
    }
}
