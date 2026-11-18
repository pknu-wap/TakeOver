using System;
using System.Collections.Generic;
using System.Linq;
using TakeOver.SaveSystem;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed partial class EventSystemController
    {
        [Serializable] public sealed class RepeatRecord
        {
            public string eventId, companyId;
            public int day, choice, serial;
        }
        private readonly List<RepeatRecord> repeatHistory = new List<RepeatRecord>();
        private int lastOccurrenceSerial;
        private bool politicalFavorGranted;
        public IReadOnlyList<RepeatRecord> RepeatHistory => repeatHistory.AsReadOnly();
        private static readonly string[] StoryCompanyIds = { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" };
        private static readonly string[] CompanyEntryIds = { MandatoryEventId, "main_haneul_01", "main_dongbang_01", "main_hansung_01" };
        private static readonly string[] CompanyFinalIds = { "main_mirae_02", "main_haneul_02", "main_dongbang_02", "main_hansung_02" };

        public bool CompanyStoryStarted(string companyId)
        {
            int index = Array.IndexOf(StoryCompanyIds, companyId);
            return index >= 0 && selectedChoices.ContainsKey(CompanyEntryIds[index]);
        }
        public bool CompanyStoryCompleted(string companyId)
        {
            int index = Array.IndexOf(StoryCompanyIds, companyId);
            return index >= 0 && selectedChoices.ContainsKey(campaignRevision == 1 ? CompanyFinalIds[index] : index == 0 || index == 2 ? "main_pair_a_complete" : "main_pair_b_complete");
        }
        private bool PresentationGate(EventDefinition item, string companyId)
        {
            if (item.IsTutorialOnly) return true;
            if (item.getId() == "greenmail_01") return CompanyStoryCompleted(companyId);
            if (item.FirstContact) return tutorialRewardGranted && !TakeOver.Story.StoryDirector.IsRunning;
            if (!item.Repeatable) return true;
            if (TakeOver.Story.StoryDirector.IsRunning) return false;
            if (item.hasCompanyConditions() && !CompanyStoryStarted(companyId)) return false;
            return !string.IsNullOrEmpty(item.RepeatGroup) || CompanyStoryStarted(companyId);
        }
        private string RepeatKey(EventDefinition item, string companyId) => string.IsNullOrEmpty(item.RepeatGroup) ? companyId : item.RepeatGroup;
        private bool RepeatReady(EventDefinition item, string companyId)
        {
            string group = RepeatKey(item, companyId);
            if (occurrences.Any(active => active.definition.Repeatable && RepeatKey(active.definition, active.companyId) == group)) return false;
            foreach (var record in repeatHistory)
            {
                var past = events.Find(definition => definition.getId() == record.eventId);
                if (past != null && RepeatKey(past, record.companyId) == group && currentDay < record.day + past.RepeatCooldown) return false;
            }
            if (item.getId() == "stock_manipulation_01" && FindFirstObjectByType<EventStockmanipulationAccept>()?.ScheduledEffects.Count > 0) return false;
            return !availableDays.TryGetValue((item.getId(), getKeyCompany(item, companyId)), out int day) || currentDay >= day;
        }
        private uint DailyOrder(string key)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in FixedDaySeed.seed + ":" + currentDay + ":" + key) hash = (hash ^ value) * 16777619;
                return hash;
            }
        }
        private void SpawnRepeatableOffers()
        {
            var candidates = new List<(EventDefinition definition, string company)>();
            foreach (var item in events.Where(definition => definition.Repeatable))
            {
                if (!hasRequiredHoldings(item) || occurrences.Any(active => active.definition == item)) continue;
                if (item.hasCompanyConditions())
                {
                    foreach (var company in worldReader.getCompanies())
                        if (PresentationGate(item, company.companyId) && RepeatReady(item, company.companyId) && meetsConditions(item, company.companyId)) candidates.Add((item, company.companyId));
                }
                else if (PresentationGate(item, "") && RepeatReady(item, "") && meetsConditions(item, "")) candidates.Add((item, ""));
            }
            var winners = candidates.GroupBy(item => RepeatKey(item.definition, item.company))
                .Where(group => DailyOrder("chance:" + group.Key) % 10000 < 2000)
                .OrderBy(group => DailyOrder("priority:" + group.Key));
            int added = 0;
            foreach (var group in winners)
            {
                if (added == 2) break;
                var available = group.Where(item => !occurrences.Any(active => active.definition == item.definition)).OrderBy(item => DailyOrder("event:" + item.definition.getId() + ":" + item.company)).ToList();
                if (available.Count == 0) continue;
                var winner = available[0];
                occurrences.Add(new Occurrence { definition = winner.definition, companyId = winner.company, firstDay = currentDay, serial = ++lastOccurrenceSerial });
                added++;
            }
        }
        private void SpawnFirstContacts()
        {
            foreach (var item in events.Where(definition => definition.FirstContact))
                if (PresentationGate(item, "") && canAppear(item, "") && !occurrences.Any(active => active.definition == item))
                    occurrences.Add(new Occurrence { definition = item, companyId = "", firstDay = currentDay });
        }
        private string RecordRepeatChoice(EventDefinition item, string companyId, int choice)
        {
            if (item.getId() == "political_donation_01" && choice == 0) politicalFavorGranted = true;
            if (!item.Repeatable) return null;
            var occurrence = occurrences.Find(active => active.definition == item);
            int serial = occurrence?.serial ?? ++lastOccurrenceSerial;
            repeatHistory.Add(new RepeatRecord { eventId = item.getId(), companyId = companyId, day = currentDay, choice = choice, serial = serial });
            availableDays[(item.getId(), getKeyCompany(item, companyId))] = currentDay + item.RepeatCooldown;
            return $"repeat:{serial}:{item.getId()}:{companyId}";
        }
    }
}
