using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class FloatingMessage : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text label;
    private Coroutine routine;

    public void Show(string text)
    {
        transform.SetAsLastSibling();
        if (routine != null) StopCoroutine(routine);
        label.text = text;
        routine = StartCoroutine(play());
    }

    private IEnumerator play()
    {
        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.unscaledDeltaTime / 0.15f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.unscaledDeltaTime / 0.3f);
            yield return null;
        }
        routine = null;
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        canvasGroup.alpha = 0f;
    }

    private void OnDisable() => Hide();
}
