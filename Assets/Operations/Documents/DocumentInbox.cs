using System;
using System.Collections.Generic;
using System.Linq;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class DocumentInbox
    {
        // open = 열린 승인 문서 + 안 읽은 읽기 전용 문서. history = 처리한 승인 문서 + 읽은 읽기 전용 문서.
        private readonly List<IDocument> open = new List<IDocument>();
        private readonly List<ResolvedDocument> history = new List<ResolvedDocument>();
        private readonly Dictionary<IDocument, int> arrivals = new Dictionary<IDocument, int>();
        private readonly Dictionary<IDocument, int> arrivalTurns = new Dictionary<IDocument, int>();
        private int nextArrival = 1;
        private readonly ChangeApplier applier;
        private readonly GameTurnClock clock;
        private readonly int historyLimit;
        public IReadOnlyList<IDocument> Open => open;
        public IReadOnlyList<ResolvedDocument> History => history;
        public int PendingCount => open.Count;
        public event Action Changed;
        public event Action<IApprovalDocument, bool> DocumentResolved;
        public int GetArrival(IDocument document) => arrivals[document];
        public int GetArrivalTurn(IDocument document) => arrivalTurns[document];

        public DocumentInbox(ChangeApplier applier, GameTurnClock clock, int historyLimit)
        {
            this.applier = applier;
            this.clock = clock;
            this.historyLimit = historyLimit;
        }

        public bool Add(IDocument document)
        {
            return add(document, nextArrival, clock.CurrentTurn);
        }

        private bool add(IDocument document, int arrival, int arrivalTurn)
        {
            if (!string.IsNullOrEmpty(document.DedupKey))
                foreach (IDocument current in open)
                    if (current.TypeId == document.TypeId && current.DedupKey == document.DedupKey) return false;
            open.Add(document);
            arrivals.Add(document, arrival);
            arrivalTurns.Add(document, arrivalTurn);
            nextArrival = Math.Max(nextArrival, arrival + 1);
            Debug.Log($"[Documents] Add type={document.TypeId} dedup={document.DedupKey} deadline={(document as IApprovalDocument)?.DeadlineTurn.ToString() ?? "-"}");
            Changed?.Invoke();
            return true;
        }

        public void Resolve(IApprovalDocument document, bool approved)
        {
            if (IsSavePending() || !open.Contains(document)) return;
            if (approved && !document.CanApprove(out string reason))
            {
                Debug.LogWarning($"[Documents] Resolve 거부 type={document.TypeId} reason={reason}");
                return;
            }
            List<NameValue> fields = document.ToSaveRecord().fields;
            RuleResult result = approved ? document.OnApprove() : document.OnReject();
            if (document.TargetCompany != null) applier.Apply(result, document.TargetCompany);
            open.Remove(document);
            history.Add(new ResolvedDocument(document, approved, clock.CurrentTurn, fields));
            trimHistory();
            Debug.Log($"[Documents] Resolve type={document.TypeId} approved={approved} turn={clock.CurrentTurn} pending={PendingCount} history={history.Count}");
            DocumentResolved?.Invoke(document, approved);
            Changed?.Invoke();
        }

        public void MarkRead(IDocument document)
        {
            if (IsSavePending() || document is IApprovalDocument || !open.Contains(document)) return;
            open.Remove(document);
            history.Add(new ResolvedDocument(document, false, clock.CurrentTurn, new List<NameValue>()));
            trimHistory();
            Debug.Log($"[Documents] MarkRead type={document.TypeId} turn={clock.CurrentTurn} pending={PendingCount} history={history.Count}");
            Changed?.Invoke();
        }

        private static bool IsSavePending()
        {
            if (TakeOver.SaveSystem.GameSessionCoordinator.Active?.IsPersistenceBlocked != true) return false;
            UnityEngine.Object.FindFirstObjectByType<FloatingMessage>()?.Show("저장 재시도 후 처리해 주세요.");
            return true;
        }

        public bool HasOverdue(int currentTurn)
        {
            foreach (IDocument document in open)
                if (document is IApprovalDocument approval && currentTurn >= approval.DeadlineTurn) return true;
            return false;
        }

        public List<DocumentSaveRecord> ToSaveRecords()
        {
            List<DocumentSaveRecord> records = new List<DocumentSaveRecord>();
            foreach (IDocument document in open)
            {
                DocumentSaveRecord record = document.ToSaveRecord();
                record.arrival = arrivals[document];
                record.arrivalTurn = arrivalTurns[document];
                records.Add(record);
            }
            foreach (ResolvedDocument entry in history)
            {
                DocumentSaveRecord record = entry.document.ToSaveRecord();
                record.fields = new List<NameValue>(entry.fields);
                record.resolved = true;
                record.approved = entry.approved;
                record.resolvedTurn = entry.resolvedTurn;
                record.arrival = arrivals[entry.document];
                record.arrivalTurn = arrivalTurns[entry.document];
                records.Add(record);
            }
            return records;
        }

        public void RestoreFrom(IReadOnlyList<DocumentSaveRecord> records, DocumentRegistry registry, DocumentContext context)
        {
            open.Clear();
            history.Clear();
            arrivals.Clear();
            arrivalTurns.Clear();
            bool legacy = records.All(record => record.arrival == 0);
            nextArrival = legacy || records.Count == 0 ? 1 : records.Max(record => record.arrival) + 1;
            foreach (DocumentSaveRecord record in records.OrderBy(record => record.arrival))
            {
                IDocument document = record.snapshot ? SnapshotDocument.FromRecord(record) : registry.Restore(record, context);
                if (document == null) continue;
                int arrival = legacy ? nextArrival : record.arrival;
                if (record.resolved)
                {
                    history.Add(new ResolvedDocument(document, record.approved, record.resolvedTurn,
                        record.fields ?? new List<NameValue>()));
                    arrivals.Add(document, arrival);
                    arrivalTurns.Add(document, record.arrivalTurn);
                    nextArrival = Math.Max(nextArrival, arrival + 1);
                }
                else add(document, arrival, record.arrivalTurn);
            }
            trimHistory();
            Changed?.Invoke();
        }

        private void trimHistory()
        {
            while (history.Count > historyLimit)
            {
                arrivals.Remove(history[0].document);
                arrivalTurns.Remove(history[0].document);
                history.RemoveAt(0);
            }
        }
    }
}
