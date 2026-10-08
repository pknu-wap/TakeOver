namespace TakeOver.Events
{
    public sealed class TodayEventInfo
    {
        public EventDefinition definition { get; }
        public string companyId { get; }
        public string companyName { get; }
        public string speakerName { get; }
        public string speakerRole { get; }
        public string title => resolve(definition.getTitle());
        public string description => resolve(definition.getDescription());
        public string dialogue => resolve(definition.getDialogue());
        public int remainingDays { get; }

        public TodayEventInfo(EventDefinition definition, EventCompanyData company, int remainingDays)
        {
            this.definition = definition;
            this.remainingDays = remainingDays;
            companyId = company?.companyId ?? "";
            companyName = company?.companyName ?? "";
            speakerName = definition.getSpeakerName();
            speakerRole = definition.getSpeakerRole();
            if (definition.getSpeakerSource() == EventSpeakerSource.CompanyCeo)
            {
                speakerName = company?.ceoName ?? "";
                speakerRole = "대표";
            }
            else if (definition.getSpeakerSource() == EventSpeakerSource.CompanyRestructuring)
            {
                speakerName = company?.restructuringName ?? "";
                speakerRole = company?.restructuringRole ?? "";
            }
        }

        public string resolve(string text) => (text ?? "").Replace("{company}", companyName)
            .Replace("{speaker}", speakerName).Replace("{speakerRole}", speakerRole);
    }
}
