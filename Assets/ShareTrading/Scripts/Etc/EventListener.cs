using System;
using System.Collections.Generic;
using TakeOver.Events;
using UnityEngine;

public sealed class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private Player player;
    [SerializeField] private Marketdata marketData;
    [SerializeField] private EventStockmanipulationAccept eventStockmanipulationAccept;
    [SerializeField] private EventGreenmailAccept eventGreenmailAccept;

    private readonly Dictionary<string, Action<string>> eventEffects = new Dictionary<string, Action<string>>();

    public event Action effectsAppliedToCompany;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
        player = FindFirstObjectByType<Player>();
        eventEffects.Add("greenmail_01", companyID => eventGreenmailAccept.applyEventEffect(companyID, 0));
        eventEffects.Add("stock_manipulation_01", companyID => eventStockmanipulationAccept.applyEventEffect(companyID, 1));
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
        Action<string> effect;
        if (info.resultValue == "accept") {
            if (eventEffects.TryGetValue(info.eventId, out effect))
            {
                effect(info.companyId);
                return;
            }

            applyEventAcceptEffect(info.companyId, info.cashDelta);
        }
    }

    public void applyEventAcceptEffect(string companyID, float cashDelta)
    {
        player.state.addCash(cashDelta);
        Debug.Log($"이벤트 결과 반영: companyID={companyID}, cashDelta={cashDelta}, playerCash={player.state.cash}", this);
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
