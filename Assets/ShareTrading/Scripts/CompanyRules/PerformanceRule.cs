using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class PerformanceRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.PerformanceSettings settings;
        public int PeriodTurns => settings.periodTurns;

        public PerformanceRule(CompanyRuleSettings.PerformanceSettings settings)
        {
            this.settings = settings;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            CompanyState state = company.state;
            CompanyDefinition definition = company.definition;
            float employees = state.getStat(CompanyStat.EMPLOYEE_COUNT);
            float reputation = (state.getStat(CompanyStat.REPUTATION_B2C)
                + state.getStat(CompanyStat.REPUTATION_B2B)) / 2f;
            float employeeGrowth = CompanyCalculationMath.safeDivide(
                employees - definition.initialEmployeeCount,
                CompanyCalculationMath.positiveDenominator(definition.initialEmployeeCount));
            // 시장 이벤트의 실적 영향은 후속 작업에서 연결한다(현재 0).
            float growth = CompanyCalculationMath.clamp(definition.growthRate
                + context.marketScore * settings.marketWeight
                + CompanyCalculationMath.normalizeScore(state.getStat(CompanyStat.MARKET_POSITION)) * settings.positionWeight
                + CompanyCalculationMath.normalizeScore(reputation) * settings.reputationWeight
                + employeeGrowth * settings.employeeWeight, settings.minGrowth, settings.maxGrowth);
            float revenue = Mathf.Max(0f, state.getStat(CompanyStat.REVENUE) * (1f + growth));
            float cost = Mathf.Max(0f, settings.fixedCost + employees * settings.costPerEmployee
                + revenue * settings.variableCostRatio);
            float cash = state.getStat(CompanyStat.CASH) + revenue - cost;
            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.REVENUE, revenue - state.getStat(CompanyStat.REVENUE)));
            result.stats.Add(new StatChange(CompanyStat.COST, cost - state.getStat(CompanyStat.COST)));
            result.stats.Add(new StatChange(CompanyStat.CASH, Mathf.Max(0f, cash) - state.getStat(CompanyStat.CASH)));
            if (cash < 0f) result.stats.Add(new StatChange(CompanyStat.DEBT, -cash));
            return result;
        }
    }
}
