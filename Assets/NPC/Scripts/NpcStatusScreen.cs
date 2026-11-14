using UnityEngine;
using UnityEngine.EventSystems;

namespace TakeOver.NPC
{
    /// <summary>테스트 상태를 조작하지 않고 공용 NPC 정보 패널을 표시하는 독립 프리팹 화면.</summary>
    public sealed class NpcStatusScreen : MonoBehaviour
    {
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private NpcProfilePanelView profilePanel;

        private string selectedNpcId = "";
        private Vector2 selectorScroll;
        private Vector2 infoScroll;
        private string selectorProfileId;

        public string SelectedNpcId => selectedNpcId;
        public int NpcCount => registry != null ? registry.Profiles.Count : 0;

        public void Open()
        {
            gameObject.SetActive(true);
            ResolveRegistry();
            SyncProfilePanel(true);
        }

        public void Close()
        {
            SyncProfilePanel(false);
            gameObject.SetActive(false);
        }

        public void Configure(NpcStateRegistry stateRegistry)
        {
            registry = stateRegistry;
            selectedNpcId = "";
            ResolveRegistry();
            SyncProfilePanel(gameObject.activeInHierarchy);
        }

        public void SelectNpc(string npcId)
        {
            ResolveRegistry();
            if (registry == null || !registry.TryGetProfile(npcId, out var profile) || profile.profile == null) return;
            if (selectedNpcId != npcId) infoScroll = Vector2.zero;
            selectedNpcId = npcId;
        }

        private void OnEnable()
        {
            ResolveRegistry();
            SyncProfilePanel(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDisable()
        {
            SyncProfilePanel(false);
        }

        private void ResolveRegistry()
        {
            if (registry == null) registry = FindFirstObjectByType<NpcStateRegistry>();
            if (profilePanel == null) profilePanel = GetComponentInChildren<NpcProfilePanelView>(true);
            if (registry == null) return;
            if (registry.TryGetProfile(selectedNpcId, out var selected) && selected.profile != null) return;
            foreach (var profile in registry.Profiles)
                if (profile != null && profile.profile != null) { selectedNpcId = profile.npcId; break; }
        }

        private void SyncProfilePanel(bool open)
        {
            if (profilePanel == null) profilePanel = GetComponentInChildren<NpcProfilePanelView>(true);
            if (profilePanel == null) return;

            // 튜토리얼 연출 중일 때 기존 OnGUI와 동일한 위치/축소 비율 자동 적용
            RectTransform panelRect = profilePanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                bool tutorial = TakeOver.Story.StoryDirector.IsInspectingProfile;
                if (tutorial)
                {
                    panelRect.localScale = new Vector3(0.70f, 0.70f, 1f);
                    panelRect.anchoredPosition = new Vector2(356f, -40f);
                }
                else
                {
                    panelRect.localScale = Vector3.one;
                    panelRect.anchoredPosition = Vector2.zero;
                }
            }

            if (open) profilePanel.Open();
            else profilePanel.Close();
        }
    }
}