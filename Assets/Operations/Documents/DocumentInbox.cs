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
        private readonly List<IApprovalDocument> open = new List<IApprovalDocument>();
        private readonly List<ResolvedDocument> history = new List<ResolvedDocument>();
        private readonly Dictionary<IApprovalDocument, int> arrivals = new Dictionary<IApprovalDocument, int>();
        private int nextArrival = 1;
        private readonly ChangeApplier applier;
        private readonly GameTurnClock clock;
        private readonly int historyLimit;
        public IReadOnlyList<IApprovalDocument> Open => open;
        public IReadOnlyList<ResolvedDocument> History => history;
        public int PendingCount => open.Count;
        public event Action Changed;
        public event Action<IApprovalDocument, bool> DocumentResolved;
        public int GetArrival(IApprovalDocument document) => arrivals[document];

        public DocumentInbox(ChangeApplier applier, GameTurnClock clock, int historyLimit)
        {
            this.applier = applier;
            this.clock = clock;
            this.historyLimit = historyLimit;
        }

        public bool Add(IApprovalDocument document)
        {
            return add(document, nextArrival);
        }

        private bool add(IApprovalDocument document, int arrival)
        {
            foreach (IApprovalDocument current in open)
                if (current.TypeId == document.TypeId && current.TargetCompany == document.TargetCompany) return false;
            open.Add(document);
            arrivals.Add(document, arrival);
            nextArrival = Math.Max(nextArrival, arrival + 1);
            Debug.Log($"[Documents] Add type={document.TypeId} company={document.TargetCompany?.definition.companyName} deadline={document.DeadlineTurn}");
            Changed?.Invoke();
            return true;
        }

        public void Resolve(IApprovalDocument document, bool approved)
        {
            if (!open.Contains(document)) return;
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

        public bool HasOverdue(int currentTurn)
        {
            foreach (IApprovalDocument document in open)
                if (currentTurn >= document.DeadlineTurn) return true;
            return false;
        }

        public List<DocumentSaveRecord> ToSaveRecords()
        {
            List<DocumentSaveRecord> records = new List<DocumentSaveRecord>();
            foreach (IApprovalDocument document in open)
            {
                DocumentSaveRecord record = document.ToSaveRecord();
                record.arrival = arrivals[document];
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
                records.Add(record);
            }
            return records;
        }

        public void RestoreFrom(IReadOnlyList<DocumentSaveRecord> records, DocumentRegistry registry, DocumentContext context)
        {
            open.Clear();
            history.Clear();
            arrivals.Clear();
            bool legacy = records.All(record => record.arrival == 0);
            nextArrival = legacy ? 1 : records.Max(record => record.arrival) + 1;
            foreach (DocumentSaveRecord record in records.OrderBy(record => record.arrival))
            {
                IApprovalDocument document = registry.Restore(record, context);
                if (document == null) continue;
                int arrival = legacy ? nextArrival : record.arrival;
                if (record.resolved)
                {
                    history.Add(new ResolvedDocument(document, record.approved, record.resolvedTurn, record.fields));
                    arrivals.Add(document, arrival);
                    nextArrival = Math.Max(nextArrival, arrival + 1);
                }
                else add(document, arrival);
            }
            trimHistory();
            Changed?.Invoke();
        }

        private void trimHistory()
        {
            while (history.Count > historyLimit)
            {
                arrivals.Remove(history[0].document);
                history.RemoveAt(0);
            }
        }
    }
}
