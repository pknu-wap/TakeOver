using System.Collections.Generic;

namespace TakeOver.Documents
{
    public interface IDocumentPages
    {
        int PageCount { get; }
        IReadOnlyList<DocumentInfo> GetInfoRows(int pageIndex);
        IReadOnlyList<DocumentField> GetFields(int pageIndex);
        IReadOnlyList<DocumentChoiceField> GetChoiceFields(int pageIndex);
    }
}
