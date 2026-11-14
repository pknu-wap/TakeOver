using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public sealed class DividendPolicySource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly DocumentSettings settings;

        public DividendPolicySource(Companies companies, DocumentSettings settings)
        {
            this.companies = companies;
            this.settings = settings;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            if (turn % settings.quarterTurns != 0) return;
            foreach (Company company in companies.CompanyList)
            {
                float stake = company.state.getBaseStat(CompanyStat.PLAYER_SHARES) / company.state.totalShares;
                if (stake >= settings.takeoverRatio && company.state.status == CompanyStatus.Normal)
                    inbox.Add(new DividendPolicyDocument(company, turn + settings.dividendDeadlineTurns, settings));
            }
        }
    }
}
