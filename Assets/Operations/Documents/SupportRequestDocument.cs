using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Employees;

namespace TakeOver.Documents
{
    public sealed class SupportRequestDocument : IApprovalDocument
    {
        public const string typeId = "startup-support-request";
        private readonly EmployeeService employees;
        private readonly JobService jobs;
        private readonly Companies companies;
        private readonly DocumentChoiceField employee;
        private readonly List<DocumentInfo> infoRows;
        private string employeeLabel;
        private bool isResolved;

        public string TypeId => typeId;
        public string Title => "지원 인력 파견 요청";
        public string Author { get; }
        public string BodyId => "generic";
        public string DedupKey { get; }
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string ApproveLabel => "승인";
        public string RejectLabel => "거절";
        public string DoneLabel => isCancelled ? "취소 완료" : "";
        public bool isCancelled { get; private set; }
        public IReadOnlyList<string> BodyLines => Array.Empty<string>();
        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields { get; }
        public IReadOnlyList<DocumentInfo> InfoRows => isCancelled
            ? new List<DocumentInfo>(infoRows) { new DocumentInfo("취소 사유", "회사 상장 또는 폐업") }
            : infoRows;

        public SupportRequestDocument(Company company, int deadlineTurn, EmployeeService employees, JobService jobs, Companies companies)
        {
            TargetCompany = company;
            DeadlineTurn = deadlineTurn;
            Author = $"{company.definition.companyName} 대표";
            DedupKey = company.definition.companyID;
            this.employees = employees;
            this.jobs = jobs;
            this.companies = companies;
            infoRows = new List<DocumentInfo>
            {
                new DocumentInfo("대상 회사", company.definition.companyName),
                new DocumentInfo("매출 성장", $"{company.state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH):P1}"),
                new DocumentInfo("순이익", MoneyText.Format(company.state.getDerivedStat(CompanyDerivedStat.NET_PROFIT)))
            };
            bool declining = company.state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH) < 0f;
            bool loss = company.state.getDerivedStat(CompanyDerivedStat.NET_PROFIT) < 0f;
            if (declining || loss)
                infoRows.Insert(1, new DocumentInfo("요청 사유", declining ? loss ? "매출 감소 · 적자" : "매출 감소" : "적자"));
            employee = new DocumentChoiceField("employee", "직원", getEmployeeChoices);
            ChoiceFields = new[] { employee };
        }

        public SupportRequestDocument(DocumentSaveRecord record, Companies companies, EmployeeService employees, JobService jobs)
        {
            TargetCompany = companies.getCompany(record.companyId);
            DeadlineTurn = record.deadlineTurn;
            Author = record.author;
            DedupKey = record.companyId;
            this.employees = employees;
            this.jobs = jobs;
            this.companies = companies;
            infoRows = new List<DocumentInfo>();
            foreach (NameText row in record.infoRows)
            {
                if (row.label == "employee-label") employeeLabel = row.value;
                else if (row.label == "대상 회사" || row.label == "요청 사유" ||
                    row.label == "매출 성장" || row.label == "순이익")
                    infoRows.Add(new DocumentInfo(row.label, row.value));
            }
            isResolved = record.resolved;
            employee = new DocumentChoiceField("employee", "직원", getEmployeeChoices);
            ChoiceFields = new[] { employee };
        }

        private IReadOnlyList<DocumentChoice> getEmployeeChoices()
        {
            if (isResolved && employee.value.HasValue && employeeLabel != null)
                return new[] { new DocumentChoice { key = employee.value.Value, label = employeeLabel } };
            var choices = new List<DocumentChoice>();
            foreach (Employee candidate in employees.Employees)
                if (jobs.GetJob(candidate) != JobType.Support)
                    choices.Add(new DocumentChoice { key = candidate.Id, label = $"{candidate.Name} · 관리 {candidate.Management.Current}" });
            return choices;
        }

        public void SetField(string name, float value)
        {
            if (name != "cancelled") throw new ArgumentException("알 수 없는 문서 기록입니다.", nameof(name));
            isCancelled = value != 0f;
        }

        public void SetChoice(string name, int key)
        {
            if (name != employee.name) throw new ArgumentException("알 수 없는 직원 선택 칸입니다.", nameof(name));
            employee.value = key;
            if (!isResolved || employeeLabel == null)
            {
                employeeLabel = null;
                foreach (DocumentChoice choice in getEmployeeChoices())
                    if (choice.key == key) { employeeLabel = choice.label; break; }
            }
        }

        public bool CanApprove(out string reason)
        {
            bool registered = false;
            foreach (Company startup in companies.StartupList)
                if (startup == TargetCompany) { registered = true; break; }
            if (!registered || isCancelled)
            {
                reason = "요청 대상 스타트업이 없습니다";
                return false;
            }
            if (!employee.value.HasValue)
            {
                reason = "직원을 선택해 주세요";
                return false;
            }
            foreach (Employee candidate in employees.Employees)
            {
                if (candidate.Id != employee.value.Value) continue;
                reason = jobs.GetJob(candidate) == JobType.Support ? "이미 배치된 직원입니다" : "";
                return reason.Length == 0;
            }
            reason = "선택한 직원이 현재 재직 중이 아닙니다";
            return false;
        }

        public RuleResult OnApprove()
        {
            if (!CanApprove(out string reason)) throw new InvalidOperationException(reason);
            var selected = System.Linq.Enumerable.Single(employees.Employees, candidate => candidate.Id == employee.value.Value);
            if (!jobs.AssignSupport(selected, TargetCompany)) throw new InvalidOperationException("직원을 배치하지 못했습니다.");
            rememberEmployee();
            return new RuleResult();
        }

        // 지원 인원 배치 담당자가 연결할 함수
        public void assignEmployee(Company company, int employeeId)
        {
            var selected = System.Linq.Enumerable.FirstOrDefault(employees.Employees, candidate => candidate.Id == employeeId);
            if (!jobs.AssignSupport(selected, company)) throw new InvalidOperationException("직원을 배치하지 못했습니다.");
        }

        public void cancel() => isCancelled = true;

        public RuleResult OnReject()
        {
            rememberEmployee();
            return new RuleResult();
        }

        private void rememberEmployee()
        {
            foreach (DocumentChoice choice in getEmployeeChoices())
                if (choice.key == employee.value) { employeeLabel = choice.label; break; }
            isResolved = true;
        }

        public DocumentSaveRecord ToSaveRecord()
        {
            var record = new DocumentSaveRecord
            {
                type = typeId,
                companyId = DedupKey,
                deadlineTurn = DeadlineTurn,
                resolved = isResolved,
                author = Author,
                fields = new List<NameValue>(),
                infoRows = new List<NameText>()
            };
            if (employee.value.HasValue)
                record.fields.Add(new NameValue { name = employee.name, value = employee.value.Value });
            if (isCancelled) record.fields.Add(new NameValue { name = "cancelled", value = 1f });
            foreach (DocumentInfo row in infoRows) record.infoRows.Add(new NameText { label = row.label, value = row.value });
            if (employeeLabel != null) record.infoRows.Add(new NameText { label = "employee-label", value = employeeLabel });
            return record;
        }
    }
}
