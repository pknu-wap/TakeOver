using UnityEngine;
using UnityEngine.EventSystems;

namespace TakeOver.NPC
{
    /// <summary>테스트 상태를 조작하지 않고 공용 NPC 정보 패널을 표시하는 독립 프리팹 화면.</summary>
    public sealed class NpcStatusScreen : MonoBehaviour
    {
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private NpcProfilePanelView profilePanel;

        public string SelectedNpcId => profilePanel != null ? profilePanel.SelectedNpcId : "";
        public RectTransform ProfileRect => profilePanel != null && profilePanel.gameObject.activeInHierarchy
            ? (RectTransform)profilePanel.transform : null;
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
            ResolveRegistry();
            SyncProfilePanel(gameObject.activeInHierarchy);
        }

        public void SelectNpc(string npcId)
        {
            ResolveRegistry();
            profilePanel?.SelectNpc(npcId);
        }

        public void FocusMemories() => profilePanel?.FocusMemories();
        public void SetTutorialNpc(string npcId) => profilePanel?.SetTutorialNpc(npcId);

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
        }

        private void SyncProfilePanel(bool open)
        {
            if (profilePanel == null) profilePanel = GetComponentInChildren<NpcProfilePanelView>(true);
            if (profilePanel == null) return;

            RectTransform panelRect = profilePanel.GetComponent<RectTransform>();
            if (panelRect != null && !TakeOver.Story.StoryDirector.IsInspectingProfile)
            {
                panelRect.localScale = Vector3.one;
                panelRect.anchoredPosition = Vector2.zero;
            }

            if (open) profilePanel.Open();
            else profilePanel.Close();
        }
    }
}
