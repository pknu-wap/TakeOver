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
    private Player player;
    private Companies companies;
    private double lastCash = double.NaN;
    private double lastTotalAsset = double.NaN;

    [Header("Top Bar - AP Cost")]
    [SerializeField] private TextMeshProUGUI apText;
    [SerializeField] private Slider apSlider;
    private ActionPointService actionPointService;

    private void Awake()
    {
        actionPointService = FindFirstObjectByType<ActionPointService>();
        player = FindFirstObjectByType<Player>();
        companies = FindFirstObjectByType<Companies>();
        if (player == null || companies == null)
            Debug.LogWarning("Dashboard could not find Player or Companies; financial values will display zero.", this);
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

    private void UpdateTopBarUI()
    {
        updateMoney();

        if (actionPointService == null) return;

        if (apText != null)
            apText.text = $"{actionPointService.Current}/{actionPointService.Max}";

        if (apSlider != null)
        {
            apSlider.maxValue = actionPointService.Max;
            apSlider.value = actionPointService.Current;
        }
    }

    private void Update() => updateMoney();

    private void updateMoney()
    {
        double cash = player != null && companies != null && player.state != null ? player.state.cash : 0d;
        double assets = cash;
        if (player != null && companies != null)
            foreach (Company company in companies.CompanyList)
                assets += (double)CompanyUIUtil.getStat(company, CompanyStat.PLAYER_SHARES)
                    * CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE);
        if (lastCash != cash)
        {
            lastCash = cash;
            if (cashText != null) cashText.text = MoneyText.Format(cash);
        }
        if (lastTotalAsset != assets)
        {
            lastTotalAsset = assets;
            if (totalAssetText != null) totalAssetText.text = MoneyText.Format(assets);
        }
    }
}
