using System;
using TakeOver.Events;
using UnityEngine;

public sealed class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private Player player;
    [SerializeField] private TmpMarketData marketData;

    public event Action effectsAppliedToCompany;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
        player = FindFirstObjectByType<Player>();
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
        receiveEventResult(info.companyId, info.cashDelta);
    }

    public void receiveEventResult(string companyID, float cashDelta)
    {
        player.state.addCash(cashDelta);
        Debug.Log($"이벤트 결과 반영: companyID={companyID}, cashDelta={cashDelta}, playerCash={player.state.cash}", this);
    }

    // 시장 효과 전달 방식이 확정될 때까지 미호출
    private bool applyEventEffect(string companyID, MarketEventImpact marketImpact)
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
