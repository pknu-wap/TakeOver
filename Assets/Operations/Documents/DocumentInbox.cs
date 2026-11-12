using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class DocumentInbox
    {
        private readonly List<IApprovalDocument> open = new List<IApprovalDocument>();
        private readonly List<ResolvedDocument> history = new List<ResolvedDocument>();
        private readonly ChangeApplier applier;
        private readonly GameTurnClock clock;
        private readonly int historyLimit;
        public IReadOnlyList<IApprovalDocument> Open => open;
        public IReadOnlyList<ResolvedDocument> History => history;
        public int PendingCount => open.Count;
        public event Action Changed;
        public event Action<IApprovalDocument, bool> DocumentResolved;

        public DocumentInbox(ChangeApplier applier, GameTurnClock clock, int historyLimit)
        {
            this.applier = applier;
            this.clock = clock;
            this.historyLimit = historyLimit;
        }

        public bool Add(IApprovalDocument document)
        {
            foreach (IApprovalDocument current in open)
                if (current.TypeId == document.TypeId && current.TargetCompany == document.TargetCompany) return false;
            open.Add(document);
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
            foreach (IApprovalDocument document in open) records.Add(document.ToSaveRecord());
            foreach (ResolvedDocument entry in history)
            {
                DocumentSaveRecord record = entry.document.ToSaveRecord();
                record.fields = new List<NameValue>(entry.fields);
                record.resolved = true;
                record.approved = entry.approved;
                record.resolvedTurn = entry.resolvedTurn;
                records.Add(record);
            }
            return records;
        }

        public void RestoreFrom(IReadOnlyList<DocumentSaveRecord> records, DocumentRegistry registry, DocumentContext context)
        {
            open.Clear();
            history.Clear();
            foreach (DocumentSaveRecord record in records)
            {
                IApprovalDocument document = registry.Restore(record, context);
                if (document == null) continue;
                if (record.resolved)
                    history.Add(new ResolvedDocument(document, record.approved, record.resolvedTurn, record.fields));
                else Add(document);
            }
            trimHistory();
            Changed?.Invoke();
        }

        private void trimHistory()
        {
            while (history.Count > historyLimit) history.RemoveAt(0);
        }
    }
}
