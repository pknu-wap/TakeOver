namespace TakeOver.Documents
{
    public sealed class DocumentInfo
    {
        public string label;
        public string value;
        public bool isNotice;

        public DocumentInfo(string label, string value, bool isNotice = false)
        {
            this.label = label;
            this.value = value;
            this.isNotice = isNotice;
        }
    }
}
