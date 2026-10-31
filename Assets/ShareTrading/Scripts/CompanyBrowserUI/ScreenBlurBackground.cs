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

        // 1) 지금 화면을 그림으로 찍는다.
        Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();

        // 2) 반씩 여러 번 줄인다. 한 번에 확 줄이는 것보다 여러 번 나눠 줄여야 부드럽게 뭉개진다.
        int width = screen.width;
        int height = screen.height;
        RenderTexture source = null;

        for (int i = 0; i < downsampleSteps; i++)
        {
            width = Mathf.Max(1, width / 2);
            height = Mathf.Max(1, height / 2);

            RenderTexture next = RenderTexture.GetTemporary(width, height, 0);
            next.filterMode = FilterMode.Bilinear;   // 픽셀 사이를 섞어서 부드럽게

            if (source == null)
            {
                Graphics.Blit(screen, next);   // Blit = 그림을 다른 크기의 그림에 복사
            }
            else
            {
                Graphics.Blit(source, next);
                RenderTexture.ReleaseTemporary(source);
            }
            source = next;
        }

        // 3) 결과를 보관용 그림에 옮긴다. (임시 그림은 다른 곳에서 재사용될 수 있어서 계속 들고 있으면 안 됨)
        prepareResult(width, height);
        Graphics.Blit(source, blurred);
        RenderTexture.ReleaseTemporary(source);

        // 찍은 원본은 더 이상 필요 없으니 바로 지운다. (안 지우면 열 때마다 메모리가 쌓인다)
        Destroy(screen);

        // 4) 화면에 보여 주고, 색을 곱해서 어둡게 만든다.
        rawImage.texture = blurred;
        rawImage.color = new Color(brightness, brightness, brightness, 1f);
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
            blurred.Release();
            Destroy(blurred);
        }
    }
}
