using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    // 스타트업의 1년 운영 방향을 정하는 문서. 빈칸 4개(채용, 마케팅, 운영 기조, 시설 투자)를 모두 골라야 승인할 수 있다.
    // 쓰는 돈은 전부 스타트업 회사 현금에서 나가고, 모자라면 부족한 만큼 부채로 빌린다. 플레이어 현금은 그대로다.
    // 거절: 변화 없음 (임시)
    public sealed class StartupBusinessPlanDocument : IApprovalDocument
    {
        public const string typeId = "startup-business-plan";
        public const string hiringName = "hiring";
        public const string marketingName = "marketing";
        public const string stanceName = "stance";
        public const string facilityName = "facility";

        // 선택지 키. 네 빈칸 모두 0 / 1 / 2 세 단계다.
        private const int none = 0;
        private const int middle = 1;
        private const int high = 2;

        private readonly StartupRecord record;   // 복원 때 이미 상장한 회사면 null일 수 있다
        private readonly Companies companies;
        private readonly StartupSettings settings;
        private readonly GameTurnClock clock;
        private readonly List<DocumentChoiceField> choiceFields;

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "연간 사업 계획";
        public string Author => "";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";

        // 선택을 바꿀 때마다 화면이 다시 읽어서 예상 지출이 바로 바뀐다.
        public IReadOnlyList<DocumentInfo> InfoRows
        {
            get
            {
                float cash = TargetCompany.state.getBaseStat(CompanyStat.CASH);
                float spend = totalSpend();
                List<DocumentInfo> rows = new List<DocumentInfo>
                {
                    new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
                    new DocumentInfo("회사 현금", $"{cash:N0}원"),
                    new DocumentInfo("예상 지출", $"{spend:N0}원")
                };
                if (spend > cash) rows.Add(new DocumentInfo("부족분 대출", $"{spend - cash:N0}원"));
                return rows;
            }
        }

        public IReadOnlyList<string> BodyLines => new[]
        {
            $"{TargetCompany.definition.companyName}의 올해 사업 계획입니다.",
            "빈칸 4개를 모두 골라 승인하세요.",
            "비용은 회사 현금에서 나가고, 모자라면 부족한 금액을 대출로 충당합니다."
        };

        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => choiceFields;

        public StartupBusinessPlanDocument(Company startup, StartupRecord record, Companies companies,
            StartupSettings settings, GameTurnClock clock, int deadlineTurn)
        {
            TargetCompany = startup;
            this.record = record;
            this.companies = companies;
            this.settings = settings;
            this.clock = clock;
            DeadlineTurn = deadlineTurn;

            // 선택지 이름에 비용을 같이 보여 줘서 플레이어가 회사 현금과 비교할 수 있게 한다.
            choiceFields = new List<DocumentChoiceField>
            {
                new DocumentChoiceField(hiringName, "채용 규모", () => new[]
                {
                    new DocumentChoice { key = none, label = "동결" },
                    new DocumentChoice { key = middle, label = "소폭" },
                    new DocumentChoice { key = high, label = "대폭" }
                }),
                new DocumentChoiceField(marketingName, "마케팅 투자", () => new[]
                {
                    new DocumentChoice { key = none, label = "안 함" },
                    new DocumentChoice { key = middle, label = $"보통 ({settings.marketingNormalCost:N0}원)" },
                    new DocumentChoice { key = high, label = $"집중 ({settings.marketingFocusCost:N0}원)" }
                }),
                new DocumentChoiceField(stanceName, "운영 기조", () => new[]
                {
                    new DocumentChoice { key = none, label = "공격" },
                    new DocumentChoice { key = middle, label = "균형" },
                    new DocumentChoice { key = high, label = "보수" }
                }),
                new DocumentChoiceField(facilityName, "시설 투자", () => new[]
                {
                    new DocumentChoice { key = none, label = "없음" },
                    new DocumentChoice { key = middle, label = $"중간 ({settings.facilityMediumCost:N0}원)" },
                    new DocumentChoice { key = high, label = $"대규모 ({settings.facilityLargeCost:N0}원)" }
                })
            };
        }

        // 저장 복원용. 고른 값은 DocumentRegistry.Restore가 SetChoice로 다시 넣는다.
        public static StartupBusinessPlanDocument restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company startup = context.companies.getCompany(saved.companyId);
            if (startup == null || context.startupSystem == null) return null;
            context.startupSystem.tryGetRecord(saved.companyId, out StartupRecord record);
            return new StartupBusinessPlanDocument(startup, record, context.companies, context.startupSystem.Settings,
                context.clock, saved.deadlineTurn);
        }

        public void SetField(string name, float value)
        {
            throw new ArgumentException("알 수 없는 문서 입력 칸입니다.", nameof(name));
        }

        public void SetChoice(string name, int key)
        {
            DocumentChoiceField field = findField(name);
            if (field == null) throw new ArgumentException("알 수 없는 문서 선택 칸입니다.", nameof(name));
            field.value = key;
        }

        public bool CanApprove(out string reason)
        {
            if (!StartupListingDocument.isStartup(companies, TargetCompany))
            {
                reason = "이미 폐업했거나 상장한 회사입니다";
                return false;
            }
            foreach (DocumentChoiceField field in choiceFields)
            {
                if (field.value.HasValue) continue;
                reason = $"{field.label}을(를) 골라 주세요";
                return false;
            }
            reason = "";
            return true;
        }

        public RuleResult OnApprove()
        {
            CompanyState state = TargetCompany.state;
            RuleResult result = new RuleResult();

            // 직원: 채용 규모 + 운영 기조. 최소 1명은 남긴다.
            float employees = state.getBaseStat(CompanyStat.EMPLOYEE_COUNT);
            float employeeChange = hiringChange(employees) + stanceEmployeeChange(employees);
            employeeChange = Mathf.Max(employeeChange, 1f - employees);
            if (employeeChange != 0f) result.stats.Add(new StatChange(CompanyStat.EMPLOYEE_COUNT, employeeChange));

            // 소비자 평판: 마케팅 + 운영 기조. 0~100 안에서 끝나게 자른다.
            float reputation = state.getBaseStat(CompanyStat.REPUTATION_B2C);
            float nextReputation = Mathf.Clamp(reputation + marketingGain(reputation) + stanceReputationChange(), 0f, 100f);
            if (nextReputation != reputation)
                result.stats.Add(new StatChange(CompanyStat.REPUTATION_B2C, nextReputation - reputation));

            // 돈: 마케팅비 + 시설 투자금. 회사 현금이 모자라면 남은 금액은 부채가 된다.
            float spend = totalSpend();
            float cash = state.getBaseStat(CompanyStat.CASH);
            float fromCash = Mathf.Min(spend, Mathf.Max(0f, cash));
            if (fromCash > 0f) result.stats.Add(new StatChange(CompanyStat.CASH, -fromCash));
            if (spend > fromCash) result.stats.Add(new StatChange(CompanyStat.DEBT, spend - fromCash));

            // 시설: 투자금만큼 유형자산이 늘고, 몇 달 뒤부터 성장 보너스가 붙도록 기록한다.
            float facility = facilityCost();
            if (facility > 0f)
            {
                result.stats.Add(new StatChange(CompanyStat.TANGIBLE_ASSETS, facility));
                recordFacility(facility);
            }
            return result;
        }

        public RuleResult OnReject()
        {
            return new RuleResult();
        }

        public DocumentSaveRecord ToSaveRecord()
        {
            DocumentSaveRecord saved = new DocumentSaveRecord
            {
                type = TypeId,
                companyId = TargetCompany.definition.companyID,
                deadlineTurn = DeadlineTurn,
                fields = new List<NameValue>()
            };
            foreach (DocumentChoiceField field in choiceFields)
                if (field.value.HasValue) saved.fields.Add(new NameValue { name = field.name, value = field.value.Value });
            return saved;
        }

        private float totalSpend()
        {
            return marketingCost() + facilityCost();
        }

        private float hiringChange(float employees)
        {
            int? key = findField(hiringName).value;
            if (key == middle) return Mathf.Max(1f, Mathf.Round(employees * settings.hireSmallRatio));
            if (key == high) return Mathf.Max(1f, Mathf.Round(employees * settings.hireLargeRatio));
            return 0f;
        }

        private float marketingCost()
        {
            int? key = findField(marketingName).value;
            if (key == middle) return settings.marketingNormalCost;
            if (key == high) return settings.marketingFocusCost;
            return 0f;
        }

        // 평판이 높을수록 덜 오른다: 실제 상승량 = 기본 상승량 × (100 − 지금 평판) ÷ 100
        private float marketingGain(float reputation)
        {
            int? key = findField(marketingName).value;
            float baseGain = key == middle ? settings.marketingNormalGain
                : key == high ? settings.marketingFocusGain : 0f;
            return baseGain * (100f - Mathf.Clamp(reputation, 0f, 100f)) / 100f;
        }

        private float stanceEmployeeChange(float employees)
        {
            int? key = findField(stanceName).value;
            if (key == none) return Mathf.Round(employees * settings.aggressiveEmployeeRatio);
            if (key == high) return Mathf.Round(employees * settings.conservativeEmployeeRatio);
            return 0f;
        }

        private float stanceReputationChange()
        {
            int? key = findField(stanceName).value;
            if (key == none) return settings.aggressiveReputation;
            if (key == high) return settings.conservativeReputation;
            return 0f;
        }

        private float facilityCost()
        {
            int? key = findField(facilityName).value;
            if (key == middle) return settings.facilityMediumCost;
            if (key == high) return settings.facilityLargeCost;
            return 0f;
        }

        // 보너스 = 투자금 ÷ 100 × 100원당 보너스. 지연 기간 뒤 시작해 정해진 기간 동안 성장률에 더해진다.
        private void recordFacility(float facility)
        {
            if (record == null) return;
            int turn = clock != null ? clock.CurrentTurn : 0;
            int start = turn + settings.facilityBonusDelayTurns;
            record.facilityInvestments.Add(new StartupFacilityInvestment
            {
                startTurn = start,
                endTurn = start + settings.facilityBonusDurationTurns,
                growthBonus = facility / 100f * settings.facilityGrowthPer100
            });
        }

        private DocumentChoiceField findField(string name)
        {
            foreach (DocumentChoiceField field in choiceFields)
                if (field.name == name) return field;
            return null;
        }
    }
}
