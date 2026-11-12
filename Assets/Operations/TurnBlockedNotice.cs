using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class TurnBlockedNotice : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text reasonText;
        [SerializeField] private Button documents;
        [SerializeField] private Button close;
        private GameTurnClock clock;
        private OperationsScreen operations;

        private void Start()
        {
            clock = FindFirstObjectByType<GameTurnClock>();
            operations = FindFirstObjectByType<OperationsScreen>(FindObjectsInactive.Include);
            clock.AdvanceBlocked += show;
            documents.onClick.AddListener(() => { operations.OpenDocumentsTab(); panel.SetActive(false); });
            close.onClick.AddListener(() => panel.SetActive(false));
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (clock != null) clock.AdvanceBlocked -= show;
        }

        private void show(string reason)
        {
            reasonText.text = reason;
            panel.SetActive(true);
        }
    }
}
