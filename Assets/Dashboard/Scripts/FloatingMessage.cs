using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class FloatingMessage : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text label;
    private const float SidePadding = 32f;
    private const int SortingOrder = 180;
    private Coroutine routine;
    private float minWidth = -1f;
    private GameObject overlayRoot;
    private bool isTopBar;
    private static FloatingMessage topBar;

    private void Awake()
    {
        isTopBar = GetComponentInParent<TopBarNavigationController>(true) != null;
        if (isTopBar) topBar = this;
    }

    public void Show(string text)
    {
        // 상단바가 아닌 인스턴스(이벤트 상세 알약 등)는 상단바 글자로 넘기고 자기는 숨겨 둔다.
        if (!isTopBar && topBar != null && topBar.isActiveAndEnabled)
        {
            Hide();
            topBar.Show(text);
            return;
        }
        raiseAboveScreens();
        transform.SetAsLastSibling();
        if (routine != null) StopCoroutine(routine);
        label.text = text;
        fitWidth();
        routine = StartCoroutine(play());
    }

    // 상단바 인스턴스만 별도 루트 캔버스로 옮겨 운영·일지 같은 다른 화면 캔버스 위에 그린다.
    private void raiseAboveScreens()
    {
        if (overlayRoot != null || !isTopBar) return;
        var source = GetComponentInParent<Canvas>(true).rootCanvas;
        overlayRoot = new GameObject("FloatingMessage overlay", typeof(Canvas), typeof(CanvasScaler));
        var canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var sourceScaler = source.GetComponent<CanvasScaler>();
        if (sourceScaler != null) copyScaler(sourceScaler, overlayRoot.GetComponent<CanvasScaler>());
        transform.SetParent(overlayRoot.transform, true);
    }

    private static void copyScaler(CanvasScaler from, CanvasScaler to)
    {
        to.uiScaleMode = from.uiScaleMode;
        to.referencePixelsPerUnit = from.referencePixelsPerUnit;
        to.scaleFactor = from.scaleFactor;
        to.referenceResolution = from.referenceResolution;
        to.screenMatchMode = from.screenMatchMode;
        to.matchWidthOrHeight = from.matchWidthOrHeight;
        to.physicalUnit = from.physicalUnit;
        to.fallbackScreenDPI = from.fallbackScreenDPI;
        to.defaultSpriteDPI = from.defaultSpriteDPI;
        to.dynamicPixelsPerUnit = from.dynamicPixelsPerUnit;
    }

    private void fitWidth()
    {
        var rect = (RectTransform)transform;
        if (minWidth < 0f) minWidth = rect.rect.width;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(minWidth, label.preferredWidth + SidePadding * 2f));
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

    private void OnDestroy()
    {
        if (topBar == this) topBar = null;
        if (overlayRoot != null) Destroy(overlayRoot);
    }
}
