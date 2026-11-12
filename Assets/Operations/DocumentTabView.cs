using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Operations
{
    public sealed class DocumentTabView : MonoBehaviour
    {
        [SerializeField] private Transform openRoot;
        [SerializeField] private Transform historyRoot;
        [SerializeField] private DocumentCard rowTemplate;
        [SerializeField] private GameObject openEmpty;
        [SerializeField] private GameObject historyEmpty;
        [SerializeField] private DocumentDetailView detail;
        private readonly List<DocumentCard> cards = new List<DocumentCard>();
        private readonly List<IApprovalDocument> cardDocuments = new List<IApprovalDocument>();
        private DocumentInbox inbox;
        private GameTurnClock clock;
        private IApprovalDocument selected;

        private void Start()
        {
            var system = FindFirstObjectByType<DocumentSystem>();
            inbox = system != null ? system.Inbox : null;
            clock = FindFirstObjectByType<GameTurnClock>();
            if (inbox != null) inbox.Changed += refresh;
            clock.TurnAdvanced += onTurn;
            detail.Decided += decide;
            refresh();
        }

        private void OnEnable()
        {
            if (clock != null) refresh();
        }

        private void OnDestroy()
        {
            if (inbox != null) inbox.Changed -= refresh;
            if (clock != null) clock.TurnAdvanced -= onTurn;
            detail.Decided -= decide;
        }

        private void onTurn(int turn) => refresh();
        private void decide(IApprovalDocument doc, bool approved) => inbox.Resolve(doc, approved);

        private void refresh()
        {
            foreach (var card in cards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear();
            cardDocuments.Clear();
            rowTemplate.gameObject.SetActive(false);
            openEmpty.SetActive(inbox == null || inbox.PendingCount == 0);
            historyEmpty.SetActive(inbox == null || inbox.History.Count == 0);
            if (inbox == null) { detail.Clear(); return; }
            foreach (var doc in inbox.Open)
            {
                int remaining = doc.DeadlineTurn - clock.CurrentTurn;
                string due = remaining < 0 ? "기한 지남" : remaining == 0 ? "오늘 마감" : $"D-{remaining}";
                addCard(openRoot, doc, due, remaining <= 0, null);
            }
            foreach (var entry in inbox.History)
            {
                string values = string.Join(", ", entry.fields.Select(f => $"{f.value:P0}"));
                addCard(historyRoot, entry.document,
                    $"{(entry.approved ? "승인" : "거절")} · {entry.resolvedTurn}턴 · {values}", false, entry);
            }
            if (selected == null || !cardDocuments.Contains(selected))
                selected = cardDocuments.Count > 0 ? cardDocuments[0] : null;
            if (selected == null) { detail.Clear(); return; }
            ResolvedDocument resolved = null;
            foreach (var entry in inbox.History)
                if (entry.document == selected) { resolved = entry; break; }
            select(selected, resolved);
        }

        private void addCard(Transform parent, IApprovalDocument doc, string status, bool overdue, ResolvedDocument resolved)
        {
            var card = Instantiate(rowTemplate, parent, false);
            card.gameObject.SetActive(true);
            string title = doc.TypeId == DividendPolicyDocument.typeId
                ? $"배당 정책 · {doc.TargetCompany?.definition.companyName}" : doc.Title;
            card.Bind(title, doc.TargetCompany?.definition.companyName ?? "대상 없음", status, overdue, () => select(doc, resolved));
            cards.Add(card);
            cardDocuments.Add(doc);
        }

        private void select(IApprovalDocument doc, ResolvedDocument resolved)
        {
            selected = doc;
            for (int i = 0; i < cards.Count; i++) cards[i].SetSelected(cardDocuments[i] == doc);
            detail.Show(doc, resolved);
        }
    }
}
