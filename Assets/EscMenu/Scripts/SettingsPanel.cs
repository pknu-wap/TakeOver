using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsPanel : MonoBehaviour
{
    [Serializable]
    private sealed class Tab
    {
        [Tooltip("탭 이름 글자 (그래픽 / 사운드 / 게임)")]
        public Text label;

        [Tooltip("선택됐을 때만 보이는 탭 아래 빛나는 막대")]
        public GameObject highlight;

        [Tooltip("이 탭을 눌렀을 때 보이는 내용 묶음")]
        public GameObject page;
    }

    [Header("탭 (순서: 그래픽, 사운드, 게임)")]
    [SerializeField] private Tab[] tabs;
    [SerializeField] private Color selectedTabColor = Color.white;
    [SerializeField] private Color normalTabColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("실제로 적용되는 설정")]
    [SerializeField] private ScreenSettings screenSettings;

    [Header("보이기만 하는 설정 (리셋 때 처음 값으로 돌아감)")]
    [SerializeField] private Slider[] displayOnlySliders;
    [SerializeField] private Dropdown[] displayOnlyDropdowns;
    [SerializeField] private SelectButtonGroup[] displayOnlyButtonGroups;

    private float[] defaultSliderValues;
    private int[] defaultDropdownValues;

    private void Awake()
    {
        rememberDefaults();
    }

    private void OnEnable()
    {
        selectTab(0);
    }

    public void selectTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isSelected = i == index;
            tabs[i].page.SetActive(isSelected);
            tabs[i].highlight.SetActive(isSelected);
            tabs[i].label.color = isSelected ? selectedTabColor : normalTabColor;
        }
    }

    public void applySettings()
    {
        screenSettings.applyToScreen();
    }

    public void resetSettings()
    {
        screenSettings.resetToDefault();

        for (int i = 0; i < displayOnlySliders.Length; i++)
        {
            displayOnlySliders[i].SetValueWithoutNotify(defaultSliderValues[i]);
        }

        for (int i = 0; i < displayOnlyDropdowns.Length; i++)
        {
            displayOnlyDropdowns[i].SetValueWithoutNotify(defaultDropdownValues[i]);
        }

        foreach (SelectButtonGroup group in displayOnlyButtonGroups)
        {
            group.resetToDefault();
        }
    }

    private void rememberDefaults()
    {
        defaultSliderValues = new float[displayOnlySliders.Length];
        for (int i = 0; i < displayOnlySliders.Length; i++)
        {
            defaultSliderValues[i] = displayOnlySliders[i].value;
        }

        defaultDropdownValues = new int[displayOnlyDropdowns.Length];
        for (int i = 0; i < displayOnlyDropdowns.Length; i++)
        {
            defaultDropdownValues[i] = displayOnlyDropdowns[i].value;
        }
    }
}
