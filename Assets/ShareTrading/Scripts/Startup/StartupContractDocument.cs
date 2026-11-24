using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Documents;
using TakeOver.Employees;

namespace TakeOver.Startups
{
    public sealed class StartupContractDocument : IApprovalDocument
    {
        private static readonly float[] stakeOptions = { .2f, .3f, .4f };
        private readonly Player player;
        private readonly Companies companies;
        private readonly float companyValue;
        private readonly DocumentField stake;
        private readonly EmployeeService employees;
        private readonly JobService jobs;
        private readonly DocumentChoiceField staff;
        private const int noStaffKey = 0;

        public string TypeId => "startup-investment";
        public string Title => "투자 계약 체결";
        public string Author => $"{TargetCompany.definition.companyName} 대표";
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string ApproveLabel => "확정";
        public string RejectLabel => "거절";
        public string DoneLabel => "계약 완료";
        public IReadOnlyList<string> BodyLines => Array.Empty<string>();
        public IReadOnlyList<DocumentField> Fields { get; }
        public IReadOnlyList<DocumentChoiceField> ChoiceFields { get; }
        public float investment => calculateInvestment(companyValue, stake.value);
        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("기업가치", $"{companyValue:N0}"),
            new DocumentInfo("투자금", $"{investment:N0}"),
            new DocumentInfo("내 보유자금", $"{player.state.cash:N0} → {player.state.cash - investment:N0}")
        };

        public Employee SelectedStaff => staff.value.HasValue ? findEmployee(staff.value.Value) : null;

        public StartupContractDocument(Company company, Player player, Companies companies, int turn,
            EmployeeService employees, JobService jobs)
        {
            TargetCompany = company;
            this.player = player;
            this.companies = companies;
            DeadlineTurn = turn;
            companyValue = company.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE);
            stake = new DocumentField("stake", "지분율", stakeOptions[0], stakeOptions);
            Fields = new[] { stake };
            this.employees = employees;
            this.jobs = jobs;
            staff = new DocumentChoiceField("staff", "직원 배치", getStaffChoices, noStaffKey);
            ChoiceFields = new[] { staff };
        }

        private IReadOnlyList<DocumentChoice> getStaffChoices()
        {
            var choices = new List<DocumentChoice> { new DocumentChoice { key = noStaffKey, label = "배치하지 않음" } };
            if (employees == null || jobs == null) return choices;
            foreach (Employee candidate in employees.Employees)
                if (jobs.GetJob(candidate) != JobType.Support)
                    choices.Add(new DocumentChoice { key = candidate.Id, label = $"{candidate.Name} · 관리 {candidate.Management.Current}" });
            return choices;
        }

        private Employee findEmployee(int id)
        {
            if (id == noStaffKey || employees == null) return null;
            foreach (Employee candidate in employees.Employees)
                if (candidate.Id == id) return candidate;
            return null;
        }

        public static float calculateInvestment(float companyValue, float stakeRatio) =>
            (float)((double)stakeRatio * companyValue / (1d - stakeRatio));

        public void SetField(string name, float value)
        {
            if (name != stake.name || Array.IndexOf(stakeOptions, value) < 0)
                throw new ArgumentException("계약서의 지분율 선택이 올바르지 않습니다.");
            stake.value = value;
        }

        public void SetChoice(string name, int key)
        {
            if (name != staff.name) throw new ArgumentException("알 수 없는 직원 선택 칸입니다.", nameof(name));
            staff.value = key;
        }

        public bool CanApprove(out string reason)
        {
            bool registered = false;
            foreach (Company startup in companies.StartupList)
                if (startup == TargetCompany) { registered = true; break; }
            if (!registered)
            {
                reason = "계약할 수 있는 스타트업이 아닙니다";
                return false;
            }
            if (TargetCompany.state.getStat(CompanyStat.PLAYER_SHARES) > 0f)
            {
                reason = "이미 투자한 스타트업입니다";
                return false;
            }
            if (player.state.cash < investment)
            {
                reason = "보유자금이 부족합니다";
                return false;
            }
            if (staff.value.HasValue && staff.value.Value != noStaffKey)
            {
                Employee selected = SelectedStaff;
                if (selected == null)
                {
                    reason = "선택한 직원이 현재 재직 중이 아닙니다";
                    return false;
                }
                if (jobs == null || jobs.GetJob(selected) == JobType.Support)
                {
                    reason = "이미 배치된 직원입니다";
                    return false;
                }
            }
            reason = "";
            return true;
        }

        public RuleResult OnApprove()
        {
            var result = new RuleResult { playerCash = -investment };
            result.stats.Add(new StatChange(CompanyStat.CASH, investment));
            result.stats.Add(new StatChange(CompanyStat.PLAYER_SHARES, TargetCompany.state.totalShares * stake.value));
            return result;
        }

        public RuleResult OnReject() => new RuleResult();

        public DocumentSaveRecord ToSaveRecord() => new DocumentSaveRecord
        {
            type = TypeId,
            companyId = TargetCompany.definition.companyID,
            fields = new List<NameValue> { new NameValue { name = stake.name, value = stake.value } }
        };
    }
}
