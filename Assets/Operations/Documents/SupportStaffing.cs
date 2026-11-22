using System.Collections.Generic;
using TakeOver.Employees;

namespace TakeOver.Documents
{
    public static class SupportStaffing
    {
        public const int noEmployeeKey = 0;

        public static List<DocumentChoice> getChoices(EmployeeService employees, JobService jobs, bool includeNone)
        {
            var choices = new List<DocumentChoice>();
            if (includeNone) choices.Add(new DocumentChoice { key = noEmployeeKey, label = "배치하지 않음" });
            if (employees == null || jobs == null) return choices;
            foreach (Employee candidate in employees.Employees)
                if (jobs.GetJob(candidate) != JobType.Support)
                    choices.Add(new DocumentChoice { key = candidate.Id, label = $"{candidate.Name} · 관리 {candidate.Management.Current}" });
            return choices;
        }

        public static bool canAssign(int employeeId, EmployeeService employees, JobService jobs, out string reason)
        {
            Employee employee = find(employeeId, employees);
            if (employee == null || jobs == null)
            {
                reason = "선택한 직원이 현재 재직 중이 아닙니다";
                return false;
            }
            reason = jobs.GetJob(employee) == JobType.Support ? "이미 배치된 직원입니다" : "";
            return reason.Length == 0;
        }

        public static bool assign(Company company, int employeeId, EmployeeService employees, JobService jobs)
        {
            Employee employee = find(employeeId, employees);
            if (company == null || employee == null || jobs == null) return false;
            return jobs.Assign(employee, JobType.Support, new[] { company });
        }

        private static Employee find(int employeeId, EmployeeService employees)
        {
            if (employees == null) return null;
            foreach (Employee candidate in employees.Employees)
                if (candidate.Id == employeeId) return candidate;
            return null;
        }
    }
}
