using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.Story;

namespace TakeOver.Journal
{
    
    public static class JournalEntryBuilder
    {
        
        private static readonly string[] axisNames = { "신뢰", "존중", "친밀" };

        
        public static List<JournalEntry> buildStoryEntries(EventSystemController events, NpcStateRegistry npcs)
        {
            if (events == null) return new List<JournalEntry>();
            var entries = buildTutorialEntries(events, npcs);

            foreach (EventDefinition definition in events.StoryEvents)
            {
                if (definition == null || definition.IsTutorialOnly) continue;

                
                if (!events.tryGetChoice(definition.getId(), out int choiceIndex)) continue;

                IReadOnlyList<EventChoice> choices = definition.getChoices();
                if (choiceIndex < 0 || choiceIndex >= choices.Count) continue;

                JournalEntry entry = buildEntry(definition, choices[choiceIndex], events, npcs);
                entry.resolvedDay = events.tryGetStoryResolvedDay(definition.getId(), out int resolvedDay) ? resolvedDay : 0;
                entries.Add(entry);
            }

            return entries.Select((entry, order) => (entry, order))
                .OrderBy(item => item.entry.resolvedDay)
                .ThenBy(item => item.order)
                .Select(item => item.entry)
                .ToList();
        }

        private static List<JournalEntry> buildTutorialEntries(EventSystemController events, NpcStateRegistry npcs)
        {
            var entries = new List<JournalEntry>();
            JournalEntry visit = null;
            var definition = events.TutorialVisit;
            if (definition != null && events.tryGetChoice(definition.getId(), out int choiceIndex))
            {
                visit = buildEntry(definition, definition.getChoices()[choiceIndex], events, npcs);
                visit.resolvedDay = events.TutorialVisitResolvedDay;
            }

            var decisions = events.TutorialDecisions.ToDictionary(item => item.beatId);
            var sequence = StoryLaunch.IntroPrefab?.GetComponent<StoryDirector>()?.Sequence;
            if (sequence != null)
                foreach (var beat in sequence.beats)
                {
                    if (beat.goal == StoryGoal.CompleteEvent && visit != null)
                    {
                        entries.Add(visit);
                        visit = null;
                    }
                    if (!decisions.TryGetValue(beat.id, out var decision)) continue;
                    var choice = beat.choices[decision.choice];
                    var entry = new JournalEntry
                    {
                        eventId = "tutorial:" + beat.id,
                        title = string.Join(" · ", beat.sceneTitle.Split('/').Select(part => part.Trim())),
                        summary = choice.interpretation,
                        photo = beat.background,
                        resolvedDay = decision.day
                    };
                    entry.keyPoints.Add("나의 선택: " + choice.title);
                    addPerson(entry, choice.targetNpcId, null, npcs);
                    entries.Add(entry);
                }
            if (visit != null) entries.Add(visit);
            return entries;
        }

        private static JournalEntry buildEntry(EventDefinition definition, EventChoice choice,
            EventSystemController events, NpcStateRegistry npcs)
        {
           
            Sprite photo = definition.getImageFor(definition.getSpeakerNpcId());
            if (photo == null) photo = definition.getCardImage();

            var entry = new JournalEntry
            {
                eventId = definition.getId(),
                title = definition.getTitle(),
                summary = definition.getDescription(),
                photo = photo
            };

         
            bool succeeded = !events.TryGetStoryResult(definition.getId(), out bool storySucceeded) || storySucceeded;
            EventOutcome outcome = succeeded ? choice : (choice.getFailureOutcome() ?? choice);

            entry.keyPoints.Add("나의 선택: " + choice.getText());
            if (!succeeded) entry.keyPoints.Add("조건을 충족하지 못해 다른 결과로 이어졌다");

         
            addPerson(entry, definition.getSpeakerNpcId(), definition.getSpeakerName(), npcs);

            NpcEventPayload npcResult = outcome.getNpcResult();
            bool hasTarget = false;
            if (npcResult != null && npcResult.targetNpcIds != null)
            {
                foreach (string npcId in npcResult.targetNpcIds)
                {
                    if (string.IsNullOrWhiteSpace(npcId)) continue;
                    hasTarget = true;

                    string change = formatDelta(calculateDelta(npcResult, npcId, npcs));
                    entry.keyPoints.Add(getDisplayName(npcId, null, npcs) + "  " + (change ?? "관계 변화 없음"));
                    addPerson(entry, npcId, null, npcs);
                }
            }
            if (!hasTarget) entry.keyPoints.Add("관계 변화 없음");

            return entry;
        }

      
        private static NpcRelationDelta calculateDelta(NpcEventPayload payload, string npcId, NpcStateRegistry npcs)
        {
            NpcEventReaction reaction = payload.targetReactions?.Find(item => item != null && item.targetNpcId == npcId);
            NpcMemoryType memoryType = reaction != null ? reaction.memoryType : payload.memoryType;
            NpcRelationDelta delta = reaction != null ? reaction.relationDelta : payload.relationDelta;
            if (npcs == null) return delta;

            delta = npcs.ApplyProfileEventBias(npcId, memoryType, delta);
            return npcs.EvaluatePreferenceDetails(npcId, payload.actionId, memoryType, delta).relationDelta;
        }

       
        private static string formatDelta(NpcRelationDelta delta)
        {
            float[] values = { delta.trust, delta.respect, delta.closeness };
            var parts = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (Mathf.Abs(values[i]) < 0.05f) continue;
                parts.Add(axisNames[i] + " " + Mathf.Abs(values[i]).ToString("0.#") + (values[i] > 0 ? " 증가" : " 감소"));
            }
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private static void addPerson(JournalEntry entry, string npcId, string fallbackName, NpcStateRegistry npcs)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return;
            if (entry.people.Exists(person => person.npcId == npcId)) return; 

            Sprite portrait = null;
            if (npcs != null && npcs.TryGetProfile(npcId, out NpcStateRegistry.NpcSeed seed) && seed.profile != null)
                portrait = seed.profile.standingIllustration;

            entry.people.Add(new JournalPerson
            {
                npcId = npcId,
                displayName = getDisplayName(npcId, fallbackName, npcs),
                portrait = portrait
            });
        }

       
        private static string getDisplayName(string npcId, string fallbackName, NpcStateRegistry npcs)
        {
            if (npcs != null && npcs.TryGetProfile(npcId, out NpcStateRegistry.NpcSeed seed) &&
                seed.profile != null && !string.IsNullOrWhiteSpace(seed.profile.displayName))
                return seed.profile.displayName;
            return string.IsNullOrWhiteSpace(fallbackName) ? npcId : fallbackName;
        }
    }
}
