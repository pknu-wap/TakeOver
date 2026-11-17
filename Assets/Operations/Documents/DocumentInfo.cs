namespace TakeOver.Documents
{
    public sealed class DocumentInfo
    {
        public string label;
        public string value;

        public DocumentInfo(string label, string value)
        {
            this.label = label;
            this.value = value;
        }
    }
}
