using System.Collections.Generic;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Employees
{
    public sealed class MarketAppraisalService : MonoBehaviour
    {
        private readonly System.Random random = new System.Random();
        private readonly List<JobCompletion> completed = new List<JobCompletion>();
        private JobService jobs;
        private DocumentSystem documents;
        private GameTurnClock clock;

        private void Start()
        {
            jobs = FindFirstObjectByType<JobService>();
            documents = FindFirstObjectByType<DocumentSystem>();
            clock = FindFirstObjectByType<GameTurnClock>();
            jobs.OnJobCompleted += handleCompleted;
            clock.DayStarted += publishReports;
        }

        private void OnDestroy()
        {
            if (jobs != null) jobs.OnJobCompleted -= handleCompleted;
            if (clock != null) clock.DayStarted -= publishReports;
        }

        private void handleCompleted(JobCompletion completion)
        {
            if (completion.Job == JobType.Appraisal) completed.Add(completion);
        }

        // 회사의 날짜 계산이 끝난 뒤 보고서 작성
        private void publishReports(int turn)
        {
            foreach (JobCompletion completion in completed)
            {
                var rows = new List<DocumentInfo>();
                foreach (Company company in completion.Targets)
                {
                    MarketAppraisal.Result result = MarketAppraisal.evaluate(company, completion.Employee.Analysis.Current, random);
                    rows.Add(new DocumentInfo("대상 회사", company.definition.companyName));
                    rows.Add(new DocumentInfo("실적 추세", MarketAppraisal.describe(result.performance)));
                    rows.Add(new DocumentInfo("시장 평가", MarketAppraisal.describe(result.market)));
                    rows.Add(new DocumentInfo("현재 주가", $"{company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE):N0}원"));
                    rows.Add(new DocumentInfo("내 지분", $"{company.state.getDerivedStat(CompanyDerivedStat.STAKE):0.##}%"));
                }
                documents.Inbox.Add(new SnapshotDocument("market-appraisal", "시세 예측 보고", completion.Employee.Name, rows, rowsPerPage: 5));
            }
            completed.Clear();
        }
    }
}
