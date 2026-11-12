using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class EmployeeRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.EmployeeSettings settings;
        public int PeriodTurns => settings.periodTurns;

        public EmployeeRule(CompanyRuleSettings.EmployeeSettings settings)
        {
            this.settings = settings;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            CompanyState state = company.state;
            int count = company.history.History.Count;
            float previousRevenue = count == 0 ? state.initialRevenue : company.history.History[count - 1].revenue;
            float revenue = state.getStat(CompanyStat.REVENUE);
            float profit = revenue - state.getStat(CompanyStat.COST);
            float change = profit < 0f ? profit * settings.lossWeight
                : (revenue - previousRevenue) * settings.revenueChangeWeight;
            float employees = state.getStat(CompanyStat.EMPLOYEE_COUNT);
            float next = Mathf.Max(0f, employees + Mathf.RoundToInt(change));
            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.EMPLOYEE_COUNT, next - employees));
            return result;
        }
    }
}
