using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.SaveSystem;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed partial class EventSystemController : ISaveRebuildOrder
    {
        [Serializable] public sealed class EventSaveData
        {
            public int day;
            public List<ChoiceData> choices = new List<ChoiceData>();
            public List<ChoiceData> companyChoices = new List<ChoiceData>();
            public List<TimingData> cooldowns = new List<TimingData>();
            public List<TimingData> occurrences = new List<TimingData>();
            public List<StoryResultData> storyResults = new List<StoryResultData>();
            public bool tutorialRewardGranted;
            public MandatoryStoryState mandatoryState;
            public float mandatoryFailureStake;
            public string mandatoryEventId;
            public int campaignRevision = 2;
            public string campaignRunId;
            public List<string> pendingMandatoryEvents = new List<string>();
            public List<CampaignStakeSnapshot> failureGoals = new List<CampaignStakeSnapshot>();
            public string failedMissionId, failedEndingSignal;
            public int endingLineIndex;
            public int campaignDayOffset;
            public int lastOccurrenceSerial;
            public bool politicalFavorGranted;
            public List<RepeatRecord> repeatHistory = new List<RepeatRecord>();
        }
        [Serializable] public sealed class StoryResultData
        {
            public string eventId;
            public bool succeeded;
        }
        [Serializable] public sealed class ChoiceData
        {
            public string eventId, companyId;
            public int choice;
        }
        [Serializable] public sealed class TimingData
        {
            public string eventId, companyId;
            public int day;
            public float greenmailStake;
            public int serial;
        }
        private EventSaveData preparedEventSave;
        public string saveId => "events";
        public int saveVersion => 6;
        public int rebuildOrder => 100;

        public string captureJson()
        {
            if (currentDay < 1) throw new InvalidOperationException("이벤트 초기화 후 저장해 주십시오.");
            if (mandatoryState == MandatoryStoryState.Failed && !IsMandatoryFailureReady)
                throw new InvalidOperationException("엔딩 전환이 끝난 뒤 저장해 주십시오.");
            // 결과는 이미 적용됐지만 미응답 면담은 AP만 소비된 상태이므로 저장하지 않는다.
            if (screenState != ScreenState.Closed && screenState != ScreenState.Result)
                throw new InvalidOperationException("진행 중인 만남을 마친 뒤 저장해 주십시오.");
            if (mandatoryState == MandatoryStoryState.Forced && screenState == ScreenState.Result)
                throw new InvalidOperationException("필수 사건의 결과 대사를 마친 뒤 저장해 주십시오.");
            var data = new EventSaveData { day = currentDay, tutorialRewardGranted = tutorialRewardGranted,
                mandatoryState = MandatoryState, mandatoryFailureStake = mandatoryFailureStake,
                mandatoryEventId = MandatoryState == MandatoryStoryState.Completed ? "" : mandatoryEventId,
                campaignRevision = campaignRevision, campaignRunId = campaignRunId,
                pendingMandatoryEvents = pendingMandatoryEvents.ToList(), failureGoals = failureGoals.Select(g => new CampaignStakeSnapshot { companyId = g.companyId, required = g.required, actual = g.actual }).ToList(),
                failedMissionId = failedMissionId, failedEndingSignal = failedEndingSignal, endingLineIndex = endingLineIndex,
                campaignDayOffset = campaignDayOffset, lastOccurrenceSerial = lastOccurrenceSerial, politicalFavorGranted = politicalFavorGranted,
                repeatHistory = repeatHistory.Select(item => new RepeatRecord { eventId = item.eventId, companyId = item.companyId, day = item.day, choice = item.choice, serial = item.serial }).ToList() };
            foreach (var item in storyResults.OrderBy(x => x.Key, StringComparer.Ordinal))
                data.storyResults.Add(new StoryResultData { eventId = item.Key, succeeded = item.Value });
            foreach (var item in selectedChoices.OrderBy(x => x.Key, StringComparer.Ordinal))
                data.choices.Add(new ChoiceData { eventId = item.Key, companyId = "", choice = item.Value });
            foreach (var item in companyChoices.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.companyChoices.Add(new ChoiceData { eventId = item.Key.Item1, companyId = item.Key.Item2, choice = item.Value });
            foreach (var item in availableDays.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.cooldowns.Add(new TimingData { eventId = item.Key.Item1, companyId = item.Key.Item2, day = item.Value });
            foreach (var item in occurrences)
                data.occurrences.Add(new TimingData { eventId = item.definition.getId(), companyId = item.companyId, day = item.firstDay, greenmailStake = item.greenmailStake, serial = item.serial });
            return JsonUtility.ToJson(data);
        }

        public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
        {
            preparedEventSave = null;
            if ((savedVersion < 1 || savedVersion > saveVersion) || string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("지원하지 않는 이벤트 저장 형식입니다.");
            var fields = Newtonsoft.Json.Linq.JObject.Parse(json);
            foreach (var key in new[] { "day", "choices", "companyChoices", "cooldowns", "occurrences" })
                if (fields[key] == null || fields[key].Type == Newtonsoft.Json.Linq.JTokenType.Null)
                    throw new InvalidDataException("이벤트 저장 필수 항목이 누락됐습니다.");
            var data = JsonUtility.FromJson<EventSaveData>(json);
            PrepareCampaignVersion(data, fields, savedVersion);
            // v1에는 보상과 조건별 결과 기록이 없었다. 지급/성공 여부를 과거 선택으로 추정하지 않는다.
            if (savedVersion == 1)
            {
                data.storyResults = new List<StoryResultData>();
                data.tutorialRewardGranted = false;
            }
            else if (fields["storyResults"]?.Type != Newtonsoft.Json.Linq.JTokenType.Array ||
                     fields["tutorialRewardGranted"]?.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                throw new InvalidDataException("스토리 결과 또는 튜토리얼 보상 기록이 누락됐습니다.");
            if (savedVersion >= 2)
                foreach (var entry in fields["storyResults"])
                    if (entry.Type != Newtonsoft.Json.Linq.JTokenType.Object ||
                        entry["eventId"]?.Type != Newtonsoft.Json.Linq.JTokenType.String ||
                        entry["succeeded"]?.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                        throw new InvalidDataException("스토리 결과의 ID 또는 성공 여부가 누락됐습니다.");
            if (data == null || data.day < 1 || data.day > dayStart.day || dayStart.day - data.day > 1
                || data.choices == null || data.companyChoices == null || data.cooldowns == null || data.occurrences == null)
                throw new InvalidDataException("이벤트 저장 날짜 또는 목록이 올바르지 않습니다.");
            var definitions = DefinitionsForCampaign(data.campaignRevision).ToDictionary(x => x.getId(), StringComparer.Ordinal);
            var companies = new HashSet<string>(worldReader.getCompanies().Select(x => x.companyId), StringComparer.Ordinal);
            EventDefinition Definition(string id, string company)
            {
                if (id == null || !definitions.TryGetValue(id, out var definition)
                    || company == null || company.Length > 0 && !companies.Contains(company))
                    throw new InvalidDataException("이벤트 또는 회사 ID가 올바르지 않습니다.");
                return definition;
            }
            var choices = new Dictionary<string, int>(StringComparer.Ordinal);
            var companySelections = new Dictionary<(string, string), int>();
            foreach (var entry in data.choices)
            {
                if (entry == null) throw new InvalidDataException("이벤트 선택 기록이 누락됐습니다.");
                var definition = Definition(entry.eventId, entry.companyId);
                if (entry.companyId != "" || entry.choice < 0 || entry.choice >= definition.getChoices().Count || !choices.TryAdd(entry.eventId, entry.choice))
                    throw new InvalidDataException("이벤트 선택 번호가 잘못됐거나 기록이 중복됐습니다.");
            }
            foreach (var entry in data.companyChoices)
            {
                if (entry == null) throw new InvalidDataException("회사별 선택 기록이 누락됐습니다.");
                var definition = Definition(entry.eventId, entry.companyId);
                if (entry.choice < 0 || entry.choice >= definition.getChoices().Count
                    || definition.hasCompanyConditions() && entry.companyId == ""
                    || !choices.ContainsKey(entry.eventId) || !companySelections.TryAdd((entry.eventId, entry.companyId), entry.choice))
                    throw new InvalidDataException("회사별 이벤트 선택 기록이 올바르지 않습니다.");
            }
            foreach (var choice in choices)
                if (!companySelections.Any(x => x.Key.Item1 == choice.Key && x.Value == choice.Value))
                    throw new InvalidDataException("이벤트 선택 기록이 서로 일치하지 않습니다.");
            if (savedVersion < 3)
            {
                data.mandatoryState = choices.ContainsKey(MandatoryEventId) ? MandatoryStoryState.Completed : MandatoryStoryState.Pending;
                data.mandatoryFailureStake = 0;
            }
            else if (savedVersion < 5)
            {
                if (fields["mandatoryState"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer ||
                    (fields["mandatoryFailureStake"]?.Type != Newtonsoft.Json.Linq.JTokenType.Float && fields["mandatoryFailureStake"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer) || !Enum.IsDefined(typeof(MandatoryStoryState), data.mandatoryState) ||
                    float.IsNaN(data.mandatoryFailureStake) || float.IsInfinity(data.mandatoryFailureStake) ||
                    data.mandatoryFailureStake < 0 || data.mandatoryFailureStake > 100 ||
                    data.mandatoryState != MandatoryStoryState.Failed && data.mandatoryFailureStake != 0 ||
                    (data.mandatoryState == MandatoryStoryState.Completed) != choices.ContainsKey(MandatoryEventId) ||
                    data.mandatoryState == MandatoryStoryState.Pending && data.day >= MandatoryFailureDay ||
                    (data.mandatoryState == MandatoryStoryState.Forced || data.mandatoryState == MandatoryStoryState.Failed) && data.day < MandatoryFailureDay ||
                    data.mandatoryState == MandatoryStoryState.Failed && data.mandatoryFailureStake >= 10)
                    throw new InvalidDataException("필수 스토리 기한·완료·실패 기록이 올바르지 않습니다.");
            }
            if (savedVersion < 5)
            {
                data.mandatoryEventId = data.mandatoryState == MandatoryStoryState.Forced || data.mandatoryState == MandatoryStoryState.Failed ? MandatoryEventId : "";
                data.campaignDayOffset = choices.ContainsKey(MandatoryEventId) ? Mathf.Max(0, data.day - 6) : 0;
                data.repeatHistory = new List<RepeatRecord>(); data.lastOccurrenceSerial = 0;
                data.politicalFavorGranted = data.companyChoices.Any(item => item.eventId == "political_donation_01" && item.choice == 0);
            }
            else if (savedVersion == 5)
            {
                foreach (var key in new[] { "mandatoryEventId", "campaignDayOffset", "lastOccurrenceSerial", "politicalFavorGranted", "repeatHistory" })
                    if (fields[key] == null || fields[key].Type == Newtonsoft.Json.Linq.JTokenType.Null) throw new InvalidDataException("확장 스토리·반복 이벤트 저장이 누락됐습니다.");
                if (!Enum.IsDefined(typeof(MandatoryStoryState), data.mandatoryState) || data.campaignDayOffset < 0 || data.campaignDayOffset > data.day ||
                    data.lastOccurrenceSerial < 0 || data.repeatHistory == null || float.IsNaN(data.mandatoryFailureStake) || float.IsInfinity(data.mandatoryFailureStake) || data.mandatoryFailureStake < 0 || data.mandatoryFailureStake > 100 ||
                    data.mandatoryState != MandatoryStoryState.Failed && data.mandatoryFailureStake != 0)
                    throw new InvalidDataException("확장 스토리 상태가 올바르지 않습니다.");
                bool locked = data.mandatoryState == MandatoryStoryState.Forced || data.mandatoryState == MandatoryStoryState.Failed;
                if (locked)
                {
                    if (!definitions.TryGetValue(data.mandatoryEventId, out var forced) || !IsMandatory(forced) || choices.ContainsKey(forced.getId()) ||
                        data.day < (forced.getId() == MandatoryEventId ? MandatoryFailureDay : forced.CampaignDay + data.campaignDayOffset) ||
                        !string.IsNullOrEmpty(forced.PrerequisiteEventId) && !choices.ContainsKey(forced.PrerequisiteEventId) ||
                        data.mandatoryState == MandatoryStoryState.Failed && (!IsMission(forced) || data.mandatoryFailureStake >= 10))
                        throw new InvalidDataException("강제 스토리·실패 대상이 올바르지 않습니다.");
                }
                else if (data.mandatoryEventId != "") throw new InvalidDataException("진행 중이지 않은 강제 스토리 대상이 남아 있습니다.");
            }
            var serials = new HashSet<int>();
            foreach (var record in data.repeatHistory)
            {
                if (record == null) throw new InvalidDataException("반복 사건 이력이 누락됐습니다.");
                var definition = Definition(record.eventId, record.companyId);
                if (!definition.Repeatable || record.day < 1 || record.day > data.day || record.choice < 0 || record.choice >= definition.getChoices().Count ||
                    definition.hasCompanyConditions() != (record.companyId.Length > 0) || record.serial < 1 || record.serial > data.lastOccurrenceSerial || !serials.Add(record.serial))
                    throw new InvalidDataException("반복 사건 회차·선택 이력이 올바르지 않습니다.");
            }
            var storyIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.storyResults == null) throw new InvalidDataException("스토리 결과 목록이 누락됐습니다.");
            foreach (var entry in data.storyResults)
                if (entry == null || entry.eventId == null || !definitions.TryGetValue(entry.eventId, out var story) ||
                    !story.IsStory || !choices.ContainsKey(entry.eventId) || !storyIds.Add(entry.eventId))
                    throw new InvalidDataException("스토리 결과와 완료 기록이 올바르지 않습니다.");
            var cooldownKeys = new HashSet<(string, string)>();
            foreach (var entry in data.cooldowns)
            {
                if (entry == null) throw new InvalidDataException("이벤트 재발생 기록이 누락됐습니다.");
                var definition = Definition(entry.eventId, entry.companyId);
                if (entry.day < 1 || definition.hasCompanyConditions() != (entry.companyId.Length > 0)
                    || !cooldownKeys.Add((entry.eventId, entry.companyId)))
                    throw new InvalidDataException("이벤트 재발생 기록이 올바르지 않습니다.");
            }
            var occurrenceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data.occurrences)
            {
                if (entry == null) throw new InvalidDataException("진행 가능한 이벤트가 누락됐습니다.");
                var definition = Definition(entry.eventId, entry.companyId);
                bool greenmail = definition.getConditions().Any(condition => condition.kind == EventConditionKind.CompanyGreenmailStake);
                if (savedVersion >= 4 && (float.IsNaN(entry.greenmailStake) || float.IsInfinity(entry.greenmailStake) ||
                    (greenmail ? entry.greenmailStake != 25 && entry.greenmailStake != 30 && entry.greenmailStake != 35 : entry.greenmailStake != 0)))
                    throw new InvalidDataException("이벤트의 발생 당시 요구 지분이 올바르지 않습니다.");
                if (savedVersion < 4) entry.greenmailStake = 0;
                string keyCompany = getKeyCompany(definition, entry.companyId);
                if (entry.day < 1 || entry.day > data.day || OccurrenceExpiry(definition, entry.day) <= data.day
                    || definition.hasCompanyConditions() && entry.companyId == ""
                    || !occurrenceIds.Add(entry.eventId)
                    || !definition.Repeatable && (definition.hasCompanyConditions() && !definition.IsStory ? companySelections.ContainsKey((entry.eventId, entry.companyId)) : choices.ContainsKey(entry.eventId))
                    || !IsMandatory(definition) && data.cooldowns.Any(x => x.eventId == entry.eventId && x.companyId == keyCompany && x.day > data.day))
                    throw new InvalidDataException("이벤트 발생·기한·완료 기록이 올바르지 않습니다.");
                if (definition.Repeatable)
                {
                    if (savedVersion < 5) entry.serial = ++data.lastOccurrenceSerial;
                    if (entry.serial < 1 || entry.serial > data.lastOccurrenceSerial || !serials.Add(entry.serial)) throw new InvalidDataException("반복 사건 발생 회차가 올바르지 않습니다.");
                }
            }
            if (data.mandatoryState == MandatoryStoryState.Forced && !occurrenceIds.Contains(data.mandatoryEventId))
                throw new InvalidDataException("강제 스토리의 진행 가능한 사건이 누락됐습니다.");
            ValidateCampaignSave(data, definitions, choices, occurrenceIds, savedVersion);
            preparedEventSave = data;
        }

        public void applyLoad()
        {
            if (preparedEventSave == null) throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
            var data = preparedEventSave;
            var restoredDefinitions = DefinitionsForCampaign(data.campaignRevision).ToList();
            events.Clear(); events.AddRange(restoredDefinitions);
            selectedChoices.Clear(); companyChoices.Clear(); availableDays.Clear(); occurrences.Clear();
            storyResults.Clear();
            foreach (var entry in data.storyResults) storyResults.Add(entry.eventId, entry.succeeded);
            tutorialRewardGranted = data.tutorialRewardGranted;
            mandatoryState = data.mandatoryState; mandatoryFailureStake = data.mandatoryFailureStake;
            mandatoryEventId = data.mandatoryEventId; campaignDayOffset = data.campaignDayOffset;
            campaignRevision = data.campaignRevision; campaignRunId = data.campaignRunId;
            pendingMandatoryEvents.Clear(); pendingMandatoryEvents.AddRange(data.pendingMandatoryEvents);
            failureGoals = data.failureGoals; failedMissionId = data.failedMissionId; failedEndingSignal = data.failedEndingSignal; endingLineIndex = data.endingLineIndex;
            lastOccurrenceSerial = data.lastOccurrenceSerial; politicalFavorGranted = data.politicalFavorGranted;
            repeatHistory.Clear(); repeatHistory.AddRange(data.repeatHistory);
            foreach (var entry in data.choices) selectedChoices.Add(entry.eventId, entry.choice);
            foreach (var entry in data.companyChoices) companyChoices.Add((entry.eventId, entry.companyId), entry.choice);
            foreach (var entry in data.cooldowns) availableDays.Add((entry.eventId, entry.companyId), entry.day);
            foreach (var entry in data.occurrences)
                occurrences.Add(new Occurrence { definition = events.Find(x => x.getId() == entry.eventId), companyId = entry.companyId, firstDay = entry.day, greenmailStake = entry.greenmailStake, serial = entry.serial });
            currentDay = data.day;
            activeInfo = null; negotiationChoices.Clear(); todayEvents.Clear(); todayInfos.Clear();
            screenState = ScreenState.Closed;
            detailView.hide(); negotiationView.hide();
            preparedEventSave = null;
        }

        public void rebuildAfterLoad()
        {
            // 다른 파트의 파생 수치 복원 후 카드만 재구성한다. 추첨·날짜 진행·효과 지급은 하지 않는다.
            // 기존 대시보드의 카드 배치 추첨도 게임 규칙의 난수 상태를 소비하지 않게 한다.
            var randomState = UnityEngine.Random.state;
            try
            {
                // v1~v3에는 발행 시점 지분이 없다. 거래 수치 복원 후 현재가치로 한 번 정해 보존한다.
                foreach (var item in occurrences)
                    if (item.greenmailStake == 0 && item.definition.getConditions().Any(condition => condition.kind == EventConditionKind.CompanyGreenmailStake))
                        worldReader.tryGetGreenmailStake(item.companyId, out _, out item.greenmailStake);
                RestoreMandatoryPresentation();
                rebuildToday();
                todayEventsChanged?.Invoke();
            }
            finally { UnityEngine.Random.state = randomState; }
        }
    }
}
