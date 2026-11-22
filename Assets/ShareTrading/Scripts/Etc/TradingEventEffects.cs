using TakeOver.Events;
using TakeOver.SaveSystem;
using UnityEngine;

public class TradingEventEffects : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private EventGreenmailAccept greenmailAccept;
    [SerializeField] private EventStockmanipulationAccept stockManipulationAccept;

    internal EventGreenmailAccept GreenmailAccept => greenmailAccept;
    internal EventStockmanipulationAccept StockManipulationAccept => stockManipulationAccept;

    public void Apply(ChoiceConfirmedInfo info)
    {
        switch (info.eventId)
        {
            case "greenmail_01":
                if(info.resultValue=="accept")greenmailAccept.applyEventEffect(info.companyId, 0, info.buybackPremiumRatio);
                break;

            case "stock_manipulation_01":
                float roll = (float)(FixedDaySeed.StableOrder(info.currentDay, "surge:" + info.occurrenceId + ":" + info.stockSurgeCompanyId) / 4294967296d);
                float value = Mathf.Lerp(info.stockSurgeMinRatio, info.stockSurgeMaxRatio, roll);
                stockManipulationAccept.applyEventEffect(info.stockSurgeCompanyId, 1, value);
                break;

            default:
                applyEventAcceptEffect(info.companyId);
                break;
        }
    }

    public void applyEventAcceptEffect(string companyID)
    {
        // player.state.addCash(cashDelta);
        Debug.Log($"이벤트 effects가 없음: companyID={companyID}", this);
    }
}
