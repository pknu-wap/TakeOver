using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.CompanyRules;
using TakeOver.SaveSystem;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed partial class DocumentSystem : ISaveParticipant, ILegacySaveSectionInitializer, ISaveRebuildOrder
    {
        [Serializable] private sealed class SavedDocuments { public List<DocumentSaveRecord> records = new List<DocumentSaveRecord>(); }
        private readonly DocumentRegistry documentRegistry = new DocumentRegistry();
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
            var trading = FindFirstObjectByType<TradingSaveAdapter>();
            var startups = FindFirstObjectByType<StartupSystem>();
            if (version != 1 || Newtonsoft.Json.Linq.JObject.Parse(json)["records"]?.Type != Newtonsoft.Json.Linq.JTokenType.Array || data?.records == null) throw new InvalidDataException("결재 저장 기록이 누락됐습니다.");
            var arrivals = new HashSet<int>();
            var probeRecords = new List<DocumentSaveRecord>();
            foreach (var record in data.records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.type) || record.arrival < 1 || !arrivals.Add(record.arrival) ||
                    record.arrivalTurn < 0 || record.arrivalTurn > day.day ||
                    record.fields == null || record.fields.Any(field => string.IsNullOrWhiteSpace(field.name) || float.IsNaN(field.value) || float.IsInfinity(field.value)) ||
                    record.resolved && (record.resolvedTurn < 1 || record.resolvedTurn > day.day) ||
                    record.snapshot && (record.infoRows == null || string.IsNullOrWhiteSpace(record.title)) ||
                    !record.snapshot && (!documentRegistry.IsRegistered(record.type) || record.deadlineTurn < 1) ||
                    record.fields.Select(field => field.name).Distinct().Count() != record.fields.Count)
                    throw new InvalidDataException("결재 문서의 종류·회사·순서·필드가 올바르지 않습니다.");
                if (!string.IsNullOrEmpty(record.companyId))
                {
                    if (startups?.IsStartupId(record.companyId) == true && trading != null)
                    {
                        // 과거 저장과 폐업 기업의 문서를 새로 생성한 동명의 ID에 연결하지 않는다.
                        if (!trading.HasSavedStartupRoster || !trading.ContainsSavedCompany(record.companyId))
                            ArchiveUnavailableStartup(record);
                        else if (!record.snapshot)
                        {
                            ValidateStartupFields(record);
                            continue; // 재구성 단계에서 복원된 회사에 연결한다.
                        }
                    }
                    else if (companies.getCompany(record.companyId) == null)
                        throw new InvalidDataException("결재 문서의 회사를 찾을 수 없습니다.");
                }
                probeRecords.Add(record);
            }
            var probe = new DocumentInbox(new ChangeApplier(FindFirstObjectByType<Player>()), clock, settings.historyLimit);
            probe.RestoreFrom(probeRecords, documentRegistry, new DocumentContext(companies, settings,
                clock: clock, acquisitionOperations: FindFirstObjectByType<AcquisitionOperationsService>()));
            preparedDocuments = data;
        }

        private static void ArchiveUnavailableStartup(DocumentSaveRecord record)
        {
            record.companyId = "";
            record.snapshot = true;
            record.title = string.IsNullOrWhiteSpace(record.title) ? "스타트업 문서" : record.title;
            record.infoRows ??= new List<NameText>();
            record.infoRows.Add(new NameText { label = "상태", value = "원래 회사를 불러올 수 없어 열람만 가능합니다." });
        }

        private static void ValidateStartupFields(DocumentSaveRecord record)
        {
            foreach (var field in record.fields)
            {
                bool recognized = record.type switch
                {
                    StartupFundingDocument.typeId => field.name == StartupFundingDocument.amountName,
                    StartupFollowUpDocument.typeId => field.name == StartupFollowUpDocument.amountName,
                    StartupBusinessPlanDocument.typeId => field.name is StartupBusinessPlanDocument.hiringName
                        or StartupBusinessPlanDocument.marketingName or StartupBusinessPlanDocument.stanceName
                        or StartupBusinessPlanDocument.facilityName,
                    SupportRequestDocument.typeId => field.name is "employee" or "cancelled",
                    FacilityInvestmentDocument.typeId or RestructuringDocument.typeId => field.name == AcquisitionAgendaDocument.scaleName
                        && field.value >= 0f && field.value <= 2f && field.value == Mathf.Floor(field.value),
                    _ => false
                };
                if (!recognized) throw new InvalidDataException("스타트업 문서의 입력 항목이 올바르지 않습니다.");
            }
        }
        public void applyLoad() { }
        public void rebuildAfterLoad()
        {
            Inbox.RestoreFrom(preparedDocuments.records, documentRegistry, new DocumentContext(FindFirstObjectByType<Companies>(), settings,
                clock: clock, acquisitionOperations: FindFirstObjectByType<AcquisitionOperationsService>()));
            preparedDocuments = null;
        }
        public bool TryCreateLegacySection(int version, out SaveSectionData section)
        {
            section = new SaveSectionData { id = saveId, version = saveVersion, json = JsonUtility.ToJson(new SavedDocuments()) };
            return version == 2 || version == 3;
        }
    }
}
