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
    public sealed class StartupBusinessPlanDocument : IApprovalDocument, IDocumentPages
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
        private IReadOnlyList<DocumentInfo>[] resolvedInfoPages;

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "연간 사업 계획 승인";
        public string Author => $"{TargetCompany.definition.companyName} 대표";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";

        // 처리한 순간의 현금·지출 표시. 처리 뒤 기록에서 다시 열어도 돈을 쓴 뒤 숫자로 바뀌지 않게 고정한다.
        public int PageCount => 4;
        public IReadOnlyList<DocumentInfo> InfoRows => GetInfoRows(0);

        public IReadOnlyList<DocumentInfo> GetInfoRows(int pageIndex) =>
            resolvedInfoPages != null ? resolvedInfoPages[pageIndex] : buildInfoRows(pageIndex);

        public IReadOnlyList<DocumentField> GetFields(int pageIndex) => Array.Empty<DocumentField>();

        public IReadOnlyList<DocumentChoiceField> GetChoiceFields(int pageIndex) =>
            pageIndex >= 0 && pageIndex < choiceFields.Count ? new[] { choiceFields[pageIndex] } : Array.Empty<DocumentChoiceField>();

        private IReadOnlyList<DocumentInfo> buildInfoRows(int pageIndex)
        {
            float cash = TargetCompany.state.getBaseStat(CompanyStat.CASH);
            float spend = totalSpend();
            List<DocumentInfo> rows = new List<DocumentInfo>
            {
                new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
                new DocumentInfo("회사 현금", MoneyText.Format(cash)),
                new DocumentInfo("총 예상 지출", MoneyText.Format(spend)),
                previewRow(pageIndex)
            };
            if (pageIndex == 3 && spend > cash)
                rows.Add(new DocumentInfo("", $"부족분 {MoneyText.Format(spend - cash)} (대출)", true));
            return rows;
        }

        private DocumentInfo previewRow(int pageIndex)
        {
            float employees = TargetCompany.state.getBaseStat(CompanyStat.EMPLOYEE_COUNT);
            float reputation = TargetCompany.state.getBaseStat(CompanyStat.REPUTATION_B2C);
            switch (pageIndex)
            {
                case 0:
                    return new DocumentInfo("직원 수", findField(hiringName).value.HasValue
                        ? $"{employees:N0}명 → {Mathf.Max(1f, employees + hiringChange(employees)):N0}명" : "-");
                case 1:
                    return new DocumentInfo("평판", findField(marketingName).value.HasValue
                        ? $"{reputation:0.#} → {reputationAfterMarketing(reputation):0.#}" : "-");
                case 2:
                    return new DocumentInfo("직원 수", findField(stanceName).value.HasValue
                        ? $"{employees:N0}명 → {employeeCountAfterPlan(employees):N0}명" : "-");
                case 3:
                    return new DocumentInfo("예상 효과", findField(facilityName).value.HasValue
                        ? facilityEffectLabel() : "-");
                default:
                    return new DocumentInfo("결과 미리보기", "-");
            }
        }

        private void cacheResolvedInfoPages()
        {
            resolvedInfoPages = new IReadOnlyList<DocumentInfo>[PageCount];
            for (int page = 0; page < PageCount; page++) resolvedInfoPages[page] = buildInfoRows(page);
        }

        public IReadOnlyList<string> BodyLines => new[]
        {
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
                    new DocumentChoice { key = middle, label = $"보통 ({MoneyText.Format(settings.marketingNormalCost)})" },
                    new DocumentChoice { key = high, label = $"집중 ({MoneyText.Format(settings.marketingFocusCost)})" }
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
                    new DocumentChoice { key = middle, label = $"중간 ({MoneyText.Format(settings.facilityMediumCost)})" },
                    new DocumentChoice { key = high, label = $"대규모 ({MoneyText.Format(settings.facilityLargeCost)})" }
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
            if (!StartupListingSource.isStartup(companies, TargetCompany))
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
            cacheResolvedInfoPages();
            CompanyState state = TargetCompany.state;
            RuleResult result = new RuleResult();

            // 직원: 채용 규모 + 운영 기조. 최소 1명은 남긴다.
            float employees = state.getBaseStat(CompanyStat.EMPLOYEE_COUNT);
            float employeeChange = employeeCountAfterPlan(employees) - employees;
            if (employeeChange != 0f) result.stats.Add(new StatChange(CompanyStat.EMPLOYEE_COUNT, employeeChange));

            // 소비자 평판: 마케팅 + 운영 기조. 0~100 안에서 끝나게 자른다.
            float reputation = state.getBaseStat(CompanyStat.REPUTATION_B2C);
            float nextReputation = reputationAfterPlan(reputation);
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
                recordFacility(facility, facilityBonus());
            }
            return result;
        }

        public RuleResult OnReject()
        {
            cacheResolvedInfoPages();
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

        private float employeeCountAfterPlan(float employees)
        {
            float change = hiringChange(employees) + stanceEmployeeChange(employees);
            return employees + Mathf.Max(change, 1f - employees);
        }

        private float reputationAfterMarketing(float reputation) =>
            Mathf.Clamp(reputation + marketingGain(reputation), 0f, 100f);

        private float reputationAfterPlan(float reputation) =>
            Mathf.Clamp(reputation + marketingGain(reputation) + stanceReputationChange(), 0f, 100f);

        private float facilityBonus() => facilityCost() / 100f * settings.facilityGrowthPer100;

        private string facilityEffectLabel()
        {
            int? value = findField(facilityName).value;
            return value == none ? "작음" : value == middle ? "보통" : "큼";
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
        private void recordFacility(float facility, float bonus)
        {
            if (record == null) return;
            int turn = clock != null ? clock.CurrentTurn : 0;
            int start = turn + settings.facilityBonusDelayTurns;
            record.facilityInvestments.Add(new StartupFacilityInvestment
            {
                startTurn = start,
                endTurn = start + settings.facilityBonusDurationTurns,
                growthBonus = bonus
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
