using System.Collections.Generic;
using System.Globalization;

namespace TakeOver.Documents
{
    // 읽기 전용 문서의 범용 구현. 저장할 때 제목·작성자·정보 줄을 그대로 스냅샷으로 남긴다.
    public sealed class SnapshotDocument : IDocument
    {
        private readonly List<DocumentInfo> infoRows;
        public string TypeId { get; }
        public string Title { get; }
        public string Author { get; }
        public string BodyId => "generic";
        public string DedupKey { get; }
        public IReadOnlyList<DocumentInfo> InfoRows => infoRows;
        public int RowsPerPage { get; }

        public SnapshotDocument(string typeId, string title, string author, IEnumerable<DocumentInfo> infoRows,
            string dedupKey = "", int rowsPerPage = 0)
        {
            TypeId = typeId;
            Title = title;
            Author = author ?? "";
            DedupKey = dedupKey ?? "";
            this.infoRows = new List<DocumentInfo>(infoRows);
            RowsPerPage = rowsPerPage;
        }

        public static SnapshotDocument FromRecord(DocumentSaveRecord record)
        {
            List<DocumentInfo> rows = new List<DocumentInfo>();
            int rowsPerPage = 0;
            if (record.infoRows != null)
                foreach (NameText row in record.infoRows)
                {
                    if (row.label == "rows-per-page") rowsPerPage = int.Parse(row.value, CultureInfo.InvariantCulture);
                    else rows.Add(new DocumentInfo(row.label, row.value));
                }
            return new SnapshotDocument(record.type, record.title, record.author, rows, rowsPerPage: rowsPerPage);
        }

        public DocumentSaveRecord ToSaveRecord()
        {
            DocumentSaveRecord record = new DocumentSaveRecord
            {
                type = TypeId,
                snapshot = true,
                title = Title,
                author = Author,
                fields = new List<NameValue>(),
                infoRows = new List<NameText>()
            };
            foreach (DocumentInfo row in infoRows) record.infoRows.Add(new NameText { label = row.label, value = row.value });
            if (RowsPerPage > 0) record.infoRows.Add(new NameText { label = "rows-per-page", value = RowsPerPage.ToString(CultureInfo.InvariantCulture) });
            return record;
        }
    }
}
