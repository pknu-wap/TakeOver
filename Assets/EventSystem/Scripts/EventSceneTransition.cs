using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed class EventSceneTransition : MonoBehaviour
    {
        private static EventSceneTransition active;
        public static bool IsBusy => active != null;
        private Image curtain;
        public static void Cancel()
        {
            if (active == null) return;
            var previous = active;
            active = null;
            previous.StopAllCoroutines();
            Destroy(previous.gameObject);
        }
        public static bool Play(Action change, bool fromBlack = false, float seconds = .35f)
        {
            if (IsBusy) return false;
            var root = new GameObject("Event scene fade", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32765;
            active = root.AddComponent<EventSceneTransition>();
            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(root.transform, false);
            active.curtain = black.GetComponent<Image>();
            var rect = active.curtain.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            active.curtain.color = new Color(0, 0, 0, fromBlack ? 1 : 0);
            active.StartCoroutine(active.Sequence(change, fromBlack, seconds));
            return true;
        }
        private IEnumerator Sequence(Action change, bool fromBlack, float seconds)
        {
            if (!fromBlack) yield return Fade(0, 1, seconds);
            try { change(); } catch { Destroy(gameObject); throw; }
            yield return Fade(1, 0, seconds);
            Destroy(gameObject);
        }
        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / seconds)));
                yield return null;
            }
            curtain.color = new Color(0, 0, 0, to);
        }
        private void OnDestroy() { if (active == this) active = null; }
    }
}
