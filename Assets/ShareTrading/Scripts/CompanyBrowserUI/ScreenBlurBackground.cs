using UnityEngine;
using UnityEngine.UI;

// 팝업 뒤 배경을 "어둡게 + 흐리게" 보여 준다.
// 방법: 팝업이 뜨기 직전 화면을 한 번 찍고 → 아주 작게 줄였다가 → 다시 화면 크기로 늘려서 보여 준다.
//       작게 줄이면 세밀한 부분이 뭉개지고, 늘릴 때 픽셀 사이가 부드럽게 채워져서 흐려 보인다.
// 팝업이 떠 있는 동안 뒤 화면은 멈춰 있어도 되니까, 매 프레임 계산하지 않고 열 때 한 번만 찍는다.
[RequireComponent(typeof(RawImage))]
public class ScreenBlurBackground : MonoBehaviour
{
    [Tooltip("몇 번 반으로 줄일지. 클수록 더 흐려진다. (3이면 1/8 크기)")]
    [SerializeField, Range(1, 6)] private int downsampleSteps = 3;

    [Tooltip("배경을 얼마나 어둡게 할지. 1이면 원래 밝기, 0이면 완전 검정")]
    [SerializeField, Range(0f, 1f)] private float brightness = 0.45f;

    private RawImage rawImage;
    private RenderTexture blurred;   // 흐리게 만든 결과를 담아 두는 그림

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    // 반드시 "WaitForEndOfFrame" 뒤에 불러야 한다. (화면 그리기가 끝난 뒤라야 찍을 수 있음)
    public void captureAndBlur()
    {
        if (rawImage == null)
        {
            rawImage = GetComponent<RawImage>();
        }

        RenderTexture previousActive = RenderTexture.active;
        Texture2D screen = null;
        RenderTexture source = null;
        try
        {
            screen = ScreenCapture.CaptureScreenshotAsTexture();
            int width = screen.width;
            int height = screen.height;
            for (int i = 0; i < downsampleSteps; i++)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                RenderTexture next = RenderTexture.GetTemporary(width, height, 0);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source != null ? (Texture)source : screen, next);
                if (source != null) RenderTexture.ReleaseTemporary(source);
                source = next;
            }
            prepareResult(width, height);
            Graphics.Blit(source, blurred);
            rawImage.texture = blurred;
            rawImage.color = new Color(brightness, brightness, brightness, 1f);
        }
        finally
        {
            // 캡처·Blit가 바꾼 렌더 대상을 돌려놓고 임시 자원을 해제한다.
            RenderTexture.active = previousActive;
            if (source != null) RenderTexture.ReleaseTemporary(source);
            if (screen != null) Destroy(screen);
        }
    }

    // 결과 그림 크기가 바뀌었을 때만 새로 만든다.
    private void prepareResult(int width, int height)
    {
        if (blurred != null && blurred.width == width && blurred.height == height)
        {
            return;
        }

        if (blurred != null)
        {
            if (RenderTexture.active == blurred) RenderTexture.active = null;
            blurred.Release();
            Destroy(blurred);
        }

        blurred = new RenderTexture(width, height, 0);
        blurred.filterMode = FilterMode.Bilinear;
    }

    void OnDestroy()
    {
        if (blurred != null)
        {
            if (RenderTexture.active == blurred) RenderTexture.active = null;
            blurred.Release();
            Destroy(blurred);
        }
    }
}
