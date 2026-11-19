using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed partial class DocumentSystem : MonoBehaviour, ITurnBlocker
    {
        [SerializeField] private DocumentSettings settings;
        private CompanyRuleRunner runner;
        private GameTurnClock clock;
        private List<IDocumentSource> sources;
        public DocumentInbox Inbox { get; private set; }

        private void Awake()
        {
            runner = FindFirstObjectByType<CompanyRuleRunner>();
            Companies companies = FindFirstObjectByType<Companies>();
            Player player = FindFirstObjectByType<Player>();
            clock = FindFirstObjectByType<GameTurnClock>();
            Inbox = new DocumentInbox(new ChangeApplier(player), clock, settings.historyLimit);
            sources = new List<IDocumentSource> { new DividendPolicySource(companies, settings) };

            StartupSystem startupSystem = FindFirstObjectByType<StartupSystem>();
            if (startupSystem != null)
            {
                sources.Add(new StartupFundingSource(companies, startupSystem, player));
                sources.Add(new StartupListingSource(companies, startupSystem));
                sources.Add(new StartupFollowUpSource(companies, startupSystem));
                sources.Add(new StartupBusinessPlanSource(companies, startupSystem, clock));
            }
        }

        private void Start()
        {
            runner.RulesCompleted += handleRulesCompleted;
            clock.RegisterBlocker(this);
        }

        private void OnDestroy()
        {
            if (runner != null) runner.RulesCompleted -= handleRulesCompleted;
            if (clock != null) clock.UnregisterBlocker(this);
        }

        private void handleRulesCompleted(int turn)
        {
            foreach (IDocumentSource source in sources) source.Generate(turn, Inbox);
        }

        // 검증용 임시 문서. 실제 게임 흐름에는 연결하지 않는다.
        [ContextMenu("Sample/읽기 전용 샘플 2개 추가")]
        private void addSampleReports()
        {
            for (int number = 1; number <= 2; number++)
            {
                List<DocumentInfo> rows = new List<DocumentInfo>();
                for (int row = 1; row <= 6; row++) rows.Add(new DocumentInfo($"항목 {row}", $"값 {number}-{row}"));
                Inbox.Add(new SnapshotDocument("sample-report", $"임시 보고서 {number}", "검증 담당", rows));
            }
        }

        [ContextMenu("Sample/선택칸 승인 샘플 추가")]
        private void addSampleChoice()
        {
            Inbox.Add(new SampleChoiceDocument(clock.CurrentTurn + 7));
        }

        public bool IsBlocking(out string reason)
        {
            reason = "기한이 지난 결재 문서가 있습니다";
            return Inbox.HasOverdue(clock.CurrentTurn);
        }
    }
}
