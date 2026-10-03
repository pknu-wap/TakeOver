using UnityEngine;
using System.Collections.Generic;

public class CompanyHistory
{
    private const int MAX_HISTORY = 5;

    private CompanyState state;
    private List<CompanyHistoryEntry> history
        = new List<CompanyHistoryEntry>();

    public IReadOnlyList<CompanyHistoryEntry> History => history;

    public CompanyHistory(CompanyState state)
    {
        this.state = state;
    }

    public void recordHistory(int turn)
    {
        CompanyHistoryEntry entry = new CompanyHistoryEntry(
            turn,
            state.getStat(CompanyStat.REVENUE),
            state.getDerivedStat(CompanyDerivedStat.NET_PROFIT),
            state.getDerivedStat(CompanyDerivedStat.NET_MARGIN)
        );

        history.Add(entry);

        if (history.Count > MAX_HISTORY)
        {
            history.RemoveAt(0);
        }
    }

    public void updateHistory()
    {
        
    }

    public int getMaxHistory() { return MAX_HISTORY; }
}
