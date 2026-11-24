using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    // 직원 직무 상태를 직원 ID로 보관하고, 턴마다 업무를 진행한다.
    // 시세 예측·발굴의 실제 결과는 만들지 않는다. OnJobCompleted를 구독해서 각자 만든다.
    public sealed class JobService : MonoBehaviour
    {
        [SerializeField] private EmployeeService employeeService;
        [SerializeField] private GameTurnClock gameTurnClock;
        [SerializeField] private JobSettings settings;

        private readonly Dictionary<int, JobAssignment> assignments = new Dictionary<int, JobAssignment>();

        [Serializable]
        internal sealed class SavedJob
        {
            public int employeeId, days;
            public JobType job;
            public List<string> targets;
        }

        internal List<SavedJob> CaptureJobs()
        {
            PruneSupportAssignments();
            return assignments.Values.Select(assignment => new SavedJob
            {
                employeeId = assignment.EmployeeId, job = assignment.Job, days = assignment.RemainingDays,
                targets = assignment.Targets.Where(company => company != null).Select(company => company.definition.companyID).ToList()
            }).ToList();
        }

        private readonly List<int> endedSupport = new List<int>();
        private void LateUpdate() => PruneSupportAssignments();
        private void PruneSupportAssignments()
        {
            endedSupport.Clear();
            var companies = FindFirstObjectByType<Companies>();
            if (companies == null) return;
            foreach (var pair in assignments)
                if (pair.Value.Job == JobType.Support && (pair.Value.Targets.Count != 1 ||
                    pair.Value.Targets[0] == null || !companies.StartupList.Contains(pair.Value.Targets[0]))) endedSupport.Add(pair.Key);
            foreach (int id in endedSupport) assignments.Remove(id);
            if (endedSupport.Count > 0) employeeService?.MarkChanged();
        }

        internal void RestoreJobs(List<SavedJob> jobs)
        {
            var companies = FindFirstObjectByType<Companies>();
            assignments.Clear();
            foreach (var job in jobs)
            {
                var targets = job.targets.Select(id => companies?.getCompany(id)).Where(company => company != null).ToList();
                assignments.Add(job.employeeId, new JobAssignment(job.employeeId, job.job, job.days, targets));
            }
        }

        public JobSettings Settings => settings;
        public event Action<JobCompletion> OnJobCompleted;

        private void Awake()
        {
            if (employeeService == null) employeeService = FindFirstObjectByType<EmployeeService>();
            if (gameTurnClock == null) gameTurnClock = FindFirstObjectByType<GameTurnClock>();

            if (employeeService == null)
            {
                Debug.LogWarning("[JobService] EmployeeService 참조가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
            }

            if (gameTurnClock == null)
            {
                Debug.LogWarning("[JobService] GameTurnClock 참조가 비어 있습니다. 턴 진행이 동작하지 않습니다.", this);
            }

            if (settings == null)
            {
                Debug.LogWarning("[JobService] JobSettings 참조가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
            }
        }

        private void OnEnable()
        {
            if (gameTurnClock != null) gameTurnClock.TurnAdvanced += HandleTurnAdvanced;
            if (employeeService != null) employeeService.OnEmployeeLeft += HandleEmployeeLeft;
        }

        private void OnDisable()
        {
            if (gameTurnClock != null) gameTurnClock.TurnAdvanced -= HandleTurnAdvanced;
            if (employeeService != null) employeeService.OnEmployeeLeft -= HandleEmployeeLeft;
        }

        // 직접 배정은 조사 직무만 받는다. 지원은 대상 회사를 지정하는 AssignSupport를 사용한다.
        // 이미 직무가 있으면 바꾸고 남은 일수를 기본 기간으로 다시 시작한다.
        public bool Assign(Employee emp, JobType job, IReadOnlyList<Company> targets = null)
        {
            if (emp == null || employeeService == null || !Contains(emp))
            {
                Debug.LogWarning("[JobService] 배정 실패: 존재하지 않는 직원입니다.");
                return false;
            }
            if (!employeeService.CanChangeRoster()) return false;

            if (job == JobType.None)
            {
                Clear(emp);
                return true;
            }

            if (job == JobType.Support)
            {
                Debug.LogWarning("[JobService] 배정 실패: 지원 인원 배치는 이 서비스에서 배정하지 않습니다.");
                return false;
            }

            if (settings == null)
            {
                Debug.LogWarning("[JobService] 배정 실패: JobSettings가 할당되지 않았습니다.");
                return false;
            }

            assignments[emp.Id] = new JobAssignment(emp.Id, job, settings.GetDuration(emp.Diligence.Value), targets);
            employeeService.MarkChanged();
            return true;
        }

        public void Clear(Employee emp)
        {
            if (emp != null && employeeService?.CanChangeRoster() != false && assignments.Remove(emp.Id))
                employeeService?.MarkChanged();
        }

        public bool AssignSupport(Employee emp, Company company)
        {
            var companies = FindFirstObjectByType<Companies>();
            if (emp == null || !Contains(emp) || !employeeService.CanChangeRoster() || company == null ||
                companies == null || !companies.StartupList.Contains(company) || GetJob(emp) == JobType.Support) return false;
            assignments[emp.Id] = new JobAssignment(emp.Id, JobType.Support, 0, new[] { company });
            employeeService.MarkChanged();
            return true;
        }
        public bool HasSupportFor(Company company) => assignments.Values.Any(job => job.Job == JobType.Support && job.Targets.Contains(company));
        public string SupportEmployeeNamesFor(Company company) => string.Join(", ", employeeService.Employees
            .Where(employee => assignments.TryGetValue(employee.Id, out var job) && job.Job == JobType.Support && job.Targets.Contains(company))
            .Select(employee => employee.Name));
        public string SupportTargetName(Employee emp) => emp != null && assignments.TryGetValue(emp.Id, out var job) &&
            job.Job == JobType.Support ? job.Targets.FirstOrDefault()?.definition.companyName ?? "" : "";

        public JobType GetJob(Employee emp)
        {
            return emp != null && assignments.TryGetValue(emp.Id, out JobAssignment assignment)
                ? assignment.Job : JobType.None;
        }

        public int GetRemainingDays(Employee emp)
        {
            return emp != null && assignments.TryGetValue(emp.Id, out JobAssignment assignment)
                ? assignment.RemainingDays : 0;
        }

        private void HandleTurnAdvanced(int turn)
        {
            foreach (int id in new List<int>(assignments.Keys))
            {
                if (!assignments.TryGetValue(id, out JobAssignment assignment)) continue;
                Employee emp = Find(id);
                if (emp == null)
                {
                    assignments.Remove(id);
                    continue;
                }

                if (assignment.Job == JobType.Support)
                {
                    var company = assignment.Targets.FirstOrDefault();
                    if (company == null || !FindFirstObjectByType<Companies>().StartupList.Contains(company)) Clear(emp);
                    continue;
                }

                assignment.RemainingDays--;
                if (assignment.RemainingDays > 0) continue;

                ApplyGrowth(emp, assignment.Job);
                OnJobCompleted?.Invoke(new JobCompletion(emp, assignment.Job, assignment.Targets));
                // 이벤트 구독자가 직무를 바꾸거나 해제했으면 그대로 둔다.
                if (assignments.TryGetValue(id, out JobAssignment current) && current == assignment)
                {
                    assignment.RemainingDays = settings != null ? settings.GetDuration(emp.Diligence.Value) : 1;
                }
            }
        }

        private void ApplyGrowth(Employee emp, JobType job)
        {
            if (settings == null || employeeService == null) return;
            float rate = employeeService.GetGrowthRate(emp);
            foreach (JobStatGain gain in settings.statGains)
            {
                if (gain.job != job) continue;
                employeeService.RaiseStat(emp, gain.stat, settings.GetGainAmount(gain) * rate);
            }
        }

        private void HandleEmployeeLeft(Employee emp)
        {
            Clear(emp);
        }

        private bool Contains(Employee emp)
        {
            return Find(emp.Id) == emp;
        }

        private Employee Find(int id)
        {
            if (employeeService == null) return null;
            foreach (Employee emp in employeeService.Employees)
            {
                if (emp.Id == id) return emp;
            }
            return null;
        }
    }
}
