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
                    context.companies.getCompany(record.companyId), record.deadlineTurn, context.settings) }
            };

        public IApprovalDocument Restore(DocumentSaveRecord record, DocumentContext context)
        {
            if (!factories.TryGetValue(record.type, out var factory))
            {
                Debug.LogWarning($"[Documents] 알 수 없는 문서 종류: {record.type}");
                return null;
            }
            IApprovalDocument document = factory(record, context);
            foreach (NameValue field in record.fields) document.SetField(field.name, field.value);
            return document;
        }
    }
}
