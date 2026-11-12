namespace TakeOver.Documents
{
    public sealed class DocumentContext
    {
        public Companies companies { get; }
        public DocumentSettings settings { get; }

        public DocumentContext(Companies companies, DocumentSettings settings)
        {
            this.companies = companies;
            this.settings = settings;
        }
    }
}
