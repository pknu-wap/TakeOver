using UnityEngine;

[System.Serializable]
public class CompanyHistoryEntry
{
    public int turn;
    public float revenue;
    public float netProfit;
    public float netMargin;

    public CompanyHistoryEntry(
        int turn,
        float revenue,
        float netProfit,
        float netMargin)
    {
        this.turn = turn;
        this.revenue = revenue;
        this.netProfit = netProfit;
        this.netMargin = netMargin;
    }
}
