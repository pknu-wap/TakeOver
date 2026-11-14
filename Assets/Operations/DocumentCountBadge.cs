using TakeOver.Documents;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentCountBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;
        [SerializeField] private Text count;
        private DocumentInbox inbox;

        public void Hide()
        {
            badge.SetActive(false);
            enabled = false;
        }

        private void Start()
        {
            var system = FindFirstObjectByType<DocumentSystem>();
            inbox = system != null ? system.Inbox : null;
            if (inbox != null) inbox.Changed += refresh;
            refresh();
        }

        private void OnDestroy()
        {
            if (inbox != null) inbox.Changed -= refresh;
        }

        private void refresh()
        {
            int pending = inbox != null ? inbox.PendingCount : 0;
            badge.SetActive(pending > 0);
            count.text = pending.ToString();
        }
    }
}
