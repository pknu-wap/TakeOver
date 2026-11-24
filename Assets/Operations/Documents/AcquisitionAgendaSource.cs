using TakeOver.NPC;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public sealed class AcquisitionAgendaSource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly AcquisitionOperationsService operations;
        private readonly GameTurnClock clock;
        private readonly int performancePeriod;

        public AcquisitionAgendaSource(Companies companies, AcquisitionOperationsService operations, GameTurnClock clock, int performancePeriod)
        {
            this.companies = companies;
            this.operations = operations;
            this.clock = clock;
            this.performancePeriod = performancePeriod;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            DocumentSettings settings = operations.Settings;
            foreach (Company company in companies.CompanyList)
            {
                if (company.state.status != CompanyStatus.Normal || company.state.totalShares <= 0 ||
                    company.state.getBaseStat(CompanyStat.PLAYER_SHARES) / company.state.totalShares < settings.takeoverRatio) continue;
                int deadline = turn + settings.agendaDeadlineTurns;
                if (turn % settings.quarterTurns == 0 && FacilityInvestmentDocument.Liquidity(company) >= settings.facilityLiquidityThreshold)
                    inbox.Add(new FacilityInvestmentDocument(company, companies, operations, clock, deadline));
                if (turn % performancePeriod == 0 && operations.LossWeeks(company) >= settings.restructuringLossWeeks)
                    inbox.Add(new RestructuringDocument(company, companies, operations, clock, deadline));
            }
        }
    }
}
