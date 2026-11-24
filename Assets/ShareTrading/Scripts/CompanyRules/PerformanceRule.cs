using System;
using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class PerformanceRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.PerformanceSettings settings;
        // 회사별 추가 성장률. 시장 기업과 스타트업의 투자 기록은 각각의 서비스에서 읽는다.
        private readonly Func<Company, int, float> extraGrowth;
        private readonly Func<Company, float> costReduction;
        public int PeriodTurns => settings.periodTurns;

        public PerformanceRule(CompanyRuleSettings.PerformanceSettings settings,
            Func<Company, int, float> extraGrowth = null, Func<Company, float> costReduction = null)
        {
            this.settings = settings;
            this.extraGrowth = extraGrowth;
            this.costReduction = costReduction;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            CompanyState state = company.state;
            CompanyDefinition definition = company.definition;
            float employees = state.getBaseStat(CompanyStat.EMPLOYEE_COUNT);
            float reputation = (state.getBaseStat(CompanyStat.REPUTATION_B2C)
                + state.getBaseStat(CompanyStat.REPUTATION_B2B)) / 2f;
            float employeeGrowth = CompanyCalculationMath.safeDivide(
                employees - definition.initialEmployeeCount,
                CompanyCalculationMath.positiveDenominator(definition.initialEmployeeCount));
            // 시장 이벤트의 실적 영향은 후속 작업에서 연결한다(현재 0).
            float growth = CompanyCalculationMath.clamp(definition.growthRate
                + context.marketScore * settings.marketWeight
                + CompanyCalculationMath.normalizeScore(state.getBaseStat(CompanyStat.MARKET_POSITION)) * settings.positionWeight
                + CompanyCalculationMath.normalizeScore(reputation) * settings.reputationWeight
                + employeeGrowth * settings.employeeWeight
                + (extraGrowth != null ? extraGrowth(company, context.currentTurn) : 0f),
                settings.minGrowth, settings.maxGrowth);
            float revenue = Mathf.Max(0f, state.getBaseStat(CompanyStat.REVENUE) * (1f + growth));
            float reduction = costReduction != null ? Mathf.Clamp01(costReduction(company)) : 0f;
            float cost = Mathf.Max(0f, (settings.fixedCost + employees * settings.costPerEmployee
                + revenue * settings.variableCostRatio) * (1f - reduction));
            float cash = state.getBaseStat(CompanyStat.CASH) + revenue - cost;
            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.REVENUE, revenue - state.getBaseStat(CompanyStat.REVENUE)));
            result.stats.Add(new StatChange(CompanyStat.COST, cost - state.getBaseStat(CompanyStat.COST)));
            result.stats.Add(new StatChange(CompanyStat.CASH, Mathf.Max(0f, cash) - state.getBaseStat(CompanyStat.CASH)));
            if (cash < 0f) result.stats.Add(new StatChange(CompanyStat.DEBT, -cash));
            return result;
        }
    }
}
