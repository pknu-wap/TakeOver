using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.CompanyRules;
using TakeOver.SaveSystem;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed partial class DocumentSystem : ISaveParticipant, ILegacySaveSectionInitializer, ISaveRebuildOrder
    {
        [Serializable] private sealed class SavedDocuments { public List<DocumentSaveRecord> records = new List<DocumentSaveRecord>(); }
        private SavedDocuments preparedDocuments;
        public string saveId => "documents";
        public int saveVersion => 1;
        public int rebuildOrder => 50;
        public string captureJson() => JsonUtility.ToJson(new SavedDocuments { records = Inbox.ToSaveRecords() });
        public void prepareLoad(string json, int version, DayStartData day)
        {
            preparedDocuments = null;
            var data = JsonUtility.FromJson<SavedDocuments>(json);
            var companies = FindFirstObjectByType<Companies>();
            if (version != 1 || Newtonsoft.Json.Linq.JObject.Parse(json)["records"]?.Type != Newtonsoft.Json.Linq.JTokenType.Array || data?.records == null) throw new InvalidDataException("결재 저장 기록이 누락됐습니다.");
            var arrivals = new HashSet<int>();
            foreach (var record in data.records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.type) || record.arrival < 1 || !arrivals.Add(record.arrival) ||
                    record.fields == null || record.fields.Any(field => string.IsNullOrWhiteSpace(field.name) || float.IsNaN(field.value) || float.IsInfinity(field.value)) ||
                    record.resolved && (record.resolvedTurn < 1 || record.resolvedTurn > day.day) ||
                    record.snapshot && (record.infoRows == null || string.IsNullOrWhiteSpace(record.title)) ||
                    !record.snapshot && record.type != DividendPolicyDocument.typeId && record.type != SampleChoiceDocument.typeId ||
                    !record.snapshot && record.deadlineTurn < 1 ||
                    record.fields.Select(field => field.name).Distinct().Count() != record.fields.Count ||
                    record.type == DividendPolicyDocument.typeId && (string.IsNullOrEmpty(record.companyId) || record.fields.Count != 1 || record.fields[0].name != "dividendRatio" || record.fields[0].value < 0 || record.fields[0].value > 1) ||
                    !string.IsNullOrEmpty(record.companyId) && companies.getCompany(record.companyId) == null)
                    throw new InvalidDataException("결재 문서의 종류·회사·순서·필드가 올바르지 않습니다.");
            }
            var probe = new DocumentInbox(new ChangeApplier(FindFirstObjectByType<Player>()), clock, settings.historyLimit);
            probe.RestoreFrom(data.records, new DocumentRegistry(), new DocumentContext(companies, settings));
            preparedDocuments = data;
        }
        public void applyLoad() { }
        public void rebuildAfterLoad()
        {
            Inbox.RestoreFrom(preparedDocuments.records, new DocumentRegistry(), new DocumentContext(FindFirstObjectByType<Companies>(), settings));
            preparedDocuments = null;
        }
        public bool TryCreateLegacySection(int version, out SaveSectionData section)
        {
            section = new SaveSectionData { id = saveId, version = saveVersion, json = JsonUtility.ToJson(new SavedDocuments()) };
            return version == 2 || version == 3;
        }
    }
}
