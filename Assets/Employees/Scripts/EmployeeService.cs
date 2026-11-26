using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using TakeOver.SaveSystem;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    public class EmployeeService : MonoBehaviour, ISaveParticipant, ILegacySaveSectionInitializer, ISaveRebuildOrder
    {
        [SerializeField] private EmployeeConfig config;
        [SerializeField] private GameTurnClock gameTurnClock;
        [SerializeField] private ActionPointService actionPointService;

        [SerializeField] private int companyScaleStage = 1;
        [SerializeField] private int capacity = 5;

        private readonly List<Employee> employees = new List<Employee>();
        private readonly List<Employee> applicants = new List<Employee>();

        // 직원 정수 ID용 다음 번호. 재사용하지 않는다.
        private int nextEmployeeId = 1;

        private bool isRecruiting;
        private int recruitRemainingTurns;

        public EmployeeConfig Config => config;
        public IReadOnlyList<Employee> Employees => employees;
        public IReadOnlyList<Employee> Applicants => applicants;
        public bool IsRecruiting => isRecruiting;
        public int RecruitRemainingTurns => recruitRemainingTurns;

        public int Capacity
        {
            get => capacity;
            set => capacity = Mathf.Max(0, value);
        }

        public int CompanyScaleStage
        {
            get => companyScaleStage;
            set => companyScaleStage = Mathf.Max(1, value);
        }

        public event Action OnApplicantsArrived;
        public event Action<Employee, HiddenStatType> OnStatRevealed;
        public event Action<Employee> OnGradeChanged;
        public event Action<Employee> OnContractRenewalNotice;
        public event Action<Employee> OnContractExpired;
        public event Action<Employee> OnEmployeeLeft;
        public event Action Changed;

        [Serializable]
        private sealed class SavedEmployees
        {
            public List<Employee> employees = new List<Employee>();
            public List<Employee> applicants = new List<Employee>();
            public List<JobService.SavedJob> jobs = new List<JobService.SavedJob>();
            public int nextId = 1, recruitTurns;
        }

        private SavedEmployees prepared;
        public string saveId => "employees";
        public int saveVersion => 1;
        public int rebuildOrder => 60;

        public string captureJson() => JsonUtility.ToJson(new SavedEmployees
        {
            employees = employees, applicants = applicants, nextId = nextEmployeeId,
            recruitTurns = recruitRemainingTurns,
            jobs = FindFirstObjectByType<JobService>()?.CaptureJobs() ?? new List<JobService.SavedJob>()
        });

        public void prepareLoad(string json, int version, DayStartData day)
        {
            prepared = null;
            var data = JsonUtility.FromJson<SavedEmployees>(json);
            if (version != saveVersion || data?.employees == null || data.applicants == null || data.jobs == null ||
                data.nextId < 1 || data.recruitTurns < 0)
                throw new InvalidDataException("직원 저장 형식이 올바르지 않습니다.");
            var ids = new HashSet<int>();
            foreach (var employee in data.employees.Concat(data.applicants))
                if (employee == null || employee.Id < 1 || employee.Id >= data.nextId || !ids.Add(employee.Id))
                    throw new InvalidDataException("직원 저장의 ID가 올바르지 않습니다.");
            var employed = new HashSet<int>(data.employees.Select(employee => employee.Id));
            ids.Clear();
            foreach (var job in data.jobs)
                if (job == null || !employed.Contains(job.employeeId) || !ids.Add(job.employeeId) ||
                    (job.job != JobType.Appraisal && job.job != JobType.Discovery && job.job != JobType.Support) ||
                    (job.job == JobType.Support ? job.days != 0 || job.targets == null || job.targets.Count != 1 : job.days < 1) || job.targets == null)
                    throw new InvalidDataException("직무 저장 내용이 올바르지 않습니다.");
            prepared = data;
        }

        public void applyLoad()
        {
            employees.Clear();
            employees.AddRange(prepared.employees);
            applicants.Clear();
            applicants.AddRange(prepared.applicants);
            nextEmployeeId = prepared.nextId;
            recruitRemainingTurns = prepared.recruitTurns;
            isRecruiting = recruitRemainingTurns > 0;
        }

        public void rebuildAfterLoad()
        {
            FindFirstObjectByType<JobService>()?.RestoreJobs(prepared.jobs);
            prepared = null;
            foreach (var view in FindObjectsByType<EmployeeTabView>(FindObjectsSortMode.None)) view.RefreshAll();
        }

        public bool TryCreateLegacySection(int version, out SaveSectionData section)
        {
            section = new SaveSectionData { id = saveId, version = saveVersion, json = JsonUtility.ToJson(new SavedEmployees()) };
            return version >= 2 && version <= 4;
        }

        internal void MarkChanged() => Changed?.Invoke();

        internal bool CanChangeRoster()
        {
            if (GameSessionCoordinator.Active?.IsPersistenceBlocked != true) return true;
            FindFirstObjectByType<FloatingMessage>()?.Show("저장 재시도 후 처리해 주세요.");
            return false;
        }

        private void OnEnable()
        {
            if (config == null)
            {
                Debug.LogWarning("[EmployeeService] EmployeeConfig 참조가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
            }

            if (actionPointService == null)
            {
                Debug.LogWarning("[EmployeeService] ActionPointService 참조가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
            }

            if (gameTurnClock == null)
            {
                Debug.LogWarning("[EmployeeService] GameTurnClock 참조가 비어 있습니다. 턴 진행 및 계약 만료가 동작하지 않습니다.", this);
                return;
            }

            gameTurnClock.TurnAdvanced += HandleTurnAdvanced;
        }

        private void OnDisable()
        {
            if (gameTurnClock != null)
            {
                gameTurnClock.TurnAdvanced -= HandleTurnAdvanced;
            }
        }

        public bool StartRecruit()
        {
            if (!CanChangeRoster()) return false;
            if (config == null)
            {
                Debug.LogWarning("[EmployeeService] 모집 실패: Config(EmployeeConfig)가 할당되지 않았습니다.", this);
                return false;
            }

            if (isRecruiting)
            {
                Debug.LogWarning($"[EmployeeService] 모집 실패: 이미 모집이 진행 중입니다. (남은 턴: {recruitRemainingTurns})", this);
                return false;
            }

            if (actionPointService == null)
            {
                Debug.LogWarning("[EmployeeService] ActionPointService 참조가 비어 있어 AP 차감 없이 모집을 진행합니다.", this);
            }
            else if (!actionPointService.TryConsume(config.recruitApCost))
            {
                Debug.LogWarning($"[EmployeeService] 모집 실패: AP가 부족합니다. (필요 AP: {config.recruitApCost})", this);
                return false;
            }

            isRecruiting = true;
            recruitRemainingTurns = Mathf.Max(1, config.recruitDurationTurns);
            MarkChanged();
            return true;
        }

        public void CloseApplicants()
        {
            if (!CanChangeRoster()) return;
            applicants.Clear();
            MarkChanged();
        }

        public bool Hire(Employee applicant)
        {
            if (applicant == null || !applicants.Contains(applicant) || !CanChangeRoster())
            {
                return false;
            }

            if (employees.Count >= capacity)
            {
                return false;
            }

            applicants.Remove(applicant);

            int nowTurn = GetCurrentTurn();
            applicant.HiredTurn = nowTurn;
            applicant.ContractEndTurn = nowTurn + (config != null ? config.contractDurationTurns : 120);
            applicant.SetPendingLeave(-1);
            applicant.HasSentRenewalNotice = false;
            applicant.PendingRenewalSalary = null;
            applicant.NextRevealTurn = nowTurn + (config != null ? config.hiddenStatRevealIntervalTurns : 10);
            applicant.CaptureContractStartStats();

            employees.Add(applicant);
            MarkChanged();
            return true;
        }

        public void RaiseStat(Employee emp, AbilityStatType statType, float delta)
        {
            if (emp == null)
            {
                return;
            }

            CareerGrade prevGrade = emp.Grade;
            emp.RaiseStat(statType, delta);

            if (emp.Grade != prevGrade)
            {
                OnGradeChanged?.Invoke(emp);
            }
        }

        public float GetGrowthRate(Employee emp)
        {
            if (emp == null)
            {
                return 1f;
            }

            return Mathf.Lerp(0.7f, 1.5f, Mathf.Clamp01(emp.Ambition.Value / 20f));
        }

        public int GetRenewalSalary(Employee emp)
        {
            if (emp == null)
            {
                return 0;
            }

            if (!emp.PendingRenewalSalary.HasValue)
            {
                emp.PendingRenewalSalary = CalculateMonthlySalary(emp);
            }

            return emp.PendingRenewalSalary.Value;
        }

        public void Renew(Employee emp)
        {
            if (emp == null || !employees.Contains(emp) || !CanChangeRoster())
            {
                return;
            }

            int duration = config != null ? config.contractDurationTurns : 120;
            emp.MonthlySalary = GetRenewalSalary(emp);
            emp.ContractEndTurn = emp.ContractEndTurn + duration;
            emp.SetPendingLeave(-1);
            emp.HasSentRenewalNotice = false;
            emp.PendingRenewalSalary = null;
            emp.CaptureContractStartStats();
            MarkChanged();
        }

        public void Decline(Employee emp)
        {
            if (emp == null || !employees.Contains(emp) || !CanChangeRoster())
            {
                return;
            }

            if (emp.IsPendingLeave)
            {
                return;
            }

            emp.SetPendingLeave(emp.ContractEndTurn);
            MarkChanged();
        }

        public void Dismiss(Employee emp)
        {
            if (emp == null || !employees.Contains(emp) || !CanChangeRoster())
            {
                return;
            }

            employees.Remove(emp);
            OnEmployeeLeft?.Invoke(emp);
            MarkChanged();
        }

        public int GetSeveranceAmount(Employee emp)
        {
            if (emp == null)
            {
                return 0;
            }

            int nowTurn = GetCurrentTurn();
            int remainingTurns = Mathf.Max(0, emp.ContractEndTurn - nowTurn);
            int remainingMonths = Mathf.CeilToInt(remainingTurns / 30f);
            return emp.MonthlySalary * remainingMonths;
        }

        public bool IsRevealed(Employee emp, HiddenStatType statType)
        {
            return emp != null && emp.IsHiddenRevealed(statType);
        }

        public int GetHiddenStat(Employee emp, HiddenStatType statType)
        {
            if (emp == null)
            {
                throw new ArgumentNullException(nameof(emp), "[EmployeeService] 직원이 null인 상태에서 히든 스탯을 조회할 수 없습니다.");
            }

            return emp.GetHiddenStat(statType);
        }

        public bool TryGetHiddenStat(Employee emp, HiddenStatType statType, out int value)
        {
            if (emp == null)
            {
                value = 0;
                return false;
            }

            return emp.TryGetHiddenStat(statType, out value);
        }

        public int CalculateMonthlySalary(Employee emp)
        {
            if (emp == null || config == null)
            {
                return 0;
            }

            int statSum = emp.Analysis.Current + emp.Network.Current + emp.Eye.Current + emp.Legwork.Current + emp.Management.Current;
            float gradeMult = config.GetGradeMultiplier(emp.Grade);
            float baseSalary = (config.baseSalary + config.statSalaryWeight * statSum) * gradeMult;

            float ambitionRatio = Mathf.Clamp01(emp.Ambition.Value / 20f);
            float minFactor = Mathf.Lerp(config.lowAmbitionMinMult, config.highAmbitionMinMult, ambitionRatio);
            float maxFactor = Mathf.Lerp(config.lowAmbitionMaxMult, config.highAmbitionMaxMult, ambitionRatio);

            float randomFactor = UnityEngine.Random.Range(minFactor, maxFactor);
            return Mathf.RoundToInt(baseSalary * randomFactor);
        }

        private void HandleTurnAdvanced(int newTurn)
        {
            if (isRecruiting)
            {
                recruitRemainingTurns--;
                if (recruitRemainingTurns <= 0)
                {
                    isRecruiting = false;
                    recruitRemainingTurns = 0;
                    GenerateApplicants();
                    OnApplicantsArrived?.Invoke();
                }
            }

            int noticeTurns = config != null ? config.renewalNoticeTurns : 30;

            for (int i = employees.Count - 1; i >= 0; i--)
            {
                Employee emp = employees[i];

                if (emp.IsPendingLeave && newTurn >= emp.LeaveTurn)
                {
                    employees.RemoveAt(i);
                    OnEmployeeLeft?.Invoke(emp);
                    continue;
                }

                if (config != null && newTurn >= emp.NextRevealTurn)
                {
                    emp.NextRevealTurn = newTurn + config.hiddenStatRevealIntervalTurns;
                    if (emp.RevealNextHiddenStat(out HiddenStatType revealedStat))
                    {
                        OnStatRevealed?.Invoke(emp, revealedStat);
                    }
                }

                int remainingTurns = emp.ContractEndTurn - newTurn;
                if (!emp.IsPendingLeave && !emp.HasSentRenewalNotice && remainingTurns <= noticeTurns && remainingTurns > 0)
                {
                    emp.HasSentRenewalNotice = true;
                    GetRenewalSalary(emp);
                    OnContractRenewalNotice?.Invoke(emp);
                }

                if (!emp.IsPendingLeave && newTurn >= emp.ContractEndTurn)
                {
                    OnContractExpired?.Invoke(emp);
                }
            }
        }

        private void GenerateApplicants()
        {
            applicants.Clear();
            if (config == null)
            {
                Debug.LogWarning("[EmployeeService] 지원자 생성 실패: EmployeeConfig 참조가 비어 있습니다.", this);
                return;
            }

            int count = config.GetApplicantCountForStage(companyScaleStage);
            for (int i = 0; i < count; i++)
            {
                applicants.Add(CreateRandomApplicant());
            }
        }

        private Employee CreateRandomApplicant()
        {
            string[] lastNames = { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임" };
            string[] maleNames = { "민수", "지훈", "도윤", "현우", "준호" };
            string[] femaleNames = { "서연", "하은", "수빈", "지민", "예진" };
            var appearance = UnityEngine.Random.Range(0, 2) == 0 ? NpcMobAppearance.Male : NpcMobAppearance.Female;
            string[] firstNames = appearance == NpcMobAppearance.Female ? femaleNames : maleNames;
            string randomName = lastNames[UnityEngine.Random.Range(0, lastNames.Length)]
                + firstNames[UnityEngine.Random.Range(0, firstNames.Length)];

            int maxStatCap = config.GetMaxStatCapForStage(companyScaleStage);
            int minStat = Mathf.Clamp(1, 0, maxStatCap);

            int aMax = UnityEngine.Random.Range(minStat, maxStatCap + 1);
            int nMax = UnityEngine.Random.Range(minStat, maxStatCap + 1);
            int eMax = UnityEngine.Random.Range(minStat, maxStatCap + 1);
            int lMax = UnityEngine.Random.Range(minStat, maxStatCap + 1);
            int mMax = UnityEngine.Random.Range(minStat, maxStatCap + 1);

            int aCur = UnityEngine.Random.Range(0, aMax + 1);
            int nCur = UnityEngine.Random.Range(0, nMax + 1);
            int eCur = UnityEngine.Random.Range(0, eMax + 1);
            int lCur = UnityEngine.Random.Range(0, lMax + 1);
            int mCur = UnityEngine.Random.Range(0, mMax + 1);

            int diligence = UnityEngine.Random.Range(0, 21);
            int loyalty = UnityEngine.Random.Range(0, 21);
            int ambition = UnityEngine.Random.Range(0, 21);

            Employee candidate = new Employee(
                randomName,
                aCur, aMax,
                nCur, nMax,
                eCur, eMax,
                lCur, lMax,
                mCur, mMax,
                diligence, loyalty, ambition,
                0, 0, config.contractDurationTurns,
                config);

            candidate.Appearance = appearance;
            candidate.Id = nextEmployeeId++;
            candidate.MonthlySalary = CalculateMonthlySalary(candidate);
            return candidate;
        }

        private int GetCurrentTurn()
        {
            if (gameTurnClock == null)
            {
                Debug.LogWarning("[EmployeeService] GameTurnClock 참조가 비어 있어 현재 턴을 0으로 반환합니다.", this);
                return 0;
            }

            return gameTurnClock.CurrentTurn;
        }
    }
}
