using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>Layout/Repaint 사이에 스타일을 재사용하고 사용 중인 스킨이 바뀌면 다시 만든다.</summary>
    internal sealed class NpcTestGuiStyles
    {
        internal static readonly string[] BodyTabs = { "관계·Memory 조작", "선호·불호", "이벤트 미리보기" };
        private GUISkin skin;
        internal GUIStyle Title { get; private set; }
        internal GUIStyle Label { get; private set; }
        internal GUIStyle ProfileText { get; private set; }
        internal GUIStyle Button { get; private set; }
        internal GUIStyle FooterButton { get; private set; }
        internal GUIStyle Log { get; private set; }
        internal GUIStyle Tab { get; private set; }
        internal GUIStyle SelectedTab { get; private set; }

        internal void Ensure(GUISkin currentSkin)
        {
            if (skin == currentSkin && Title != null) return;
            skin = currentSkin;
            Title = new GUIStyle(skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            Label = new GUIStyle(skin.label) { fontSize = 20 };
            ProfileText = new GUIStyle(skin.label) { fontSize = 16, wordWrap = true };
            Button = new GUIStyle(skin.button) { fontSize = 20 };
            FooterButton = new GUIStyle(skin.button) { fontSize = 16 };
            Log = new GUIStyle(ProfileText) { fontSize = 14 };
            Tab = new GUIStyle(skin.button) { fontSize = 16, fontStyle = FontStyle.Normal, wordWrap = true };
            SelectedTab = new GUIStyle(Tab) { fontStyle = FontStyle.Bold };
        }
    }
}
