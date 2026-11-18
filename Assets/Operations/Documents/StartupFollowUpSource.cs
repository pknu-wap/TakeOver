using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class StartupFollowUpSource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly StartupSystem startupSystem;

        public StartupFollowUpSource(Companies companies, StartupSystem startupSystem)
        {
            this.companies = companies;
            this.startupSystem = startupSystem;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            StartupSettings settings = startupSystem.Settings;
            foreach (Company startup in companies.StartupList)
            {
                if (!startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;

                if (record.nextFollowUpTurn <= 0)
                {
                    record.nextFollowUpTurn = turn + randomInterval(settings);
                    continue;
                }
                if (turn < record.nextFollowUpTurn) continue;
                if (startup.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE) < settings.followUpMinCompanyValue) continue;

                float amount = Mathf.Round(Random.Range(settings.followUpAmount.x, settings.followUpAmount.y));
                bool added = inbox.Add(new StartupFollowUpDocument(startup, record, companies, amount,
                    turn + settings.followUpDeadlineTurns));
                if (added) record.nextFollowUpTurn = turn + randomInterval(settings);
            }
        }

        private static int randomInterval(StartupSettings settings)
        {
            return Random.Range(settings.followUpIntervalTurns.x, settings.followUpIntervalTurns.y + 1);
        }
    }
}
