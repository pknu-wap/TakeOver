using System.Collections;
using TakeOver.NPC;
using UnityEngine;

public sealed class DashboardCompanyPanel : MonoBehaviour
{
    [SerializeField] private CompanyItemUI[] rows;
    private Companies companies;
    private GameTurnClock turnClock;
    private CompanyDetailPanel detailPanel;
    private EventListener eventListener;
    private bool refreshPending;

    private void Awake()
    {
        companies = FindFirstObjectByType<Companies>();
        turnClock = FindFirstObjectByType<GameTurnClock>();
        detailPanel = FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
        eventListener = FindFirstObjectByType<EventListener>();
        if (companies == null) Debug.LogWarning("Dashboard company data source was not found.", this);
        if (turnClock == null) Debug.LogWarning("Dashboard turn update signal was not found.", this);
        if (detailPanel == null) Debug.LogWarning("Dashboard trade update signal was not found.", this);
        if (eventListener == null) Debug.LogWarning("Dashboard company effect signal was not found.", this);
    }

    private void OnEnable()
    {
        if (turnClock != null) turnClock.TurnAdvanced += onTurnAdvanced;
        if (detailPanel != null) detailPanel.OnTraded += refresh;
        if (eventListener != null) eventListener.effectsAppliedToCompany += refresh;
        refresh();
    }

    private void OnDisable()
    {
        refreshPending = false;
        if (turnClock != null) turnClock.TurnAdvanced -= onTurnAdvanced;
        if (detailPanel != null) detailPanel.OnTraded -= refresh;
        if (eventListener != null) eventListener.effectsAppliedToCompany -= refresh;
    }

    private IEnumerator Start()
    {
        yield return null;
        refresh();
    }

    private void onTurnAdvanced(int turn) => refreshPending = true;

    private void LateUpdate()
    {
        if (!refreshPending) return;
        refreshPending = false;
        refresh();
    }

    private void refresh()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            bool visible = companies != null && i < companies.CompanyList.Count;
            rows[i].gameObject.SetActive(visible);
            if (!visible) continue;
            Company company = companies.CompanyList[i];
            float value = CompanyUIUtil.getStat(company, CompanyStat.PLAYER_SHARES)
                * CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE);
            rows[i].SetData(company.definition.companyName,
                CompanyUIUtil.getDerived(company, CompanyDerivedStat.STAKE), value, 0f, company.definition.logo);
        }
    }
}
