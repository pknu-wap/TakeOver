using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TakeOver.NPC;
using UnityEngine;

public sealed class DashboardCompanyPanel : MonoBehaviour
{
    [SerializeField] private CompanyItemUI[] rows;
    private Companies companies;
    private GameTurnClock turnClock;
    private CompanyDetailPanel detailPanel;
    private TakeOver.Events.EventSystemController eventController;
    private bool refreshPending;

    // 지도 오른쪽 기업 목록(CompanyListView, StoryOnly)과 같은 기준: 등락률은 전날 마지막으로 본 주가 대비.
    private readonly Dictionary<Company, float> lastSeenPrice = new Dictionary<Company, float>();
    private Dictionary<Company, float> closePrice = new Dictionary<Company, float>();
    private int recordedTurn = -1;

    private void Awake()
    {
        companies = FindFirstObjectByType<Companies>();
        turnClock = FindFirstObjectByType<GameTurnClock>();
        detailPanel = FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
        eventController = FindFirstObjectByType<TakeOver.Events.EventSystemController>();
        if (companies == null) Debug.LogWarning("Dashboard company data source was not found.", this);
        if (turnClock == null) Debug.LogWarning("Dashboard turn update signal was not found.", this);
        if (detailPanel == null) Debug.LogWarning("Dashboard trade update signal was not found.", this);
    }

    private void OnEnable()
    {
        if (turnClock != null) turnClock.TurnAdvanced += onTurnAdvanced;
        if (turnClock != null) turnClock.TurnRestored += onTurnAdvanced;
        if (eventController != null) eventController.choiceConfirmed += onEventConfirmed;
        if (eventController != null) eventController.todayEventsChanged += onEventsChanged;
        if (detailPanel != null) detailPanel.OnTraded += refresh;
        refresh();
    }

    private void OnDisable()
    {
        refreshPending = false;
        if (turnClock != null) turnClock.TurnAdvanced -= onTurnAdvanced;
        if (turnClock != null) turnClock.TurnRestored -= onTurnAdvanced;
        if (eventController != null) eventController.choiceConfirmed -= onEventConfirmed;
        if (eventController != null) eventController.todayEventsChanged -= onEventsChanged;
        if (detailPanel != null) detailPanel.OnTraded -= refresh;
    }

    private IEnumerator Start()
    {
        yield return null;
        refresh();
    }

    private void onTurnAdvanced(int turn) => refreshPending = true;
    private void onEventConfirmed(TakeOver.Events.ChoiceConfirmedInfo info) => refreshPending = true;
    private void onEventsChanged() => refreshPending = true;

    private void LateUpdate()
    {
        if (!refreshPending) return;
        refreshPending = false;
        refresh();
    }

    // 지도 오른쪽 기업 목록과 같은 로직: 스토리 기업만, 주가 높은 순, 전날 대비 등락률.
    private void refresh()
    {
        List<Company> listed = companies == null ? new List<Company>() : companies.CompanyList
            .Where(c => c != null && c.state != null && c.definition != null && c.definition.isStoryCompany)
            .OrderByDescending(c => CompanyUIUtil.getDerived(c, CompanyDerivedStat.SHARE_PRICE))
            .ToList();

        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        if (turn != recordedTurn)
        {
            closePrice = new Dictionary<Company, float>(lastSeenPrice);
            recordedTurn = turn;
        }

        for (int i = 0; i < listed.Count; i++)
        {
            Company company = listed[i];
            float price = CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE);
            float changePercent = closePrice.TryGetValue(company, out float close) && close > 0f
                ? (price - close) / close * 100f
                : 0f;
            lastSeenPrice[company] = price;

            if (i >= rows.Length) continue;
            rows[i].SetData(company.definition.companyName,
                CompanyUIUtil.getDerived(company, CompanyDerivedStat.STAKE), price, changePercent, company.definition.logo);
        }

        for (int i = 0; i < rows.Length; i++)
            rows[i].gameObject.SetActive(i < listed.Count);
    }
}
