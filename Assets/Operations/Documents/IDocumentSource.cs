namespace TakeOver.Documents
{
    public interface IDocumentSource
    {
        void Generate(int turn, DocumentInbox inbox);
    }
}
