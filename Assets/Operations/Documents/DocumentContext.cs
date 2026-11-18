using TakeOver.Startups;

namespace TakeOver.Documents
{
    public sealed class DocumentContext
    {
        public Companies companies { get; }
        public DocumentSettings settings { get; }
        public Player player { get; }
        public StartupSystem startupSystem { get; }

        public DocumentContext(Companies companies, DocumentSettings settings,
            Player player = null, StartupSystem startupSystem = null)
        {
            this.companies = companies;
            this.settings = settings;
            this.player = player;
            this.startupSystem = startupSystem;
        }
    }
}
