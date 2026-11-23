using System.Collections.Generic;
using TakeOver.Documents;
using TakeOver.Employees;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Startups
{
    public sealed class StartupDiscoveryService : MonoBehaviour
    {
        public const string emptyReportTypeId = "startup-discovery-empty";

        [SerializeField] private StartupDiscoverySettings settings;

        [Header("검증용 (플레이 중 컴포넌트 우클릭 메뉴에서 사용)")]
        [SerializeField, Range(0, 20)] private int testLegwork = 10;
        [SerializeField, Range(0, 20)] private int testEye = 10;
        [SerializeField, Range(0, 20)] private int testAnalysis = 10;

        private readonly List<JobCompletion> completed = new List<JobCompletion>();
        private readonly HashSet<string> passedIds = new HashSet<string>();
        private readonly Dictionary<string, int> reviewRequestedTurns = new Dictionary<string, int>();
        private readonly Dictionary<string, int> shownCounts = new Dictionary<string, int>();

        private System.Random random;
        private Companies companies;
        private JobService jobs;
        private DocumentSystem documents;
        private GameTurnClock clock;
        private StartupContractService contracts;
        private StartupSystem startups;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogWarning("StartupDiscoveryService: StartupDiscoverySettings 에셋이 비어 있어 코드 기본값으로 진행합니다.", this);
                settings = ScriptableObject.CreateInstance<StartupDiscoverySettings>();
            }
            random = settings.randomSeed != 0 ? new System.Random(settings.randomSeed) : new System.Random();
        }

        private void Start()
        {
            companies = FindFirstObjectByType<Companies>();
            jobs = FindFirstObjectByType<JobService>();
            documents = FindFirstObjectByType<DocumentSystem>();
            clock = FindFirstObjectByType<GameTurnClock>();
            contracts = FindFirstObjectByType<StartupContractService>();
            startups = FindFirstObjectByType<StartupSystem>();

            if (jobs != null) jobs.OnJobCompleted += handleJobCompleted;
            if (clock != null) clock.DayStarted += publishReports;
            if (documents != null) documents.Inbox.DocumentResolved += handleDocumentResolved;
        }

        private void OnDestroy()
        {
            if (jobs != null) jobs.OnJobCompleted -= handleJobCompleted;
            if (clock != null) clock.DayStarted -= publishReports;
            if (documents != null && documents.Inbox != null) documents.Inbox.DocumentResolved -= handleDocumentResolved;
        }

        private void handleJobCompleted(JobCompletion completion)
        {
            if (completion.Job == JobType.Discovery) completed.Add(completion);
        }

        private void publishReports(int turn)
        {
            foreach (JobCompletion completion in completed)
            {
                Employee employee = completion.Employee;
                createReport(employee.Name, employee.Legwork.Current, employee.Eye.Current,
                    employee.Analysis.Current, turn);
            }
            completed.Clear();
        }

        private void createReport(string author, int legwork, int eye, int analysis, int turn)
        {
            if (companies == null || documents == null) return;

            List<Company> pool = collectCandidatePool(turn);
            if (pool.Count == 0)
            {
                documents.Inbox.Add(new SnapshotDocument(emptyReportTypeId, "투자 후보 없음 보고", author,
                    System.Array.Empty<DocumentInfo>()));
                return;
            }

            List<float> growthRates = new List<float>();
            foreach (Company startup in pool) growthRates.Add(startup.definition.growthRate);

            int count = StartupDiscovery.getCandidateCount(legwork, settings);
            foreach (int index in StartupDiscovery.pickIndices(growthRates, count, eye, settings, random))
            {
                Company startup = pool[index];
                string id = startup.definition.companyID;
                StartupDiscovery.Outlook truth = StartupDiscovery.getTrueOutlook(growthRates[index], settings);
                StartupDiscovery.Outlook shown = StartupDiscovery.readOutlook(truth, analysis, settings, random);

                shownCounts.TryGetValue(id, out int timesShown);
                timesShown++;
                StartupDiscoveryDocument report = new StartupDiscoveryDocument(startup, companies, author,
                    turn + settings.deadlineTurns, StartupDiscovery.describe(shown),
                    timesShown >= settings.maxTimesShown);
                if (documents.Inbox.Add(report)) shownCounts[id] = timesShown;
            }
        }

        private List<Company> collectCandidatePool(int turn)
        {
            HashSet<string> openIds = new HashSet<string>();
            foreach (IDocument document in documents.Inbox.Open)
                if (document is StartupDiscoveryDocument report) openIds.Add(report.CompanyId);

            List<Company> pool = new List<Company>();
            foreach (Company startup in companies.StartupList)
            {
                string id = startup.definition.companyID;
                if (isContracted(startup)) continue;
                if (passedIds.Contains(id) || openIds.Contains(id)) continue;
                if (shownCounts.TryGetValue(id, out int timesShown) && timesShown >= settings.maxTimesShown) continue;
                if (reviewRequestedTurns.TryGetValue(id, out int requestedTurn) && turn - requestedTurn <= 1) continue;
                pool.Add(startup);
            }
            return pool;
        }

        private bool isContracted(Company startup)
        {
            if (startups != null && startups.tryGetRecord(startup.definition.companyID, out StartupRecord record)
                && record.contracted) return true;
            return startup.state.getStat(CompanyStat.PLAYER_SHARES) > 0f;
        }

        private void handleDocumentResolved(IApprovalDocument document, bool approved)
        {
            if (!(document is StartupDiscoveryDocument report)) return;
            if (approved) requestReview(report);
            else passedIds.Add(report.CompanyId);
        }

        private void requestReview(StartupDiscoveryDocument report)
        {
            reviewRequestedTurns[report.CompanyId] = clock != null ? clock.CurrentTurn : 0;
            if (contracts == null)
            {
                Debug.Log($"[Discovery] 계약 창구가 씬에 없어 투자 검토만 기록함: {report.CompanyName}");
                return;
            }
            if (!contracts.requestContract(report.TargetCompany))
                Debug.LogWarning($"[Discovery] 계약 요청이 거절됨(이미 투자했거나 요청이 있음): {report.CompanyName}");
        }

        [ContextMenu("검증용: 발굴 보고 바로 만들기 (검증용 스텟)")]
        private void createTestReport()
        {
            if (!Application.isPlaying || clock == null) return;
            createReport("검증용 직원", testLegwork, testEye, testAnalysis, clock.CurrentTurn);
        }

        [ContextMenu("검증용: 안목·분석력 효과 통계")]
        private void logStatistics()
        {
            if (!Application.isPlaying || companies == null || clock == null) return;
            List<Company> pool = collectCandidatePool(clock.CurrentTurn);
            if (pool.Count < 2)
            {
                Debug.Log("[Discovery] 후보가 2곳 미만이라 통계를 낼 수 없습니다.");
                return;
            }

            List<float> growthRates = new List<float>();
            foreach (Company startup in pool) growthRates.Add(startup.definition.growthRate);
            System.Random testRandom = new System.Random(1);
            const int tries = 1000;

            foreach (int eye in new[] { 0, 10, 20 })
            {
                float sum = 0f;
                for (int i = 0; i < tries; i++)
                    sum += growthRates[StartupDiscovery.pickIndices(growthRates, 1, eye, settings, testRandom)[0]];
                Debug.Log($"[Discovery] 안목 {eye}: 뽑힌 회사 평균 성장률 {sum / tries:P2} (후보 {pool.Count}곳)");
            }

            foreach (int analysis in new[] { 0, 10, 20 })
            {
                int correct = 0;
                for (int i = 0; i < tries; i++)
                {
                    StartupDiscovery.Outlook truth = (StartupDiscovery.Outlook)testRandom.Next(3);
                    if (StartupDiscovery.readOutlook(truth, analysis, settings, testRandom) == truth) correct++;
                }
                Debug.Log($"[Discovery] 분석력 {analysis}: 전망 적중률 {correct / (float)tries:P0} " +
                    $"(설정값 {StartupDiscovery.getAccuracy(analysis, settings):P0})");
            }
        }
    }
}
