using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class StartupFundingSource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly StartupSystem startupSystem;
        private readonly Player player;

        public StartupFundingSource(Companies companies, StartupSystem startupSystem, Player player)
        {
            this.companies = companies;
            this.startupSystem = startupSystem;
            this.player = player;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            StartupSettings settings = startupSystem.Settings;
            foreach (Company startup in companies.StartupList)
            {
                if (startup.state.getBaseStat(CompanyStat.CASH) > settings.bankruptcyCash) continue;
                if (!startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;
                if (!record.contracted) continue;
                if (record.fundingRejected) continue;

                float amount = Mathf.Round(Random.Range(settings.fundingAmount.x, settings.fundingAmount.y));
                inbox.Add(new StartupFundingDocument(startup, record, player, companies, amount,
                    turn + settings.fundingDeadlineTurns));
            }
        }
    }
}
