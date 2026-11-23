using System.Collections.Generic;
using System;
using TakeOver.Startups;
using TakeOver.Documents;
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
        private List<ICompanyRule> startupRules;
        private BankruptcyRule bankruptcyRule;
        private ChangeApplier applier;
        private AcquisitionOperationsService acquisitionOperations;
        public event Action<int> RulesCompleted;
        public int performancePeriodTurns => settings != null ? settings.performance.periodTurns : 7;
        public float costPerEmployee => settings.performance.costPerEmployee;

        private void Awake()
        {
            if (companies == null) companies = FindFirstObjectByType<Companies>();
            if (player == null) player = FindFirstObjectByType<Player>();
            if (marketData == null) marketData = FindFirstObjectByType<Marketdata>();
            acquisitionOperations = FindFirstObjectByType<AcquisitionOperationsService>();
        }

        public void RunTurn(int turn)
        {
            if (rules == null)
            {
                rules = new List<ICompanyRule>
                {
                    new PerformanceRule(settings.performance,
                        acquisitionOperations != null ? new Func<Company, int, float>(acquisitionOperations.GrowthBonus) : null),
                    new EmployeeRule(settings.employees),
                    new DividendRule(settings.dividend),
                    new DelistingRule(settings.delisting)
                };
                StartupSystem startupSystem = FindFirstObjectByType<StartupSystem>();
                StartupSettings startupSettings = startupSystem != null ? startupSystem.Settings : null;
                bankruptcyRule = new BankruptcyRule(settings.performance,
                    startupSettings != null ? startupSettings.bankruptcyCash : 0f,
                    startupSettings != null ? startupSettings.bankruptcyNetAssets : 0f);
                startupRules = new List<ICompanyRule>
                {
                    new PerformanceRule(settings.performance,
                        startupSystem != null ? new Func<Company, int, float>(startupSystem.extraGrowthBonus) : null,
                        startupSystem != null ? new Func<Company, float>(startupSystem.supportCostReduction) : null),
                    new EmployeeRule(settings.employees),
                    bankruptcyRule
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
                    if (rule is PerformanceRule && acquisitionOperations != null)
                    {
                        bool acquired = company.state.totalShares > 0 && company.state.getBaseStat(CompanyStat.PLAYER_SHARES) /
                            company.state.totalShares >= acquisitionOperations.Settings.takeoverRatio;
                        bool loss = company.state.getBaseStat(CompanyStat.REVENUE) - company.state.getBaseStat(CompanyStat.COST) < 0f;
                        acquisitionOperations.SetLossWeeks(company, acquired && loss ? acquisitionOperations.LossWeeks(company) + 1 : 0);
                    }
                }
            }
            runStartupRules(turn, context);
            acquisitionOperations?.PruneRecords(turn);
            RulesCompleted?.Invoke(turn);
        }

        private void runStartupRules(int turn, RuleContext context)
        {
            bool bankruptcyTurn = turn % bankruptcyRule.PeriodTurns == 0;
            IReadOnlyList<Company> startups = companies.StartupList;
            for (int i = startups.Count - 1; i >= 0; i--)
            {
                Company startup = startups[i];
                foreach (ICompanyRule rule in startupRules)
                {
                    if (turn % rule.PeriodTurns != 0) continue;
                    applier.Apply(rule.Evaluate(startup, context), startup);
                }
                if (bankruptcyTurn && bankruptcyRule.isBankrupt(startup.state))
                {
                    Debug.Log($"[CompanyRules] {startup.definition.companyName} 파산으로 폐업");
                    companies.CloseStartup(startup);
                }
            }
        }
    }
}
