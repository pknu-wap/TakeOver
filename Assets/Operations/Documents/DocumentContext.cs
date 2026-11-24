using TakeOver.NPC;
using TakeOver.Startups;

namespace TakeOver.Documents
{
    public sealed class DocumentContext
    {
        public Companies companies { get; }
        public DocumentSettings settings { get; }
        public Player player { get; }
        public StartupSystem startupSystem { get; }
        public GameTurnClock clock { get; }
        public AcquisitionOperationsService acquisitionOperations { get; }

        public DocumentContext(Companies companies, DocumentSettings settings,
            Player player = null, StartupSystem startupSystem = null, GameTurnClock clock = null,
            AcquisitionOperationsService acquisitionOperations = null)
        {
            this.companies = companies;
            this.settings = settings;
            this.player = player;
            this.startupSystem = startupSystem;
            this.clock = clock;
            this.acquisitionOperations = acquisitionOperations;
        }
    }
}
