using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public sealed class ScreenSettings : MonoBehaviour
{
   
    private static readonly Vector2Int[] resolutionOptions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720),
    };

    private const int windowedIndex = 0;
    private const int fullScreenIndex = 1;

    [Tooltip("해상도 선택")]
    [SerializeField] private Dropdown resolutionDropdown;

    [Tooltip("창모드 / 전체화면 선택")]
    [SerializeField] private Dropdown screenModeDropdown;

    private void Awake()
    {
        setupResolutionDropdown();
        setupScreenModeDropdown();
    }

    private void setupResolutionDropdown()
    {
        
        resolutionDropdown.ClearOptions();

        List<string> labels = new List<string>(resolutionOptions.Length);
        foreach (Vector2Int size in resolutionOptions)
        {
            labels.Add($"{size.x} x {size.y}");
        }
        resolutionDropdown.AddOptions(labels);

       
        resolutionDropdown.SetValueWithoutNotify(findCurrentResolutionIndex());
        resolutionDropdown.onValueChanged.AddListener(onOptionChanged);
    }

    private void setupScreenModeDropdown()
    {
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(new List<string> { "창모드", "전체화면" });

        int currentIndex = Screen.fullScreenMode == FullScreenMode.Windowed ? windowedIndex : fullScreenIndex;
        screenModeDropdown.SetValueWithoutNotify(currentIndex);
        screenModeDropdown.onValueChanged.AddListener(onOptionChanged);
    }

    
    private void onOptionChanged(int _)
    {
        Vector2Int size = resolutionOptions[resolutionDropdown.value];
        FullScreenMode mode = screenModeDropdown.value == windowedIndex
            ? FullScreenMode.Windowed
            : FullScreenMode.FullScreenWindow;

        Screen.SetResolution(size.x, size.y, mode);
    }

    
    private int findCurrentResolutionIndex()
    {
        for (int i = 0; i < resolutionOptions.Length; i++)
        {
            if (resolutionOptions[i].x == Screen.width && resolutionOptions[i].y == Screen.height)
            {
                return i;
            }
        }

        return 0;
    }
}
