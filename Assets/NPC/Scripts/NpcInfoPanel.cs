using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>테스트 화면의 인물 정보다. 왼쪽에 교체 가능한 스탠딩, 오른쪽에 현재 관계와 설정을 표시한다.</summary>
    internal static class NpcInfoPanel
    {
        private static Texture2D silhouette;

        internal static void Draw(NpcStateRegistry.NpcSeed seed, NpcRuntimeState state, GUIStyle text,
            ref Vector2 scroll)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.09f, 0.13f, 0.2f);
            GUI.DrawTexture(new Rect(20, 138, 420, 514), Texture2D.whiteTexture);
            GUI.color = new Color(0.16f, 0.22f, 0.3f);
            GUI.DrawTexture(new Rect(32, 150, 396, 490), Texture2D.whiteTexture);
            GUI.color = previous;
            var portrait = seed?.profile?.standingIllustration;
            var area = new Rect(58, 160, 344, 436);
            if (portrait != null)
            {
                var source = portrait.textureRect;
                var ratio = source.width / source.height;
                var width = Mathf.Min(area.width, area.height * ratio);
                var height = width / ratio;
                var rect = new Rect(area.center.x - width / 2, area.yMax - height, width, height);
                GUI.DrawTextureWithTexCoords(rect, portrait.texture, new Rect(source.x / portrait.texture.width,
                    source.y / portrait.texture.height, source.width / portrait.texture.width, source.height / portrait.texture.height));
            }
            else
            {
                if (silhouette == null) silhouette = CreateSilhouette();
                GUI.DrawTexture(area, silhouette, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(68, 606, 330, 30), "임시 실루엣 · 일러스트 미등록", text);
            }
            GUI.Box(new Rect(456, 138, 724, 514), GUIContent.none);
            GUILayout.BeginArea(new Rect(478, 154, 680, 480));
            scroll = GUILayout.BeginScrollView(scroll);
            var heading = new GUIStyle(text) { fontSize = 24, fontStyle = FontStyle.Bold };
            var profile = seed?.profile;
            GUILayout.Label(profile?.displayName ?? state.npcId, heading);
            GUILayout.Label($"{profile?.affiliation}  ·  {profile?.role}", text);
            GUILayout.Space(18);
            GUILayout.Label("현재 관계", heading);
            var r = state.relation;
            var grid = GUILayoutUtility.GetRect(640, 138);
            Stat(new Rect(grid.x, grid.y, 300, 42), "신뢰", r.trust, -100, 100, text);
            Stat(new Rect(grid.x + 330, grid.y, 300, 42), "존중", r.respect, -100, 100, text);
            Stat(new Rect(grid.x, grid.y + 46, 300, 42), "두려움", r.fear, 0, 100, text);
            Stat(new Rect(grid.x + 330, grid.y + 46, 300, 42), "적대감", r.hostility, -100, 100, text);
            Stat(new Rect(grid.x, grid.y + 92, 300, 42), "의존도", r.dependency, 0, 100, text);
            Stat(new Rect(grid.x + 330, grid.y + 92, 300, 42), "관심", r.interest, -100, 100, text);
            GUILayout.Space(18);
            GUILayout.Label("인물 소개", heading);
            GUILayout.Label(profile?.personalitySummary ?? "설정 미등록", text);
            GUILayout.Space(12);
            GUILayout.Label(profile?.gameplayRoleSummary ?? "", text);
            GUILayout.Space(12);
            GUILayout.Label($"기억 {state.memories.Count}개 · 관계 갱신 {r.lastUpdatedTurn}일", text);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void Stat(Rect area, string name, float value, float min, float max, GUIStyle text)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 26), $"{name}  {value:0.##}", text);
            var rect = new Rect(area.x, area.y + 30, area.width, 6);
            var old = GUI.color;
            GUI.color = new Color(0.2f, 0.25f, 0.32f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.65f, 0.77f, 0.85f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.InverseLerp(min, max, value), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>원화 미제공 시의 임시 표시다. NPC의 확정 외형 설정으로 취급하지 않는다.</summary>
        private static Texture2D CreateSilhouette()
        {
            var image = new Texture2D(160, 400, TextureFormat.RGBA32, false) { name = "NPC placeholder" };
            var pixels = new Color[160 * 400];
            for (var y = 0; y < 400; y++)
                for (var x = 0; x < 160; x++)
                {
                    var head = Mathf.Pow((x - 80) / 24f, 2) + Mathf.Pow((y - 340) / 34f, 2) < 1;
                    var torso = y > 140 && y < 306 && Mathf.Abs(x - 80) < Mathf.Lerp(38, 29, (y - 140) / 166f);
                    var neck = y >= 302 && y <= 320 && Mathf.Abs(x - 80) < 12;
                    var legs = y > 18 && y <= 140 && (Mathf.Abs(x - 60) < 16 || Mathf.Abs(x - 100) < 16);
                    var arms = y > 143 && y < 290 && Mathf.Abs(x - 80) > 35 && Mathf.Abs(x - 80) < 49;
                    pixels[y * 160 + x] = head || torso || neck || legs || arms ? new Color(0.42f, 0.52f, 0.63f) : Color.clear;
                }
            image.SetPixels(pixels);
            image.Apply();
            return image;
        }
    }
}
