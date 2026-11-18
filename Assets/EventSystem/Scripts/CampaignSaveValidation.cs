using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed partial class EventSystemController
    {
        private IEnumerable<EventDefinition> DefinitionsForCampaign(int revision)
        {
            var story = revision == 1 ? Resources.Load<StoryEventDatabase>("Story/LegacyStoryEventDatabase") : storyDatabase;
            if (story == null) throw new InvalidDataException("저장에 맞는 스토리 데이터가 없습니다.");
            return events.Where(e => !e.IsStory).Concat(story.Events);
        }
        private void PrepareCampaignVersion(EventSaveData data, JObject fields, int version)
        {
            if (data == null) throw new InvalidDataException("이벤트 저장이 비어 있습니다.");
            if (version < 6)
            {
                data.campaignRevision = 1; data.campaignRunId = Guid.NewGuid().ToString("N");
                data.pendingMandatoryEvents = new List<string>(); data.failureGoals = new List<CampaignStakeSnapshot>();
                data.failedMissionId = ""; data.failedEndingSignal = ""; data.endingLineIndex = 0;
            }
            else
            {
                foreach (string key in new[] { "campaignRevision", "campaignRunId", "pendingMandatoryEvents", "failureGoals", "failedMissionId", "failedEndingSignal", "endingLineIndex",
                    "mandatoryEventId", "campaignDayOffset", "lastOccurrenceSerial", "politicalFavorGranted", "repeatHistory", "mandatoryState", "mandatoryFailureStake" })
                    if (fields[key] == null || fields[key].Type == JTokenType.Null) throw new InvalidDataException("교차 캠페인 저장 항목이 누락됐습니다: " + key);
            }
            if (data.campaignRevision != 1 && data.campaignRevision != 2 || string.IsNullOrEmpty(data.campaignRunId) || !System.Text.RegularExpressions.Regex.IsMatch(data.campaignRunId, "^[a-f0-9]{32}$") ||
                data.pendingMandatoryEvents == null || data.failureGoals == null || data.failedMissionId == null || data.failedEndingSignal == null)
                throw new InvalidDataException("캠페인 버전·실행 식별자가 올바르지 않습니다.");
        }
        private void ValidateCampaignSave(EventSaveData data, Dictionary<string, EventDefinition> definitions, Dictionary<string, int> choices, HashSet<string> occurrenceIds, int version)
        {
            var missions = data.campaignRevision == 2 ? campaignPlan.missions : definitions.Values.Where(e => e.getId() == MandatoryEventId || e.IsStory && e.MissionStartDay > 0).Select(e => new CampaignMission {
                id = e.getId(), startDay = e.getId() == MandatoryEventId ? 1 : e.MissionStartDay, dueDay = e.getId() == MandatoryEventId ? 5 : e.CampaignDay,
                goals = new List<CampaignStakeGoal> { new CampaignStakeGoal { companyId = MissionCompany(e), percent = 10 } },
                eventIds = new List<string> { e.getId() }, signals = new List<string> { e.getId() == MandatoryEventId ? MandatoryEndingSignal : e.FailureSignal }
            }).ToList();
            if (version < 6)
            {
                if (data.mandatoryState == MandatoryStoryState.Forced) data.pendingMandatoryEvents.Add(data.mandatoryEventId);
                if (data.mandatoryState == MandatoryStoryState.Failed)
                {
                    var oldMission = missions.Single(m => m.eventIds.Contains(data.mandatoryEventId));
                    data.failedMissionId = oldMission.id; data.failedEndingSignal = oldMission.signals[0];
                    data.failureGoals.Add(new CampaignStakeSnapshot { companyId = oldMission.goals[0].companyId, required = 10, actual = data.mandatoryFailureStake });
                }
            }
            if (!Enum.IsDefined(typeof(MandatoryStoryState), data.mandatoryState) || data.campaignDayOffset < 0 || data.campaignDayOffset > data.day || data.lastOccurrenceSerial < 0 || data.repeatHistory == null ||
                !float.IsFinite(data.mandatoryFailureStake) || data.mandatoryFailureStake < 0 || data.mandatoryFailureStake > 100 ||
                data.pendingMandatoryEvents.Distinct().Count() != data.pendingMandatoryEvents.Count || data.endingLineIndex < 0)
                throw new InvalidDataException("교차 캠페인 진행 상태가 올바르지 않습니다.");
            if (data.mandatoryState == MandatoryStoryState.Forced)
            {
                if (data.pendingMandatoryEvents.Count == 0 || data.mandatoryEventId != data.pendingMandatoryEvents[0]) throw new InvalidDataException("필수 사건 묶음의 대표·목록이 맞지 않습니다.");
                var groupedMission = missions.FirstOrDefault(m => m.eventIds.Contains(data.mandatoryEventId));
                var expected = groupedMission?.eventIds.Where(id => !choices.ContainsKey(id)).ToList() ?? new List<string> { data.mandatoryEventId };
                if (!expected.OrderBy(id => id).SequenceEqual(data.pendingMandatoryEvents.OrderBy(id => id))) throw new InvalidDataException("필수 사건 묶음이 일부 누락됐습니다.");
                foreach (string id in data.pendingMandatoryEvents)
                {
                    if (!definitions.TryGetValue(id, out var item) || !IsMandatory(item) || choices.ContainsKey(id) || !occurrenceIds.Contains(id) ||
                        data.day < (id == MandatoryEventId ? 5 : item.CampaignDay + data.campaignDayOffset) ||
                        !string.IsNullOrEmpty(item.PrerequisiteEventId) && !choices.ContainsKey(item.PrerequisiteEventId) || item.PrerequisiteEventIds.Any(p => !choices.ContainsKey(p)))
                        throw new InvalidDataException("필수 사건의 기한·선행 조건·발생 기록이 맞지 않습니다.");
                }
            }
            else if (data.pendingMandatoryEvents.Count != 0) throw new InvalidDataException("진행 중이지 않은 필수 사건 묶음이 남아 있습니다.");
            if (data.mandatoryState == MandatoryStoryState.Failed)
            {
                var mission = missions.FirstOrDefault(m => m.id == data.failedMissionId);
                if (mission == null || data.mandatoryEventId != mission.eventIds[0] || mission.eventIds.Any(choices.ContainsKey) || data.day < mission.dueDay + data.campaignDayOffset || data.failureGoals.Count != mission.goals.Count)
                    throw new InvalidDataException("실패 미션·판정 기록이 맞지 않습니다.");
                int mask = 0;
                for (int i = 0; i < mission.goals.Count; i++)
                {
                    var goal = data.failureGoals[i]; var required = mission.goals[i];
                    if (goal == null || goal.companyId != required.companyId || goal.required != required.percent || !float.IsFinite(goal.actual) || goal.actual < 0 || goal.actual > 100)
                        throw new InvalidDataException("실패한 회사별 실제 지분이 올바르지 않습니다.");
                    if (goal.actual + .0001f < goal.required) mask |= 1 << i;
                }
                string key = data.failedEndingSignal == "dongbang-trading-investment-deadline-failed" ? "early-a1-dongbang-stake-failed" : data.failedEndingSignal == "hansung-industry-investment-deadline-failed" ? "early-b1-hansung-stake-failed" : data.failedEndingSignal;
                var ending = campaignPlan.endings.FirstOrDefault(e => e.signal == key);
                if (mask == 0 || mission.signals[mask - 1] != data.failedEndingSignal || ending == null || data.endingLineIndex > ending.lines.Count ||
                    data.mandatoryFailureStake != data.failureGoals.First(g => g.actual + .0001f < g.required).actual)
                    throw new InvalidDataException("미달 조합과 엔딩 신호·읽기 위치가 맞지 않습니다.");
            }
            else if (data.failureGoals.Count != 0 || data.failedMissionId != "" || data.failedEndingSignal != "" || data.mandatoryFailureStake != 0 || data.endingLineIndex != 0)
                throw new InvalidDataException("정상 진행에 실패 기록이 남아 있습니다.");
            if (data.mandatoryState != MandatoryStoryState.Forced && data.mandatoryState != MandatoryStoryState.Failed && data.mandatoryEventId != "")
                throw new InvalidDataException("활성 상태가 아닌 필수 사건 대상이 남아 있습니다.");
        }
    }
}
