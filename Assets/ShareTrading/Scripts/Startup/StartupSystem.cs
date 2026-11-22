using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Employees;
using UnityEngine;

namespace TakeOver.Startups
{
    public sealed class StartupSystem : MonoBehaviour
    {
        [SerializeField] private StartupSettings settings;
        [Header("비워 두면 씬에서 자동으로 찾는다")]
        [SerializeField] private Companies companies;
        [SerializeField] private Marketdata marketData;
        [SerializeField] private CompanyRuleRunner ruleRunner;

        private readonly List<StartupRecord> records = new List<StartupRecord>();
        private JobService jobs;
        private Transform startupRoot;
        private int lastIdNumber;

        public IReadOnlyList<StartupRecord> Records => records;
        public StartupSettings Settings => settings;

        private void Awake()
        {
            if (companies == null) companies = FindFirstObjectByType<Companies>();
            if (marketData == null) marketData = FindFirstObjectByType<Marketdata>();
            if (ruleRunner == null) ruleRunner = FindFirstObjectByType<CompanyRuleRunner>();
            jobs = FindFirstObjectByType<JobService>();
        }

        private void Start()
        {
            if (ruleRunner != null) ruleRunner.RulesCompleted += handleRulesCompleted;
            createInitialStartups();
        }

        private void OnDestroy()
        {
            if (ruleRunner != null) ruleRunner.RulesCompleted -= handleRulesCompleted;
        }

        public bool tryGetRecord(string companyId, out StartupRecord record)
        {
            foreach (StartupRecord candidate in records)
            {
                if (candidate.companyId == companyId)
                {
                    record = candidate;
                    return true;
                }
            }
            record = null;
            return false;
        }

        public IReadOnlyList<float> getRevenueHistory(Company startup)
        {
            if (startup == null || startup.definition == null) return null;
            return tryGetRecord(startup.definition.companyID, out StartupRecord record) ? record.revenueHistory : null;
        }

        private void createInitialStartups()
        {
            if (settings == null || companies == null || marketData == null)
            {
                Debug.LogError("StartupSystem: StartupSettings 에셋 또는 Companies·Marketdata를 찾을 수 없어 스타트업을 만들지 않습니다.", this);
                return;
            }
            if (settings.industries == null || settings.industries.Length == 0)
            {
                Debug.LogError("StartupSystem: 업종 틀이 비어 있어 스타트업을 만들지 않습니다.", settings);
                return;
            }

            System.Random random = settings.randomSeed != 0
                ? new System.Random(settings.randomSeed) : new System.Random();
            List<string> names = shuffleNames(random);

            startupRoot = new GameObject("Startups").transform;
            startupRoot.SetParent(transform, false);

            for (int i = 0; i < settings.initialCount; i++)
            {
                StartupRecord record = StartupFactory.createRecord(settings, random, nextCompanyId(), pickName(names, i));
                Company startup = StartupFactory.createCompany(record, startupRoot);
                if (!companies.RegisterStartup(startup))
                {
                    Destroy(startup.gameObject);
                    continue;
                }

                records.Add(record);
                startup.calculator.calculateInitializeStat(startup, marketData);
            }

            Debug.Log($"스타트업 {companies.StartupList.Count}개 생성됨.");
        }

        private string nextCompanyId()
        {
            string companyId;
            do
            {
                lastIdNumber++;
                companyId = $"{settings.idPrefix}{lastIdNumber:00}";
            }
            while (companies.tryGetCompany(companyId, out _));
            return companyId;
        }

        private List<string> shuffleNames(System.Random random)
        {
            List<string> names = new List<string>();
            if (settings.names != null)
            {
                foreach (string candidate in settings.names)
                {
                    if (!string.IsNullOrWhiteSpace(candidate)) names.Add(candidate.Trim());
                }
            }

            for (int i = names.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                string temp = names[i];
                names[i] = names[j];
                names[j] = temp;
            }
            return names;
        }

        private static string pickName(List<string> names, int index)
        {
            if (names.Count == 0) return $"스타트업 {index + 1}";
            string baseName = names[index % names.Count];
            int round = index / names.Count;
            return round == 0 ? baseName : $"{baseName} {round + 1}";
        }

        private void handleRulesCompleted(int turn)
        {
            removeClosedRecords();

            int endedTurn = turn - 1;
            foreach (Company startup in companies.StartupList)
            {
                startup.calculator.calculateTurnend(startup, marketData);
                startup.history.recordHistory(endedTurn);
                startup.calculator.recalculate(startup, marketData);
            }

            if (turn % ruleRunner.performancePeriodTurns == 0) recordWeeklyRevenue();
        }

        private void recordWeeklyRevenue()
        {
            int maxCount = settings.revenueHistoryWeeks + 1;
            foreach (Company startup in companies.StartupList)
            {
                if (!tryGetRecord(startup.definition.companyID, out StartupRecord record)) continue;
                record.revenueHistory.Add(startup.state.getStat(CompanyStat.REVENUE));
                while (record.revenueHistory.Count > maxCount) record.revenueHistory.RemoveAt(0);
            }
        }

