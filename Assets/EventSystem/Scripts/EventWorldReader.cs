using System.Collections.Generic;
using TakeOver.NPC;

namespace TakeOver.Events
{
    public sealed class EventWorldReader
    {
        private readonly EventTempWorldData worldData;
        private readonly NpcStateRegistry registry;

        public EventWorldReader(EventTempWorldData worldData, NpcStateRegistry registry)
        {
            this.worldData = worldData;
            this.registry = registry;
        }

        public IReadOnlyList<EventCompanyData> getCompanies() => worldData.getCompanies();
        public EventCompanyData getCompany(string companyId)
        {
            foreach (EventCompanyData company in getCompanies())
                if (company.companyId == companyId) return company;
            return null;
        }
        public float getStake(string companyId) => getCompany(companyId)?.stake ?? 0;
        public float getConsumerReaction(string companyId) => getCompany(companyId)?.consumerReaction ?? 0;
        public NpcRelationState getNpcRelation(string npcId) =>
            registry != null && registry.TryGet(npcId, out NpcRuntimeState state) ? state.relation : default;

        public bool belongsToOtherCompany(string npcId, string companyId)
        {
            foreach (EventCompanyData company in getCompanies())
                if (company.companyId != companyId && (company.ceoNpcId == npcId || company.restructuringNpcId == npcId))
                    return true;
            return false;
        }
    }
}
