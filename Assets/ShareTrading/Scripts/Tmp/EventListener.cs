using System;
using TakeOver.Events;
using UnityEngine;

public sealed class EventListener : MonoBehaviour
{
    private EventSystemController eventSystem;
    [SerializeField] private TmpMarketData marketData;

    // 임시
    [SerializeField] private string tmpTargetCompanyId = "tmp-company-a";

    // 임시
    [SerializeField] private MarketEventImpact tmpAcceptMarketImpact =
        new MarketEventImpact(0.2f, 0.1f, 3);

    public event Action effectsAppliedToCompany;

    private void Awake()
    {
        eventSystem = FindFirstObjectByType<EventSystemController>();
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
        // 현재 통지에는 기업 ID가 없어 임시 대상 ID를 사용
        receiveEventResult(tmpTargetCompanyId, info.resultValue);
    }

    public void receiveEventResult(string companyID, string resultValue)
    {
        switch (resultValue)
        {
            case "dummy_accept":
                if (applyEventEffect(companyID, tmpAcceptMarketImpact))
                    Debug.Log($"{companyID}-dummy_accept 실행", this);
                break;

            case "dummy_reject":
                // applyEventEffect(companyID, tmpAcceptMarketImpact);
                Debug.Log($"{companyID}-dummy_reject 실행", this);
                break;
        }
    }

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