        private void removeClosedRecords()
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (!isInStartupList(records[i].companyId)) records.RemoveAt(i);
            }
        }

        private bool isInStartupList(string companyId)
        {
            foreach (Company startup in companies.StartupList)
            {
                if (startup.definition.companyID == companyId) return true;
            }
            return false;
        }

        [ContextMenu("검증용: 첫 스타트업 상장 (PromoteStartup)")]
        private void promoteFirstStartupForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            if (companies.PromoteStartup(startup)) Debug.Log($"{startup.definition.companyName} 상장됨 (검증용).");
        }

        [ContextMenu("검증용: 첫 스타트업을 파산 직전으로 (다음 7턴 갱신 때 파산)")]
        private void drainFirstStartupForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            startup.state.setStat(CompanyStat.REVENUE, 0f);
            startup.state.setStat(CompanyStat.CASH, 0f);
            startup.state.setStat(CompanyStat.TANGIBLE_ASSETS, 0f);
            startup.state.setStat(CompanyStat.INTANGIBLE_ASSTES, 0f);
            Debug.Log($"{startup.definition.companyName}의 매출·현금·자산을 0으로 만듦 (검증용).");
        }

        public bool markContracted(Company startup, int turn)
        {
            if (startup == null || !tryGetRecord(startup.definition.companyID, out StartupRecord record)) return false;
            record.contracted = true;
            record.contractTurn = turn;
            return true;
        }

        [ContextMenu("검증용: 첫 스타트업 계약한 것으로 만들기")]
        private void contractFirstStartupForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            TakeOver.NPC.GameTurnClock clock = FindFirstObjectByType<TakeOver.NPC.GameTurnClock>();
            if (!markContracted(startup, clock != null ? clock.CurrentTurn : 0)) return;
            if (startup.state.getBaseStat(CompanyStat.PLAYER_SHARES) <= 0f)
                startup.state.setStat(CompanyStat.PLAYER_SHARES, startup.state.totalShares * 0.2f);
            Debug.Log($"{startup.definition.companyName}을(를) 계약한 스타트업으로 만듦 (검증용). 지분 {startup.state.getBaseStat(CompanyStat.PLAYER_SHARES) * 100f / startup.state.totalShares:0.##}%");
        }

        [ContextMenu("검증용: 첫 스타트업 현금만 0으로 (자금 요청 문서 확인)")]
        private void emptyFirstStartupCashForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            startup.state.setStat(CompanyStat.CASH, 0f);
            Debug.Log($"{startup.definition.companyName}의 현금을 0으로 만듦 (검증용). 다음 턴에 자금 요청이 올라온다.");
        }

        [ContextMenu("검증용: 첫 스타트업 상장 요건 채우기 (상장 추진 문서 확인)")]
        private void fillFirstStartupListingForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            startup.state.setStat(CompanyStat.REVENUE, settings.listingRevenue);
            Debug.Log($"{startup.definition.companyName}의 매출을 상장 기준({settings.listingRevenue:N0})으로 만듦 (검증용). 다음 턴에 상장 추진 보고서가 오고, 그다음 턴에 상장한다.");
        }


        [ContextMenu("검증용: 첫 스타트업 후속 투자 바로 받기 (후속 투자 문서 확인)")]
        private void triggerFirstStartupFollowUpForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            if (!tryGetRecord(startup.definition.companyID, out StartupRecord record)) return;
            record.nextFollowUpTurn = 1;

            float value = startup.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE);
            float baseValue = startup.state.getBaseStat(CompanyStat.BASE_COMPANY_VALUE);
            if (value < settings.followUpMinCompanyValue && value > 0f && baseValue > 0f)
                startup.state.setStat(CompanyStat.BASE_COMPANY_VALUE, baseValue * settings.followUpMinCompanyValue / value * 1.1f);
            Debug.Log($"{startup.definition.companyName}의 후속 투자 예정일을 당김 (검증용). 1~2턴 뒤에 후속 투자가 올라온다.");
        }

        // 스타트업용 실적 규칙(CompanyRuleRunner)이 부른다. 지금 턴에 효과가 있는 시설 투자 보너스를 모두 더한다.
        public float facilityGrowthBonus(Company company, int turn)
        {
            if (!tryGetRecord(company.definition.companyID, out StartupRecord record)) return 0f;
            float bonus = 0f;
            foreach (StartupFacilityInvestment investment in record.facilityInvestments)
                if (turn >= investment.startTurn && turn < investment.endTurn) bonus += investment.growthBonus;
            return bonus;
        }

        public float extraGrowthBonus(Company company, int turn) =>
            facilityGrowthBonus(company, turn) + supportGrowthBonus(company);

        public float supportGrowthBonus(Company company)
        {
            if (jobs == null || settings == null) return 0f;
            return jobs.getSupportManagement(company) * settings.supportGrowthPerManagement;
        }

        public float supportCostReduction(Company company)
        {
            if (jobs == null || settings == null) return 0f;
            float reduction = jobs.getSupportManagement(company) * settings.supportCostReductionPerManagement;
            return Mathf.Clamp(reduction, 0f, settings.supportMaxCostReduction);
        }

        [ContextMenu("검증용: 첫 스타트업 연간 사업 계획 바로 받기")]
        private void triggerFirstStartupBusinessPlanForTest()
        {
            if (companies == null || companies.StartupList.Count == 0) return;
            Company startup = companies.StartupList[0];
            if (!tryGetRecord(startup.definition.companyID, out StartupRecord record)) return;
            record.nextBusinessPlanTurn = 1;
            Debug.Log($"{startup.definition.companyName}의 연간 사업 계획 예정일을 당김 (검증용). 다음 턴에 올라온다.");
        }
    }
}
