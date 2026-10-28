using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed class NegotiationStartedInfo
    {
        public string eventId { get; }
        public int apCost { get; }

        public NegotiationStartedInfo(string eventId, int apCost)
        {
            this.eventId = eventId;
            this.apCost = apCost;
        }
    }

    public sealed class ChoiceConfirmedInfo
    {
        public string eventId { get; }
        public int choiceIndex { get; }
        public string resultValue { get; }
        public string occurrenceId { get; }
        public int currentDay { get; }
        public NpcEventPayload npcResult { get; private set; }
        public string companyId { get; }
        internal void clearNpcResult() => npcResult = null;
        public float cashDelta { get; }
        public string stockSurgeCompanyId { get; }
        public float stockSurgeMinRatio { get; }
        public float stockSurgeMaxRatio { get; }
        public float buybackPremiumRatio { get; }

        public ChoiceConfirmedInfo(string eventId, int choiceIndex, string resultValue,
            int currentDay, NpcEventPayload npcResult, float cashDelta, string companyId = "",
            string stockSurgeCompanyId = "", float stockSurgeMinRatio = 0, float stockSurgeMaxRatio = 0, float buybackPremiumRatio = 0)
        {
            this.eventId = eventId;
            this.choiceIndex = choiceIndex;
            this.resultValue = resultValue;
            this.currentDay = currentDay;
            this.companyId = companyId;
            occurrenceId = $"day{currentDay}:{eventId}";
            this.cashDelta = cashDelta;
            this.stockSurgeCompanyId = stockSurgeCompanyId;
            this.stockSurgeMinRatio = stockSurgeMinRatio;
            this.stockSurgeMaxRatio = stockSurgeMaxRatio;
            this.buybackPremiumRatio = buybackPremiumRatio;
            if (npcResult != null && npcResult.targetNpcIds != null && npcResult.targetNpcIds.Count > 0)
            {
                this.npcResult = JsonUtility.FromJson<NpcEventPayload>(JsonUtility.ToJson(npcResult));
                this.npcResult.eventId = occurrenceId;
                this.npcResult.companyId = companyId;
            }
        }
    }
}
