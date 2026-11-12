using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class CompanyRuleRunner : MonoBehaviour
    {
        [SerializeField] private Companies companies;
        [SerializeField] private Player player;
        [SerializeField] private Marketdata marketData;
        [SerializeField] private CompanyRuleSettings settings;
        private List<ICompanyRule> rules;
        private ChangeApplier applier;

        private void Awake()
        {
            if (companies == null) companies = FindFirstObjectByType<Companies>();
            if (player == null) player = FindFirstObjectByType<Player>();
            if (marketData == null) marketData = FindFirstObjectByType<Marketdata>();
        }

        public void RunTurn(int turn)
        {
            if (rules == null)
            {
                rules = new List<ICompanyRule>
                {
                    new PerformanceRule(settings.performance),
                    new EmployeeRule(settings.employees),
                    new DividendRule(settings.dividend),
                    new DelistingRule(settings.delisting)
                };
                applier = new ChangeApplier(player);
            }
            RuleContext context = new RuleContext(turn, marketData.globalMarketScore);
            foreach (Company company in companies.CompanyList)
            {
                foreach (ICompanyRule rule in rules)
                {
                    if (turn % rule.PeriodTurns != 0) continue;
                    applier.Apply(rule.Evaluate(company, context), company);
                }
            }
        }
    }
}
