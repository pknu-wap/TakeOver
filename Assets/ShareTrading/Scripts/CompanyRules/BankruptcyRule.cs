namespace TakeOver.CompanyRules
{
    public sealed class BankruptcyRule : ICompanyRule
    {
        private readonly CompanyRuleSettings.PerformanceSettings performance;
        private readonly float cashThreshold;
        private readonly float netAssetsThreshold;

        public int PeriodTurns => performance.periodTurns;

        public BankruptcyRule(CompanyRuleSettings.PerformanceSettings performance,
            float cashThreshold, float netAssetsThreshold)
        {
            this.performance = performance;
            this.cashThreshold = cashThreshold;
            this.netAssetsThreshold = netAssetsThreshold;
        }

        public RuleResult Evaluate(Company company, RuleContext context)
        {
            RuleResult result = new RuleResult();
            CompanyState state = company.state;
            if (!isBankrupt(state)) return result;

            float playerShares = state.getBaseStat(CompanyStat.PLAYER_SHARES);
            if (playerShares != 0f) result.stats.Add(new StatChange(CompanyStat.PLAYER_SHARES, -playerShares));
            return result;
        }

        public bool isBankrupt(CompanyState state)
        {
            float cash = state.getStat(CompanyStat.CASH);
            float netAssets = cash + state.getStat(CompanyStat.TANGIBLE_ASSETS)
                + state.getStat(CompanyStat.INTANGIBLE_ASSTES) - state.getStat(CompanyStat.DEBT);
            return cash <= cashThreshold && netAssets <= netAssetsThreshold;
        }
    }
}
