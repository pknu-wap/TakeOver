using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Operations
{
    public sealed class DocumentTabView : MonoBehaviour
    {
        [SerializeField] private GameObject listRoot;
        [SerializeField] private Transform openRoot;
        [SerializeField] private Transform historyRoot;
        [SerializeField] private DocumentButton rowTemplate;
        [SerializeField] private GameObject openEmpty;
        [SerializeField] private GameObject historyEmpty;
        [SerializeField] private DocumentDetailView detail;
        private readonly List<GameObject> rows = new List<GameObject>();
        private DocumentInbox inbox;
        private GameTurnClock clock;

        private void Start()
        {
            var system = FindFirstObjectByType<DocumentSystem>();
            inbox = system != null ? system.Inbox : null;
            clock = FindFirstObjectByType<GameTurnClock>();
            if (inbox != null) inbox.Changed += refresh;
            clock.TurnAdvanced += onTurn;
            detail.Decided += decide;
            detail.Closed += showList;
            showList();
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
            detail.Closed -= showList;
        }

        private void onTurn(int turn) => refresh();

        private void decide(IApprovalDocument doc, bool approved)
        {
            inbox.Resolve(doc, approved);
            showList();
        }

        private void showList()
        {
            detail.gameObject.SetActive(false);
            listRoot.SetActive(true);
            refresh();
        }

        private void refresh()
        {
            foreach (var row in rows) { row.SetActive(false); Destroy(row); }
            rows.Clear();
            rowTemplate.gameObject.SetActive(false);
            openEmpty.SetActive(inbox == null || inbox.PendingCount == 0);
            historyEmpty.SetActive(inbox == null || inbox.History.Count == 0);
            if (inbox == null) return;
            foreach (var doc in inbox.Open)
            {
                int remaining = doc.DeadlineTurn - clock.CurrentTurn;
                string deadline = remaining <= 0 ? "기한 지남" : $"남은 기한 {remaining}턴";
                var row = addRow(openRoot);
                row.SetColor(remaining <= 0 ? new Color(.72f, .15f, .12f) : new Color(.15f, .18f, .15f));
                row.Bind($"{doc.Title} · {doc.TargetCompany?.definition.companyName} · {deadline}", () => showDetail(doc, null));
            }
            foreach (var entry in inbox.History)
            {
                string values = string.Join(", ", entry.fields.Select(f => $"{f.name}={f.value:P0}"));
                var row = addRow(historyRoot);
                row.Bind($"{entry.document.Title} · {entry.document.TargetCompany?.definition.companyName} · {(entry.approved ? "승인" : "거절")} · 처리 {entry.resolvedTurn}턴 · {values}", () => showDetail(entry.document, entry));
            }
        }

        private DocumentButton addRow(Transform parent)
        {
            var row = Instantiate(rowTemplate, parent, false);
            row.gameObject.SetActive(true);
            rows.Add(row.gameObject);
            return row;
        }

        private void showDetail(IApprovalDocument doc, ResolvedDocument resolved)
        {
            listRoot.SetActive(false);
            detail.Show(doc, resolved);
        }
    }
}
