using System;
using TakeOver.Events;
using UnityEngine;

public class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private Marketdata marketData;
    [SerializeField] private TradingEventEffects eventEffects;

    public event Action effectsAppliedToCompany;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
    }

    private void OnEnable()
    {
            eventSystem.choiceConfirmed += receiveEventNotification;
    }

    private void OnDisable()
    {
            eventSystem.choiceConfirmed -= receiveEventNotification;
    }

    private void receiveEventNotification(ChoiceConfirmedInfo info)
    {
            eventEffects.Apply(info);
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
