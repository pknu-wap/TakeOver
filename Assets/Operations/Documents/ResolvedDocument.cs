using System.Collections.Generic;

namespace TakeOver.Documents
{
    public sealed class ResolvedDocument
    {
        public IApprovalDocument document { get; }
        public bool approved { get; }
        public int resolvedTurn { get; }
        public IReadOnlyList<NameValue> fields { get; }

        public ResolvedDocument(IApprovalDocument document, bool approved, int resolvedTurn,
            IReadOnlyList<NameValue> fields)
        {
            this.document = document;
            this.approved = approved;
            this.resolvedTurn = resolvedTurn;
            this.fields = new List<NameValue>(fields);
        }
    }
}
