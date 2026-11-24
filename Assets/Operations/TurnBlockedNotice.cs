using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Operations
{
    public sealed class TurnBlockedNotice : MonoBehaviour
    {
        private GameTurnClock clock;
        private OperationsScreen operations;

        private void Start()
        {
            clock = FindFirstObjectByType<GameTurnClock>();
            operations = FindFirstObjectByType<OperationsScreen>(FindObjectsInactive.Include);
            clock.AdvanceBlocked += show;
        }

        private void OnDestroy()
        {
            if (clock != null) clock.AdvanceBlocked -= show;
        }

        private void show(string reason)
        {
            FindFirstObjectByType<FloatingMessage>()?.Show(reason);
            if (reason == DocumentSystem.OverdueBlockReason) operations.OpenDocumentsTab();
        }
    }
}
