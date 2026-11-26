namespace TakeOver.Events
{
    public sealed class TodayEventInfo
    {
        public EventDefinition definition { get; }
        public string companyId { get; }
        public string companyName { get; }
        public string speakerNpcId { get; }
        public string speakerName { get; }
        public string speakerRole { get; }
        public string speakerLabel { get; }
        public string cardSpeakerLabel { get; }
        public UnityEngine.Sprite cardImage => definition.getCardImageFor(speakerNpcId);
        public UnityEngine.Sprite popupImage => definition.getImageFor(speakerNpcId);
        public string title => resolve(definition.Variant(companyId)?.title ?? definition.getTitle());
        public string description => resolve(definition.Variant(companyId)?.description ?? definition.getDescription());
        public string dialogue => resolve(definition.getDialogue());
        public int remainingDays { get; }

        public TodayEventInfo(EventDefinition definition, EventCompanyData company, int remainingDays, TakeOver.NPC.NpcStateRegistry registry = null)
        {
            this.definition = definition;
            this.remainingDays = remainingDays;
            companyId = company?.companyId ?? "";
            companyName = company?.companyName ?? "";
            speakerNpcId = definition.getSpeakerNpcId();
            speakerName = definition.getSpeakerName();
            speakerRole = definition.getSpeakerRole();
            if (definition.getSpeakerSource() == EventSpeakerSource.CompanyCeo)
            {
                speakerNpcId = company?.ceoNpcId ?? "";
                speakerName = company?.ceoName ?? "";
                speakerRole = string.IsNullOrWhiteSpace(company?.ceoRole) ? "대표" : company.ceoRole;
            }
            else if (definition.getSpeakerSource() == EventSpeakerSource.CompanyRestructuring)
            {
                speakerNpcId = company?.restructuringNpcId ?? "";
                speakerName = company?.restructuringName ?? "";
                speakerRole = company?.restructuringRole ?? "";
            }
            if (registry != null && registry.TryGetProfile(speakerNpcId, out var seed) && seed.profile != null)
            {
                speakerName = seed.profile.displayName;
                if (string.IsNullOrWhiteSpace(speakerRole)) speakerRole = seed.profile.DialogueRole;
                speakerLabel = seed.GetSpeakerLabel(speakerRole);
                cardSpeakerLabel = seed.GetCardLabel(speakerRole);
            }
            else
            {
                string nameAndRole = $"{speakerRole} {speakerName}".Trim();
                speakerLabel = definition.getSpeakerSource() == EventSpeakerSource.Fixed || string.IsNullOrWhiteSpace(companyName)
                    ? nameAndRole : string.IsNullOrWhiteSpace(nameAndRole) ? companyName : $"{companyName} · {nameAndRole}";
                cardSpeakerLabel = definition.getSpeakerSource() == EventSpeakerSource.Fixed
                    ? $"{speakerRole} {speakerName}".Trim() : $"{companyName} {speakerName}".Trim();
            }
        }

        public string resolve(string text) => (text ?? "").Replace("{company}", companyName)
            .Replace("{speaker}", speakerName).Replace("{speakerRole}", speakerRole);
    }
}
