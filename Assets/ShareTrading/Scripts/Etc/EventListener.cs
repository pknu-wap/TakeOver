using TakeOver.Events;
using UnityEngine;

public class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private Player player;
    [SerializeField] private TradingEventEffects eventEffects;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
        if (player == null) player = FindFirstObjectByType<Player>();
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
        // 후원금·기부금처럼 수락이 아닌 선택도 현금 증감은 반영한다.
        if (info.cashDelta != 0) applyCashDelta(info.companyId, info.cashDelta);
        if (info.resultValue == "accept")
            eventEffects.Apply(info);
    }

    public void applyEventAcceptEffect(string companyID)
    {
        eventEffects.applyEventAcceptEffect(companyID);
    }

    private void applyCashDelta(string companyID, float cashDelta)
    {
        if (player == null)
        {
            Debug.LogWarning("Player를 찾을 수 없어 이벤트 현금 증감을 반영하지 못했습니다.", this);
            return;
        }
        player.state.addCash(cashDelta);
        Debug.Log($"이벤트 결과 반영: companyID={companyID}, cashDelta={cashDelta}, playerCash={player.state.cash}", this);
    }

}
