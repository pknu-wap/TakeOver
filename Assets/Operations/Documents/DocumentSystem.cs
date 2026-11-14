using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class DocumentSystem : MonoBehaviour, ITurnBlocker
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

        public bool IsBlocking(out string reason)
        {
            reason = "기한이 지난 결재 문서가 있습니다";
            return Inbox.HasOverdue(clock.CurrentTurn);
        }
    }
}
