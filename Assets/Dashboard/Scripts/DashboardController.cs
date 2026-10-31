using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using Unity.XR.GoogleVr;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;

public class DashboardController : MonoBehaviour
{
    [Header("Top Bar - Currency & Asset")]
    [SerializeField] private TextMeshProUGUI cashText;
    [SerializeField] private TextMeshProUGUI totalAssetText;
    [SerializeField] private int currentCash = 0;
    [SerializeField] private int totalAsset = 0;

    [Header("Top Bar - AP Cost")]
    [SerializeField] private TextMeshProUGUI apText;
    [SerializeField] private Slider apSlider;
    [SerializeField] private int currentAp = 50;
    [SerializeField] private int maxAp = 50;

    private void Start()
    {
        UpdateTopBarUI();
    }

    private void UpdateTopBarUI()
    {
        if (cashText != null)
            cashText.text = $"{currentCash}";

        if (totalAssetText != null)
            totalAssetText.text = $"{totalAsset}";

        if (apText != null)
            apText.text = $"{currentAp}/{maxAp}";

        if (apSlider != null)
        {
            apSlider.maxValue = maxAp;
            apSlider.value = currentAp;
        }
    }
}