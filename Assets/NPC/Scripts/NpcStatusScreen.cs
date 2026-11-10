using UnityEngine;
using UnityEngine.EventSystems;

namespace TakeOver.NPC
{
    /// <summary>테스트 상태를 조작하지 않고 공용 NPC 정보 패널을 표시하는 독립 프리팹 화면.</summary>
    public sealed class NpcStatusScreen : MonoBehaviour
    {
        [SerializeField] private NpcStateRegistry registry;
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
        }

        public void Close() => gameObject.SetActive(false);

        public void Configure(NpcStateRegistry stateRegistry)
        {
            registry = stateRegistry;
            selectedNpcId = "";
            ResolveRegistry();
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
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ResolveRegistry()
        {
            if (registry == null) registry = FindFirstObjectByType<NpcStateRegistry>();
            if (registry == null) return;
            if (registry.TryGetProfile(selectedNpcId, out var selected) && selected.profile != null) return;
            foreach (var profile in registry.Profiles)
                if (profile != null && profile.profile != null) { selectedNpcId = profile.npcId; break; }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use(); Close(); return;
            }
            ResolveRegistry();
            var scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var offset = new Vector2((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f);
            bool tutorial = TakeOver.Story.StoryDirector.IsInspectingProfile;
            if (tutorial)
            {
                offset += new Vector2(356 * scale,40 * scale);
                scale *= .70f;
            }
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -100;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.BeginGroup(new Rect(40, 20, 1200, 680));
            GUI.color = new Color(.065f, .085f, .12f, .98f);
            GUI.DrawTexture(new Rect(0, 0, 1200, 680), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(new Rect(0, 0, 1200, 680), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(20, 8, 900, 38), "NPC 프로필 / 관계", new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold });
            bool close = GUI.Button(new Rect(956, 8, 224, 38), "닫기", new GUIStyle(GUI.skin.button) { fontSize = 16 });
            if (registry != null && registry.TryGetProfile(selectedNpcId, out var selected) && registry.TryGet(selectedNpcId, out var state))
            {
                DrawSelector();
                NpcInfoPanel.Draw(selected, state, new GUIStyle(GUI.skin.label) { fontSize = tutorial ? 20 : 16, wordWrap = true }, ref infoScroll);
            }
            else GUI.Label(new Rect(20, 154, 1160, 40), "표시할 NPC 정보가 없습니다.", new GUIStyle(GUI.skin.label) { fontSize = 20 });
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
            if (close) Close();
        }

        private void DrawSelector()
        {
            const float viewportWidth = 1160, tabWidth = 232, tabHeight = 54;
            var profiles = registry.Profiles;
            var viewport = new Rect(20, 54, viewportWidth, 72);
            float contentWidth = Mathf.Max(viewportWidth, profiles.Count * tabWidth);
            float maxScroll = contentWidth - viewportWidth;
            if (selectorProfileId != selectedNpcId)
            {
                selectorProfileId = selectedNpcId;
                for (int i = 0; i < profiles.Count; i++)
                    if (profiles[i]?.npcId == selectedNpcId)
                    {
                        float left = i * tabWidth;
                        if (left < selectorScroll.x) selectorScroll.x = left;
                        if (left + tabWidth > selectorScroll.x + viewportWidth) selectorScroll.x = left + tabWidth - viewportWidth;
                        break;
                    }
            }
            selectorScroll.x = Mathf.Clamp(selectorScroll.x, 0, maxScroll);
            selectorScroll.y = 0;
            if (maxScroll > 0 && Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition))
            {
                var delta = Mathf.Abs(Event.current.delta.x) > 0 ? Event.current.delta.x : Event.current.delta.y;
                selectorScroll.x = Mathf.Clamp(selectorScroll.x + delta * 40, 0, maxScroll);
                Event.current.Use();
            }
            selectorScroll = GUI.BeginScrollView(viewport, selectorScroll, new Rect(0, 0, contentWidth, tabHeight), true, false, GUI.skin.horizontalScrollbar, GUIStyle.none);
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile?.profile == null) continue;
                var style = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = profile.npcId == selectedNpcId ? FontStyle.Bold : FontStyle.Normal, wordWrap = true };
                if (GUI.Button(new Rect(i * tabWidth, 0, tabWidth - 6, tabHeight), $"{profile.profile.displayName}\n{profile.profile.role}", style)) SelectNpc(profile.npcId);
            }
            GUI.EndScrollView();
        }
    }
}
