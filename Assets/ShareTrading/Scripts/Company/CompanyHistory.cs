using UnityEngine;
using System.Collections.Generic;

public class CompanyHistory
{
    internal const int MAX_HISTORY = 5;
    internal const int MAX_PRICE_HISTORY = 14;

    private CompanyState state;
    private List<CompanyHistoryEntry> history
        = new List<CompanyHistoryEntry>();

    public IReadOnlyList<CompanyHistoryEntry> History => history;
    internal List<CompanyHistoryEntry> Entries => history;

    private readonly List<float> priceHistory = new List<float>();
    public IReadOnlyList<float> PriceHistory => priceHistory;
    internal List<float> PriceEntries => priceHistory;

    public CompanyHistory(CompanyState state)
    {
        this.state = state;
    }

    public void recordHistory(int turn)
    {
        CompanyHistoryEntry entry = new CompanyHistoryEntry(
            turn,
            state.getBaseStat(CompanyStat.REVENUE),
            state.getDerivedStat(CompanyDerivedStat.NET_PROFIT),
            state.getDerivedStat(CompanyDerivedStat.NET_MARGIN)
        );

        history.Add(entry);

        if (history.Count > MAX_HISTORY)
        {
            history.RemoveAt(0);
        }

        priceHistory.Add(state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE));
        if (priceHistory.Count > MAX_PRICE_HISTORY)
        {
            priceHistory.RemoveAt(0);
        }
    }

    public void updateHistory()
    {
        
    }

    public int getMaxHistory() { return MAX_HISTORY; }
}
