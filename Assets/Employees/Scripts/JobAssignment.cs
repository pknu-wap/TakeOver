using System.Collections.Generic;

namespace TakeOver.Employees
{
    public sealed class JobAssignment
    {
        public int EmployeeId { get; }
        public JobType Job { get; }
        public int RemainingDays { get; internal set; }
        // 시세 예측용 대상 회사 목록. 비어 있을 수 있다.
        public IReadOnlyList<Company> Targets { get; }

        public JobAssignment(int employeeId, JobType job, int remainingDays, IReadOnlyList<Company> targets)
        {
            EmployeeId = employeeId;
            Job = job;
            RemainingDays = remainingDays;
            Targets = targets != null ? new List<Company>(targets) : new List<Company>();
        }
    }

    // 업무가 한 번 끝날 때 OnJobCompleted로 전달되는 내용.
    public sealed class JobCompletion
    {
        public Employee Employee { get; }
        public JobType Job { get; }
        public IReadOnlyList<Company> Targets { get; }

        public JobCompletion(Employee employee, JobType job, IReadOnlyList<Company> targets)
        {
            Employee = employee;
            Job = job;
            Targets = targets;
        }
    }
}
