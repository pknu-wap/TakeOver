using UnityEngine;

namespace TakeOver.Events
{
    public sealed class EventDebugSubscriber : MonoBehaviour
    {
        [SerializeField] private EventSystemController eventSystem;

        private void OnEnable()
        {
            eventSystem.negotiationStarted += onNegotiationStarted;
            eventSystem.choiceConfirmed += onChoiceConfirmed;
        }

        private void OnDisable()
        {
            eventSystem.negotiationStarted -= onNegotiationStarted;
            eventSystem.choiceConfirmed -= onChoiceConfirmed;
        }

        private void onNegotiationStarted(NegotiationStartedInfo info)
        {
            Debug.Log($"[협상 시작] eventId={info.eventId}, apCost={info.apCost}", this);
        }

        private void onChoiceConfirmed(ChoiceConfirmedInfo info)
        {
            int targetCount = info.npcResult != null ? info.npcResult.targetNpcIds.Count : 0;
            Debug.Log($"[선택 확정] eventId={info.eventId}, choiceIndex={info.choiceIndex}, resultValue={info.resultValue}, occurrenceId={info.occurrenceId}, companyId={info.companyId}, npcTargetCount={targetCount}, cashDelta={info.cashDelta}", this);
        }
    }
}
