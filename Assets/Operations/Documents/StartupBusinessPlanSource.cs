using TakeOver.NPC;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    // 스타트업마다 1년(84턴)에 한 번 연간 사업 계획 문서를 올린다.
    public sealed class StartupBusinessPlanSource : IDocumentSource
    {
        private readonly Companies companies;
        private readonly StartupSystem startupSystem;
        private readonly GameTurnClock clock;

        public StartupBusinessPlanSource(Companies companies, StartupSystem startupSystem, GameTurnClock clock)
        {
            this.companies = companies;
            this.startupSystem = startupSystem;
            this.clock = clock;
        }

        public void Generate(int turn, DocumentInbox inbox)
        {
            StartupSettings settings = startupSystem.Settings;
            foreach (Company startup in companies.StartupList)
            {
                if (!startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;

                // 처음 보는 스타트업은 첫 날짜를 1~21턴(설정값) 안에서 무작위로 정해 회사마다 엇갈리게 한다
                if (record.nextBusinessPlanTurn <= 0)
                {
                    record.nextBusinessPlanTurn = turn + Random.Range(1, settings.businessPlanFirstMaxTurns + 1);
                    continue;
                }
                if (turn < record.nextBusinessPlanTurn) continue;

                bool added = inbox.Add(new StartupBusinessPlanDocument(startup, record, companies, settings, clock,
                    turn + settings.businessPlanDeadlineTurns));
                // 문서가 실제로 올라갔을 때만 다음 해 날짜를 정한다
                if (added) record.nextBusinessPlanTurn = turn + settings.businessPlanPeriodTurns;
            }
        }
    }
}
