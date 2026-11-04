using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private ActionPointService actionPointService;

    private void Awake()
    {
        actionPointService = FindFirstObjectByType<ActionPointService>();
    }

    private void OnEnable()
    {
        if (actionPointService != null)
        {
            actionPointService.Changed += UpdateTopBarUI;
        }
        UpdateTopBarUI();
    }

    private void OnDisable()
    {
        if (actionPointService != null)
        {
            actionPointService.Changed -= UpdateTopBarUI;
        }
    }

    private void Start()
    {
        UpdateTopBarUI();
    }

    private void OnNegotiationStarted(NegotiationStartedInfo info)
    {
        ConsumeAP(info.apCost);
    }

    public void ConsumeAP(int amount)
    {
        currentAp = Mathf.Max(0, currentAp - amount);
        UpdateTopBarUI();
    }

    public bool CanConsumeAP(int amount) => amount >= 0 && currentAp >= amount;

    public void ResetAP()
    {
        currentAp = maxAp;
        UpdateTopBarUI();
    }

    private void UpdateTopBarUI()
    {
        if (cashText != null)
            cashText.text = $"{currentCash}";

        if (totalAssetText != null)
            totalAssetText.text = $"{totalAsset}";

        if (actionPointService == null) return;

        if (apText != null)
            apText.text = $"{actionPointService.Current}/{actionPointService.Max}";

        if (apSlider != null)
        {
            apSlider.maxValue = actionPointService.Max;
            apSlider.value = actionPointService.Current;
        }
    }
}
