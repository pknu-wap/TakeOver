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
        }
        private EventSaveData preparedEventSave;
        public string saveId => "events";
        public int saveVersion => 1;
        public int rebuildOrder => 100;

        public string captureJson()
        {
            if (currentDay < 1) throw new InvalidOperationException("이벤트 초기화 후 저장해 주십시오.");
            // 결과는 이미 적용됐지만 미응답 면담은 AP만 소비된 상태이므로 저장하지 않는다.
            if (screenState != ScreenState.Closed && screenState != ScreenState.Result)
                throw new InvalidOperationException("진행 중인 만남을 마친 뒤 저장해 주십시오.");
            var data = new EventSaveData { day = currentDay };
            foreach (var item in selectedChoices.OrderBy(x => x.Key, StringComparer.Ordinal))
                data.choices.Add(new ChoiceData { eventId = item.Key, companyId = "", choice = item.Value });
            foreach (var item in companyChoices.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.companyChoices.Add(new ChoiceData { eventId = item.Key.Item1, companyId = item.Key.Item2, choice = item.Value });
            foreach (var item in availableDays.OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
                data.cooldowns.Add(new TimingData { eventId = item.Key.Item1, companyId = item.Key.Item2, day = item.Value });
            foreach (var item in occurrences)
                data.occurrences.Add(new TimingData { eventId = item.definition.getId(), companyId = item.companyId, day = item.firstDay });
            return JsonUtility.ToJson(data);
        }

        public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
        {
            preparedEventSave = null;
            if (savedVersion != saveVersion || string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("지원하지 않는 이벤트 저장 형식입니다.");
            var fields = Newtonsoft.Json.Linq.JObject.Parse(json);
            foreach (var key in new[] { "day", "choices", "companyChoices", "cooldowns", "occurrences" })
                if (fields[key] == null || fields[key].Type == Newtonsoft.Json.Linq.JTokenType.Null)
                    throw new InvalidDataException("이벤트 저장 필수 항목이 누락됐습니다.");
            var data = JsonUtility.FromJson<EventSaveData>(json);
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
                string keyCompany = getKeyCompany(definition, entry.companyId);
                if (entry.day < 1 || entry.day > data.day || (long)entry.day + definition.getValidDays() <= data.day
                    || definition.hasCompanyConditions() && entry.companyId == ""
                    || !occurrenceIds.Add(entry.eventId)
                    || (definition.hasCompanyConditions() ? companySelections.ContainsKey((entry.eventId, entry.companyId)) : choices.ContainsKey(entry.eventId))
                    || data.cooldowns.Any(x => x.eventId == entry.eventId && x.companyId == keyCompany && x.day > data.day))
                    throw new InvalidDataException("이벤트 발생·기한·완료 기록이 올바르지 않습니다.");
            }
            preparedEventSave = data;
        }

        public void applyLoad()
        {
            if (preparedEventSave == null) throw new InvalidOperationException("prepareLoad를 먼저 완료해야 합니다.");
            var data = preparedEventSave;
            selectedChoices.Clear(); companyChoices.Clear(); availableDays.Clear(); occurrences.Clear();
            foreach (var entry in data.choices) selectedChoices.Add(entry.eventId, entry.choice);
            foreach (var entry in data.companyChoices) companyChoices.Add((entry.eventId, entry.companyId), entry.choice);
            foreach (var entry in data.cooldowns) availableDays.Add((entry.eventId, entry.companyId), entry.day);
            foreach (var entry in data.occurrences)
                occurrences.Add(new Occurrence { definition = events.Find(x => x.getId() == entry.eventId), companyId = entry.companyId, firstDay = entry.day });
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
                rebuildToday();
                todayEventsChanged?.Invoke();
            }
            finally { UnityEngine.Random.state = randomState; }
        }
    }
}
