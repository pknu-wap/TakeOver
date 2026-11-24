using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.CompanyRules;
using TakeOver.Employees;
using TakeOver.NPC;

namespace TakeOver.Documents
{
    public sealed class EmployeeRenewalDocument : IApprovalDocument
    {
        private enum Outcome { Pending, Approved, Declined, Expired, Cancelled }

        [Serializable]
        public sealed class SavedData
        {
            public int employeeId;
            public string employeeName;
            public int salary;
            public int proposedSalary;
            public EmployeeStatSnapshot startStats;
            public EmployeeStatSnapshot comparisonStats;
            public int outcome;
        }

        public const string typeId = "employee-renewal";
        private const string StatComparisonLabel = "스탯 비교";
        private readonly EmployeeService employees;
        private readonly GameTurnClock clock;
        private readonly SavedData data;
        private Employee employee;
        private Outcome outcome => (Outcome)data.outcome;

        public string TypeId => typeId;
        public string Title => $"직원 재계약 · {data.employeeName}";
        public string Author => "인사 담당";
        public string BodyId => "generic";
        public string DedupKey => $"{data.employeeId}:{DeadlineTurn}";
        public string DocumentNumber => $"{data.employeeId:D4}-{DeadlineTurn:D4}";
        public Company TargetCompany => null;
        public int DeadlineTurn { get; }
        public string ApproveLabel => "승인";
        public string RejectLabel => "거절";
        public string DoneLabel => outcome switch
        {
            Outcome.Expired => "자동 거절",
            Outcome.Cancelled => "취소",
            _ => ""
        };
        public bool WasAutomaticallyRejected => outcome == Outcome.Expired;
        public IReadOnlyList<string> BodyLines => Array.Empty<string>();
        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("직원 이름", data.employeeName),
            new DocumentInfo("계약 만료", $"{DeadlineTurn}일차"),
            new DocumentInfo("현재 월급", $"{data.salary:N0}만원"),
            new DocumentInfo("제안 월급", $"{data.proposedSalary:N0}만원"),
            new DocumentInfo(StatComparisonLabel, "세부 보기"),
            new DocumentInfo("계약 안내", outcome switch
            {
                Outcome.Approved => "제안 월급으로 계약 연장",
                Outcome.Declined => "기존 만료일에 퇴사",
                Outcome.Expired => "미결 상태로 만료되어 퇴사",
                Outcome.Cancelled => "직원 퇴사로 문서 취소",
                _ => "미결 상태로 만료 시 퇴사"
            })
        };

        public EmployeeRenewalDocument(Employee employee, EmployeeService employees, GameTurnClock clock)
            : this(employee, employees, clock, employee.ContractEndTurn, new SavedData
            {
                employeeId = employee.Id,
                employeeName = employee.Name,
                salary = employee.MonthlySalary,
                proposedSalary = employees.GetRenewalSalary(employee),
                startStats = employee.ContractStartStats
            })
        {
        }

        private EmployeeRenewalDocument(Employee employee, EmployeeService employees, GameTurnClock clock,
            int deadlineTurn, SavedData data)
        {
            this.employee = employee;
            this.employees = employees;
            this.clock = clock;
            this.data = data;
            DeadlineTurn = deadlineTurn;
        }

        internal static EmployeeRenewalDocument Restore(DocumentSaveRecord record, EmployeeService employees, GameTurnClock clock)
        {
            SavedData data = record.employeeRenewal;
            if (data == null || data.employeeId <= 0 || string.IsNullOrWhiteSpace(data.employeeName) ||
                data.outcome < (int)Outcome.Pending || data.outcome > (int)Outcome.Cancelled ||
                record.resolved != (data.outcome != (int)Outcome.Pending) ||
                record.approved != (data.outcome == (int)Outcome.Approved))
                throw new InvalidDataException("직원 재계약 문서 기록이 올바르지 않습니다.");
            if (record.resolved)
                return new EmployeeRenewalDocument(null, employees, clock, record.deadlineTurn, data);

            // 직원 저장이 연결되기 전에는 세이브에 남은 미결 문서에 대응하는 직원이 없을 수 있다.
            Employee employee = employees?.Employees.FirstOrDefault(candidate => candidate.Id == data.employeeId &&
                candidate.ContractEndTurn == record.deadlineTurn && !candidate.IsPendingLeave);
            return employee == null ? null : new EmployeeRenewalDocument(employee, employees, clock, record.deadlineTurn, data);
        }

        internal bool IsFor(Employee candidate) => employee == candidate && DeadlineTurn == candidate.ContractEndTurn;

        public bool CanApprove(out string reason)
        {
            if (outcome != Outcome.Pending || !employees.Employees.Contains(employee) || !IsFor(employee) || employee.IsPendingLeave)
            {
                reason = "재계약할 직원이 현재 재직 중이 아닙니다.";
                return false;
            }

            reason = clock.CurrentTurn >= DeadlineTurn ? "계약 만료일이 지났습니다." : "";
            return reason.Length == 0;
        }

        public RuleResult OnApprove()
        {
            data.comparisonStats = CurrentStats(employee);
            employee.PendingRenewalSalary = data.proposedSalary;
            employees.Renew(employee);
            finish(Outcome.Approved);
            return new RuleResult();
        }

        public RuleResult OnReject()
        {
            data.comparisonStats = CurrentStats(employee);
            if (!employees.Employees.Contains(employee) || !IsFor(employee))
                finish(Outcome.Cancelled);
            else
            {
                employees.Decline(employee);
                finish(clock.CurrentTurn >= DeadlineTurn ? Outcome.Expired : Outcome.Declined);
            }
            return new RuleResult();
        }

        public bool IsStatComparisonRow(DocumentInfo row) => row.label == StatComparisonLabel;

        public string StatComparison()
        {
            EmployeeStatSnapshot start = data.startStats;
            EmployeeStatSnapshot current = outcome == Outcome.Pending ? CurrentStats(employee) : data.comparisonStats;
            return string.Join("\n",
                format("분석력", start.Analysis, current.Analysis),
                format("인맥", start.Network, current.Network),
                format("안목", start.Eye, current.Eye),
                format("발품", start.Legwork, current.Legwork),
                format("경영 역량", start.Management, current.Management));
        }

        private static string format(string label, int start, int current) =>
            $"{label}  {start} → {current} ({current - start:+0;-0;0})";

        private static EmployeeStatSnapshot CurrentStats(Employee employee) => new EmployeeStatSnapshot(
            employee.Analysis.Current, employee.Network.Current, employee.Eye.Current,
            employee.Legwork.Current, employee.Management.Current);

        private void finish(Outcome result)
        {
            data.outcome = (int)result;
            employee = null;
        }

        public void SetField(string name, float value) { }
        public void SetChoice(string name, int key) { }

        public DocumentSaveRecord ToSaveRecord() => new DocumentSaveRecord
        {
            type = typeId,
            deadlineTurn = DeadlineTurn,
            fields = new List<NameValue>(),
            employeeRenewal = data
        };
    }
}
