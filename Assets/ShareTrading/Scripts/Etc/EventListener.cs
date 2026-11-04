using System;
using TakeOver.Events;
using UnityEngine;

public sealed class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private Marketdata marketData;
    [SerializeField] private EventStockmanipulationAccept eventStockmanipulationAccept;
    [SerializeField] private EventGreenmailAccept eventGreenmailAccept;

    private readonly Dictionary<string, Action<string>> eventEffects = new Dictionary<string, Action<string>>();
    private ChoiceConfirmedInfo activeChoiceInfo;

    public event Action effectsAppliedToCompany;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
        player = FindFirstObjectByType<Player>();
        eventEffects.Add("greenmail_01", companyID => eventGreenmailAccept.applyEventEffect(companyID, 0, activeChoiceInfo.buybackPremiumRatio));
        eventEffects.Add("stock_manipulation_01", companyID => eventStockmanipulationAccept.applyEventEffect(companyID, 1, UnityEngine.Random.Range(activeChoiceInfo.stockSurgeMinRatio, activeChoiceInfo.stockSurgeMaxRatio)));
    }

    private void OnEnable()
    {
        if (eventSystem != null)
            eventSystem.choiceConfirmed += receiveEventNotification;
    }

    private void OnDisable()
    {
        if (eventSystem != null)
            eventSystem.choiceConfirmed -= receiveEventNotification;
    }

    private void receiveEventNotification(ChoiceConfirmedInfo info)
    {
        if (info.resultValue != "accept" && info.cashDelta == 0) return;
        if (info.cashDelta != 0) applyEventAcceptEffect(info.companyId, info.cashDelta);
        if (info.resultValue == "accept" && eventEffects.TryGetValue(info.eventId, out Action<string> effect))
        {
            activeChoiceInfo = info;
            string companyID = info.eventId == "stock_manipulation_01" && !string.IsNullOrEmpty(info.stockSurgeCompanyId)
                ? info.stockSurgeCompanyId : info.companyId;
            try { effect(companyID); }
            finally { activeChoiceInfo = null; }
        }
    }

    public void applyEventAcceptEffect(string companyID)
    {
        eventEffects.applyEventAcceptEffect(companyID);
    }

    // 시장 효과 전달 방식이 확정될 때까지 미호출
    private bool applyEventDelayeffect(string companyID, MarketEventImpact marketImpact)
    {
        if (!marketData.addEventImpact(companyID, marketImpact))
        {
            Debug.LogWarning($"{companyID} 이벤트 효과 적용 실패", this);
            return false;
        }

        effectsAppliedToCompany?.Invoke();
        return true;
    }
}
