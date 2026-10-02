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

        public ChoiceConfirmedInfo(string eventId, int choiceIndex, string resultValue)
        {
            this.eventId = eventId;
            this.choiceIndex = choiceIndex;
            this.resultValue = resultValue;
        }
    }
}
