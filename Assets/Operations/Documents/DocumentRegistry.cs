using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class DocumentRegistry
    {
        private readonly Dictionary<string, Func<DocumentSaveRecord, DocumentContext, IApprovalDocument>> factories
            = new Dictionary<string, Func<DocumentSaveRecord, DocumentContext, IApprovalDocument>>
            {
                { DividendPolicyDocument.typeId, (record, context) => new DividendPolicyDocument(
                    context.companies.getCompany(record.companyId), record.deadlineTurn, context.settings) },
                { SampleChoiceDocument.typeId, (record, context) => new SampleChoiceDocument(record.deadlineTurn) },
                { StartupFundingDocument.typeId, StartupFundingDocument.restore }
            };

        public IApprovalDocument Restore(DocumentSaveRecord record, DocumentContext context)
        {
            if (!factories.TryGetValue(record.type, out var factory))
            {
                Debug.LogWarning($"[Documents] 알 수 없는 문서 종류: {record.type}");
                return null;
            }
            IApprovalDocument document = factory(record, context);
            if (document == null) return null;
            foreach (NameValue field in record.fields)
            {
                bool isChoice = false;
                foreach (DocumentChoiceField choice in document.ChoiceFields)
                    if (choice.name == field.name) { isChoice = true; break; }
                if (isChoice) document.SetChoice(field.name, Mathf.RoundToInt(field.value));
                else document.SetField(field.name, field.value);
            }
            return document;
        }
    }
}
