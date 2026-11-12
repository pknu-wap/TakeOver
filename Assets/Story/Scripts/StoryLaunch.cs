using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeOver.Story
{
    /// <summary>명시적 새 게임 요청만 소비한다. Game 씬 직접 실행·저장 복원은 도입부를 시작하지 않는다.</summary>
    public static class StoryLaunch
    {
        private static bool requested;
        private static bool skipRequested;
        private static GameObject curtain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            requested = false; skipRequested = false; curtain = null;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        public static void RequestNewGame(bool skipIntro = false)
        {
            requested = true;
            skipRequested = skipIntro;
            if (curtain != null) return;
            curtain = new GameObject("New game blackout", typeof(Canvas));
            Object.DontDestroyOnLoad(curtain);
            var canvas = curtain.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(curtain.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;
        }

        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (!requested || scene.name != "Game") return;
            requested = false;
            var prefab = Resources.Load<GameObject>("Story/Intro");
            if (prefab == null) Debug.LogError("새 게임 도입부 프리팹이 없습니다: Resources/Story/Intro");
            else Object.Instantiate(prefab).GetComponent<StoryDirector>().SkipEntireIntroOnStart = skipRequested;
            skipRequested = false;
            if (curtain != null) Object.Destroy(curtain);
        }
    }
}
