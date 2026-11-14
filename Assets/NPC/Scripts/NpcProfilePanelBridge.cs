using UnityEngine;

namespace TakeOver.NPC
{
    public class NpcProfilePanelBridge : MonoBehaviour
    {
        [SerializeField] private NpcProfilePanelView profilePanel;
        [SerializeField] private GameObject inputBlocker;

        private bool _isCurrentlyOpen;

        private void Awake()
        {
            if (profilePanel == null)
                profilePanel = GetComponentInChildren<NpcProfilePanelView>(true);

            if (inputBlocker == null)
            {
                Transform t = transform.Find("InputBlocker");
                if (t != null) inputBlocker = t.gameObject;
            }

            if (profilePanel != null)
            {
                profilePanel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // InputBlocker가 켜져 있거나(activeInHierarchy) CanvasGroup/GraphicRaycaster가 켜졌는지 감지
            bool shouldOpen = inputBlocker != null && inputBlocker.activeInHierarchy;

            if (shouldOpen != _isCurrentlyOpen)
            {
                _isCurrentlyOpen = shouldOpen;
                if (profilePanel != null)
                {
                    if (_isCurrentlyOpen) profilePanel.Open();
                    else profilePanel.Close();
                }
            }
        }
    }
}