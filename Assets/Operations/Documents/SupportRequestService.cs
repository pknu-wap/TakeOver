using System.Collections.Generic;
using TakeOver.Employees;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class SupportRequestService : MonoBehaviour
    {
        [SerializeField] private DocumentSettings settings;
        private readonly Dictionary<string, int> nextRequestTurns = new Dictionary<string, int>();
        private readonly List<SupportRequestDocument> unavailableRequests = new List<SupportRequestDocument>();
        private Companies companies;
        private EmployeeService employees;
        private JobService jobs;
        private DocumentSystem documents;
        private GameTurnClock clock;

        private void Start()
        {
            companies = FindFirstObjectByType<Companies>();
            employees = FindFirstObjectByType<EmployeeService>();
            jobs = FindFirstObjectByType<JobService>();
            documents = FindFirstObjectByType<DocumentSystem>();
            clock = FindFirstObjectByType<GameTurnClock>();
            documents.Inbox.DocumentResolved += handleResolved;
            clock.DayStarted += generateRequests;
            clock.TurnRestored += restoreRetryTurns;
            restoreRetryTurns(clock.CurrentTurn);
        }

        private void OnDestroy()
        {
            if (documents != null) documents.Inbox.DocumentResolved -= handleResolved;
            if (clock != null)
            {
                clock.DayStarted -= generateRequests;
                clock.TurnRestored -= restoreRetryTurns;
            }
        }

        // 회사의 경제 계산이 끝난 뒤 판정
        private void generateRequests(int turn)
        {
            cancelUnavailableRequests();
            foreach (Company company in companies.StartupList)
            {
                if (!needsSupport(company)) continue;
                if (nextRequestTurns.TryGetValue(company.definition.companyID, out int nextTurn) && turn < nextTurn) continue;
                issueRequest(company);
            }
        }

        private void Update() => cancelUnavailableRequests();

        private void cancelUnavailableRequests()
        {
            unavailableRequests.Clear();
            foreach (IDocument document in documents.Inbox.Open)
                if (document is SupportRequestDocument request && !containsStartup(request.TargetCompany))
                    unavailableRequests.Add(request);
            foreach (SupportRequestDocument request in unavailableRequests)
            {
                request.cancel();
                documents.Inbox.Resolve(request, false);
            }
        }

        public static bool needsSupport(Company company) =>
            company.state.getStat(CompanyStat.PLAYER_SHARES) > 0f &&
            (company.state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH) < 0f ||
             company.state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) < 0f);

        private bool issueRequest(Company company) =>
            documents.Inbox.Add(new SupportRequestDocument(company,
                clock.CurrentTurn + settings.supportRequestDeadlineTurns, employees, jobs, companies));

        private bool containsStartup(Company company)
        {
            foreach (Company startup in companies.StartupList)
                if (startup == company) return true;
            return false;
        }

        private void handleResolved(IApprovalDocument document, bool approved)
        {
            if (document is SupportRequestDocument request && !approved && !request.isCancelled)
                nextRequestTurns[request.DedupKey] = clock.CurrentTurn + settings.supportRequestRetryTurns;
        }

        private void restoreRetryTurns(int turn)
        {
            nextRequestTurns.Clear();
            foreach (ResolvedDocument resolved in documents.Inbox.History)
            {
                if (!(resolved.document is SupportRequestDocument request) || resolved.approved || request.isCancelled) continue;
                int nextTurn = resolved.resolvedTurn + settings.supportRequestRetryTurns;
                if (!nextRequestTurns.TryGetValue(request.DedupKey, out int current) || nextTurn > current)
                    nextRequestTurns[request.DedupKey] = nextTurn;
            }
        }

        [ContextMenu("테스트/인원 요청 문서 발행")]
        private void issueTestRequest()
        {
            foreach (Company company in companies.StartupList)
                if (issueRequest(company)) return;
            Debug.LogWarning("발행할 수 있는 스타트업이 없습니다. 기존 인원 요청서를 먼저 처리해 주세요.", this);
        }

        [ContextMenu("테스트/인원 요청 문서 발행", true)]
        private bool canIssueTestRequest() => Application.isPlaying;
    }
}
