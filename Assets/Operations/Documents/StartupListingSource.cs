using System.Collections.Generic;
using TakeOver.Startups;

namespace TakeOver.Documents
{
    // 계약한 스타트업이 상장 요건(매출 또는 기업가치)을 채우면 읽기 전용 상장 추진 보고서를 보내고, 그다음 날 상장한다.
    // 승인·거절이 없는 보고서라 플레이어가 막거나 미룰 수 없다. 요건 자체는 보고서에도 보여 주지 않는다.
    public sealed class StartupListingSource : IDocumentSource
    {
        public const string reportTypeId = "startup-listing-report";

        private readonly Companies companies;
        private readonly StartupSystem startupSystem;

        public StartupListingSource(Companies companies, StartupSystem startupSystem)
        {
            this.companies = companies;
            this.startupSystem = startupSystem;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            StartupSettings settings = startupSystem.Settings;
            IReadOnlyList<Company> startups = companies.StartupList;
            // 상장하면 목록에서 빠지므로 뒤에서부터 돈다
            for (int i = startups.Count - 1; i >= 0; i--)
            {
                Company startup = startups[i];
                if (!startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;
                if (!record.contracted) continue;

                // 보고서를 보낸 다음 날: 조건이 다시 떨어졌어도 보고서대로 상장한다
                if (record.listingTurn > 0)
                {
                    if (turn >= record.listingTurn) listNow(companies, startup, settings);
                    continue;
                }

                if (!meetsListing(startup, settings)) continue;
                record.listingTurn = turn + 1;
                inbox.Add(new SnapshotDocument(reportTypeId, "상장 확정 보고",
                    $"{startup.definition.companyName} 대표", new[]
                    {
                        new DocumentInfo("대상 회사", startup.definition.companyName),
                        new DocumentInfo("업종", startup.definition.industry),
                        new DocumentInfo("내용", "다음 날 상장"),
                        new DocumentInfo("현재 기업가치", MoneyText.Format(startup.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE))),
                        new DocumentInfo("내 지분", StakeText.FormatShares(startup.state.getBaseStat(CompanyStat.PLAYER_SHARES), startup.state.totalShares))
                    }, startup.definition.companyID));
            }
        }

        // 상장 처리. 배당 비율은 아직 정해지지 않아 임시 값을 넣는다.
        public static bool listNow(Companies companies, Company startup, StartupSettings settings)
        {
            if (!companies.PromoteStartup(startup)) return false;
            startup.state.addStat(CompanyStat.DIVIDEND_RATIO,
                settings.listedDividendRatio - startup.state.getBaseStat(CompanyStat.DIVIDEND_RATIO));
            return true;
        }

        // 아직 스타트업 목록에 있는지 (폐업하거나 상장하면 false). 다른 스타트업 문서들이 승인 전에 확인한다.
        public static bool isStartup(Companies companies, Company company)
        {
            foreach (Company startup in companies.StartupList)
                if (startup == company) return true;
            return false;
        }

        private static bool meetsListing(Company startup, StartupSettings settings)
        {
            return startup.state.getBaseStat(CompanyStat.REVENUE) >= settings.listingRevenue
                || startup.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE) >= settings.listingCompanyValue;
        }

    }
}
