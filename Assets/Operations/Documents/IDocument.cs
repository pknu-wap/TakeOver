using System.Collections.Generic;

namespace TakeOver.Documents
{
    public interface IDocument
    {
        string TypeId { get; }
        string Title { get; }
        string Author { get; }
        string BodyId { get; }
        string DedupKey { get; }
        IReadOnlyList<DocumentInfo> InfoRows { get; }
        DocumentSaveRecord ToSaveRecord();
    }
}
