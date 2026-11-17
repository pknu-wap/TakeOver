using System.Collections.Generic;

namespace TakeOver.Documents
{
    // 읽기 전용 문서는 approved가 의미 없고(항상 false), resolvedTurn에 읽은 턴을 담는다.
    public sealed class ResolvedDocument
    {
        public IDocument document { get; }
        public bool approved { get; }
        public int resolvedTurn { get; }
        public IReadOnlyList<NameValue> fields { get; }
        public bool isReadOnly => !(document is IApprovalDocument);

        public ResolvedDocument(IDocument document, bool approved, int resolvedTurn,
            IReadOnlyList<NameValue> fields)
        {
            this.document = document;
            this.approved = approved;
            this.resolvedTurn = resolvedTurn;
            this.fields = new List<NameValue>(fields);
        }
    }
}
