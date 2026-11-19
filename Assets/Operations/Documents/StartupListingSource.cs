using System.Collections.Generic;
using TakeOver.NPC;
using TakeOver.Startups;

namespace TakeOver.Documents
{
    public sealed class StartupListingSource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly StartupSystem startupSystem;
        private readonly GameTurnClock clock;

        public StartupListingSource(Companies companies, StartupSystem startupSystem, GameTurnClock clock)
        {
            this.companies = companies;
            this.startupSystem = startupSystem;
            this.clock = clock;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            StartupSettings settings = startupSystem.Settings;
            IReadOnlyList<Company> startups = companies.StartupList;
            for (int i = startups.Count - 1; i >= 0; i--)
            {
                Company startup = startups[i];
                if (!meetsListing(startup, settings)) continue;
                if (!startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;
                if (!record.contracted) continue;
                if (turn < record.listingRetryTurn) continue;

                bool noReject = turn >= record.contractTurn + settings.listingNoRejectTurns;
                if (!noReject)
                {
                    inbox.Add(new StartupListingDocument(startup, record, companies, settings, clock,
                        turn + settings.listingDeadlineTurns));
                    continue;
                }

                if (hasOpenListing(inbox, startup)) continue;
                if (!StartupListingDocument.listNow(companies, startup, settings)) continue;
                inbox.Add(new SnapshotDocument(StartupListingDocument.reportTypeId, "상장 완료", "", new[]
                {
                    new DocumentInfo("대상 회사", startup.definition.companyName),
                    new DocumentInfo("내용", "상장 준비를 마쳐 바로 상장했습니다")
                }, startup.definition.companyID));
            }
        }

        private static bool meetsListing(Company startup, StartupSettings settings)
        {
            return startup.state.getBaseStat(CompanyStat.REVENUE) >= settings.listingRevenue
                || startup.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE) >= settings.listingCompanyValue;
        }

        private static bool hasOpenListing(DocumentInbox inbox, Company startup)
        {
            foreach (IDocument document in inbox.Open)
                if (document.TypeId == StartupListingDocument.typeId
                    && document.DedupKey == startup.definition.companyID) return true;
            return false;
        }
    }
}
