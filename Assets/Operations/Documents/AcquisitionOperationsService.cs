using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.SaveSystem;
using TakeOver.CompanyRules;
using UnityEngine;

namespace TakeOver.Documents
{
    // 시장 기업의 투자 효과와 주간 연속 적자 기록. 인수 조건은 호출자가 판단한다.
    public sealed class AcquisitionOperationsService : MonoBehaviour, ISaveParticipant, ILegacySaveSectionInitializer, ISaveRebuildOrder
    {
        [SerializeField] private DocumentSettings settings;
        private Companies companies;
        private TakeOver.CompanyRules.CompanyRuleRunner runner;
        private readonly Dictionary<string, CompanyRecord> records = new Dictionary<string, CompanyRecord>();
        private SavedOperations prepared;
        public DocumentSettings Settings => settings;
        public float CostPerEmployee => runner.costPerEmployee;
        public string saveId => "acquisition-operations";
        public int saveVersion => 1;
        public int rebuildOrder => 40;

        [Serializable] public sealed class Investment
        {
            public int startTurn;
            public int endTurn;
            public float growthBonus;
        }

        [Serializable] public sealed class CompanyRecord
        {
            public string companyId;
            public int lossWeeks;
            public List<Investment> investments = new List<Investment>();
        }

        [Serializable] private sealed class SavedOperations
        {
            public List<CompanyRecord> records = new List<CompanyRecord>();
        }

        private void Awake()
        {
            companies = FindFirstObjectByType<Companies>();
            runner = FindFirstObjectByType<TakeOver.CompanyRules.CompanyRuleRunner>();
        }

        private CompanyRecord recordFor(Company company)
        {
            string id = company.definition.companyID;
            if (!records.TryGetValue(id, out var record))
            {
                record = new CompanyRecord { companyId = id };
                records.Add(id, record);
            }
            return record;
        }

        public int LossWeeks(Company company) => records.TryGetValue(company.definition.companyID, out var record) ? record.lossWeeks : 0;
        public void SetLossWeeks(Company company, int weeks) => recordFor(company).lossWeeks = weeks;

        public void AddInvestment(Company company, int turn, float bonus)
        {
            int start = turn + settings.facilityDelayTurns;
            recordFor(company).investments.Add(new Investment
            {
                startTurn = start, endTurn = start + settings.facilityDurationTurns, growthBonus = bonus
            });
        }

        public float GrowthBonus(Company company, int turn)
        {
            if (!records.TryGetValue(company.definition.companyID, out var record)) return 0f;
            float total = 0f;
            foreach (var investment in record.investments)
                if (turn >= investment.startTurn && turn < investment.endTurn) total += investment.growthBonus;
            return total;
        }

        public void PruneRecords(int turn)
        {
            var ids = new HashSet<string>(companies.CompanyList.Where(company => company != null &&
                company.state.status != CompanyStatus.Delisted && company.state.status != CompanyStatus.Liquidation)
                .Select(company => company.definition.companyID));
            foreach (string id in records.Keys.Where(id => !ids.Contains(id)).ToArray()) records.Remove(id);
            foreach (var record in records.Values) record.investments.RemoveAll(investment => investment.endTurn <= turn);
        }

        public string captureJson() => JsonUtility.ToJson(new SavedOperations { records = records.Values.ToList() });

        public void prepareLoad(string json, int savedVersion, DayStartData dayStart)
        {
            prepared = null;
            var data = JsonUtility.FromJson<SavedOperations>(json);
            if (savedVersion != 1 || Newtonsoft.Json.Linq.JObject.Parse(json)["records"]?.Type != Newtonsoft.Json.Linq.JTokenType.Array || data?.records == null)
                throw new InvalidDataException("인수 회사 운영 기록이 올바르지 않습니다.");
            var ids = new HashSet<string>();
            foreach (var record in data.records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.companyId) || !ids.Add(record.companyId) ||
                    record.lossWeeks < 0 || record.investments == null)
                    throw new InvalidDataException("인수 회사의 ID·연속 적자 기록이 올바르지 않습니다.");
                foreach (var investment in record.investments)
                    if (investment == null || investment.startTurn < 1 || investment.endTurn <= investment.startTurn ||
                        float.IsNaN(investment.growthBonus) || float.IsInfinity(investment.growthBonus) || investment.growthBonus < 0f)
                        throw new InvalidDataException("설비 투자 효과 기록이 올바르지 않습니다.");
            }
            prepared = data;
        }

        public void applyLoad()
        {
            records.Clear();
            foreach (var record in prepared.records) records.Add(record.companyId, record);
            prepared = null;
        }

        public void rebuildAfterLoad() => PruneRecords(0);

        public bool TryCreateLegacySection(int sourceFormatVersion, out SaveSectionData section)
        {
            section = new SaveSectionData { id = saveId, version = saveVersion, json = JsonUtility.ToJson(new SavedOperations()) };
            return sourceFormatVersion >= 2 && sourceFormatVersion <= 5;
        }
    }
}
