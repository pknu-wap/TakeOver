using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Events
{
    [Serializable]
    public sealed class EventCompanyData
    {
        public string companyId;
        public string companyName;
        public string ceoNpcId;
        public string ceoName;
        public string restructuringNpcId;
        public string restructuringName;
        public string restructuringRole;
    }

    public sealed class EventWorldReader
    {
        private readonly Companies companies;
        private readonly EventTempWorldData worldData;
        private readonly NpcStateRegistry registry;

        public EventWorldReader(Companies companies, NpcStateRegistry registry, EventTempWorldData worldData = null)
        {
            this.companies = companies;
            this.registry = registry;
            this.worldData = worldData;
        }

        public IReadOnlyList<EventCompanyData> getCompanies()
        {
            var result = new List<EventCompanyData>();
            if (companies != null)
                foreach (Company company in companies.CompanyList)
                    result.Add(getCompany(company.definition.companyID));
            return result;
        }
        public EventCompanyData getCompany(string companyId, EventDefinition definition = null)
        {
            if (companies == null) return null;
            foreach (Company company in companies.CompanyList)
            {
                if (company.definition.companyID != companyId) continue;
                var data = new EventCompanyData { companyId = companyId, companyName = company.definition.companyName };
                if (registry != null)
                {
                    if (registry.TryFindByPosition(companyId, NpcPosition.CEO, out var ceo))
                    {
                        data.ceoNpcId = ceo.npcId;
                        data.ceoName = ceo.profile.displayName;
                    }
                    if (definition != null && definition.tryGetRestructuringNpcId(companyId, out string npcId)
                        && registry.TryGetProfile(npcId, out var contact))
                    {
                        data.restructuringNpcId = contact.npcId;
                        data.restructuringName = contact.profile.displayName;
                        data.restructuringRole = contact.profile.role;
                    }
                }
                return data;
            }
            return null;
        }
        public float getStake(string companyId) =>
            companies != null && companies.tryGetCompanyStake(companyId, out float stake) ? stake : 0;
        public float getConsumerReaction(string companyId) =>
            companies != null && companies.tryGetCompanyStat(companyId, CompanyStat.REPUTATION_B2C.ToString(), out float reaction) ? reaction : 0;
        public Sprite getSpeakerIllustration(TodayEventInfo info)
        {
            if (registry != null && registry.TryGetProfile(info.speakerNpcId, out var seed)
                && seed.profile.standingIllustration != null) return seed.profile.standingIllustration;
            return worldData != null ? worldData.getSpeakerIllustration(info.speakerName) : null;
        }
        public NpcRelationState getNpcRelation(string npcId) =>
            registry != null && registry.TryGet(npcId, out NpcRuntimeState state) ? state.relation : default;

        public bool belongsToOtherCompany(string npcId, string companyId) =>
            registry != null && registry.TryGetProfile(npcId, out var seed)
                && seed.companyId != companyId && seed.companyId != "external-network";
    }
}
