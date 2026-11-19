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
        }
        [Serializable] public sealed class StoryResultData
        {
            public string eventId;
            public bool succeeded;
            // 스토리를 해결한 날. 0이면 기록 없음(이 필드가 생기기 전 저장). 필드가 없어도 0으로 읽혀서 형식 버전은 그대로 둔다.
            public int resolvedDay;
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
        }
        private EventSaveData preparedEventSave;
        public string saveId => "events";
        public int saveVersion => 4;
        public int rebuildOrder => 100;

        public string captureJson()
        {
            if (currentDay < 1) throw new InvalidOperationException("이벤트 초기화 후 저장해 주십시오.");
            if (mandatoryState == MandatoryStoryState.Failed && !IsMandatoryFailureReady)
                throw new InvalidOperationException("엔딩 전환이 끝난 뒤 저장해 주십시오.");
            // 결과는 이미 적용됐지만 미응답 면담은 AP만 소비된 상태이므로 저장하지 않는다.
            if (screenState != ScreenState.Closed && screenState != ScreenState.Result)
                throw new InvalidOperationException("진행 중인 만남을 마친 뒤 저장해 주십시오.");
            var data = new EventSaveData { day = currentDay, tutorialRewardGranted = tutorialRewardGranted,
                mandatoryState = MandatoryState, mandatoryFailureStake = mandatoryFailureStake };
            foreach (var item in storyResults.OrderBy(x => x.Key, StringComparer.Ordinal))
                data.storyResults.Add(new StoryResultData { eventId = item.Key, succeeded = item.Value,
                    resolvedDay = storyResolvedDays.TryGetValue(item.Key, out int knownDay) ? knownDay : 0 });
            foreach (var item in selectedChoices.OrderBy(x => x.Key, StringComparer.Ordinal))
                data.choices.Add(new ChoiceData { eventId = item.Key, companyId = "", choice = item.Value });
            foreach (var item in companyChoices.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.companyChoices.Add(new ChoiceData { eventId = item.Key.Item1, companyId = item.Key.Item2, choice = item.Value });
            foreach (var item in availableDays.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.cooldowns.Add(new TimingData { eventId = item.Key.Item1, companyId = item.Key.Item2, day = item.Value });
            foreach (var item in occurrences)
                data.occurrences.Add(new TimingData { eventId = item.definition.getId(), companyId = item.companyId, day = item.firstDay, greenmailStake = item.greenmailStake });
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
            var definitions = events.ToDictionary(x => x.getId(), StringComparer.Ordinal);
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
            else
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
            var storyIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.storyResults == null) throw new InvalidDataException("스토리 결과 목록이 누락됐습니다.");
            foreach (var entry in data.storyResults)
                if (entry == null || entry.eventId == null || !definitions.TryGetValue(entry.eventId, out var story) ||
                    !story.IsStory || !choices.ContainsKey(entry.eventId) || !storyIds.Add(entry.eventId)
                    || entry.resolvedDay < 0 || entry.resolvedDay > data.day)
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
                    || (definition.hasCompanyConditions() && !definition.IsStory ? companySelections.ContainsKey((entry.eventId, entry.companyId)) : choices.ContainsKey(entry.eventId))
                    || !IsMandatory(definition) && data.cooldowns.Any(x => x.eventId == entry.eventId && x.companyId == keyCompany && x.day > data.day))
                    throw new InvalidDataException("이벤트 발생·기한·완료 기록이 올바르지 않습니다.");
            }
            preparedEventSave = data;
        }

        public void applyLoad()
        {
            if (preparedEventSave == null) throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
            var data = preparedEventSave;
            selectedChoices.Clear(); companyChoices.Clear(); availableDays.Clear(); occurrences.Clear();
            storyResults.Clear(); storyResolvedDays.Clear();
            foreach (var entry in data.storyResults)
            {
                storyResults.Add(entry.eventId, entry.succeeded);
                if (entry.resolvedDay > 0) storyResolvedDays.Add(entry.eventId, entry.resolvedDay);
            }
            tutorialRewardGranted = data.tutorialRewardGranted;
            mandatoryState = data.mandatoryState; mandatoryFailureStake = data.mandatoryFailureStake;
            foreach (var entry in data.choices) selectedChoices.Add(entry.eventId, entry.choice);
            foreach (var entry in data.companyChoices) companyChoices.Add((entry.eventId, entry.companyId), entry.choice);
            foreach (var entry in data.cooldowns) availableDays.Add((entry.eventId, entry.companyId), entry.day);
            foreach (var entry in data.occurrences)
                occurrences.Add(new Occurrence { definition = events.Find(x => x.getId() == entry.eventId), companyId = entry.companyId, firstDay = entry.day, greenmailStake = entry.greenmailStake });
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
