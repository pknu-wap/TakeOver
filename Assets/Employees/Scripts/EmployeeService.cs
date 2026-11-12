using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    public class EmployeeService : MonoBehaviour
    {
        [SerializeField] private EmployeeConfig config;
        [SerializeField] private GameTurnClock gameTurnClock;
        [SerializeField] private ActionPointService actionPointService;

        [SerializeField] private int companyScaleStage = 1;
        [SerializeField] private int capacity = 5;

        [SerializeField] private List<Employee> employees = new List<Employee>();
        [SerializeField] private List<Employee> applicants = new List<Employee>();
        [SerializeField] private bool isRecruiting;
        [SerializeField] private int recruitArrivalTurn;
        [SerializeField] private int currentTurn;

        private static readonly string[] LastNames = { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임", "한", "오", "서", "신", "권", "황", "안", "송", "전", "홍" };
        private static readonly string[] FirstNames = { "민준", "서준", "도윤", "예준", "시우", "하준", "지호", "주원", "지후", "준우", "서연", "서윤", "지우", "서현", "민서", "하은", "하윤", "윤서", "지민", "채원" };

        public EmployeeConfig Config => config;
        public IReadOnlyList<Employee> Employees => employees;
        public IReadOnlyList<Employee> Applicants => applicants;
        public bool IsRecruiting => isRecruiting;

        public int CompanyScaleStage
        {
            get => companyScaleStage;
            set => companyScaleStage = Mathf.Clamp(value, 1, 3);
        }

        public int Capacity
        {
            get => capacity;
            set => capacity = Mathf.Max(0, value);
        }

        public event Action OnApplicantsArrived;
        public event Action<Employee, HiddenStatType> OnStatRevealed;
        public event Action<Employee> OnGradeChanged;
        public event Action<Employee> OnContractExpired;
        public event Action<Employee> OnEmployeeLeft;

        private void OnEnable()
        {
            if (gameTurnClock != null)
            {
                gameTurnClock.TurnAdvanced += OnTurnAdvanced;
            }
        }

        private void OnDisable()
        {
            if (gameTurnClock != null)
            {
                gameTurnClock.TurnAdvanced -= OnTurnAdvanced;
            }
        }

        private void OnTurnAdvanced(int newTurn)
        {
            currentTurn = newTurn;

            if (isRecruiting && currentTurn >= recruitArrivalTurn)
            {
                isRecruiting = false;
                GenerateApplicants();
                OnApplicantsArrived?.Invoke();
            }

            ProcessScheduledLeaves();
            ProcessContractExpirations();
            ProcessHiddenStatReveals();
        }

        protected bool TryConsumeAp(int cost)
        {
            if (actionPointService == null)
            {
                return false;
            }

            return actionPointService.TryConsume(cost);
        }

        public bool StartRecruit()
        {
            if (isRecruiting)
            {
                return false;
            }

            int cost = config != null ? config.recruitApCost : 5;
            if (!TryConsumeAp(cost))
            {
                return false;
            }

            int duration = config != null ? config.recruitDurationTurns : 2;
            isRecruiting = true;
            recruitArrivalTurn = currentTurn + duration;
            return true;
        }

        public void CloseApplicants()
        {
            applicants.Clear();
        }

        public bool Hire(Employee applicant)
        {
            if (applicant == null || !applicants.Contains(applicant))
            {
                return false;
            }

            if (employees.Count >= capacity)
            {
                return false;
            }

            int contractDuration = config != null ? config.defaultContractTurns : 120;
            applicant.HiredTurn = currentTurn;
            applicant.ContractEndTurn = currentTurn + contractDuration;
            applicant.HasNotifiedContractExpired = false;
            applicant.ScheduledLeaveTurn = -1;

            applicants.Remove(applicant);
            employees.Add(applicant);
            return true;
        }

        public bool IsRevealed(Employee employee, HiddenStatType statType)
        {
            if (employee == null)
            {
                return false;
            }

            return employee.IsHiddenStatRevealed(statType);
        }

        public int GetHiddenStat(Employee employee, HiddenStatType statType)
        {
            if (employee == null)
            {
                throw new ArgumentNullException(nameof(employee));
            }

            if (!employee.IsHiddenStatRevealed(statType))
            {
                throw new InvalidOperationException();
            }

            return employee.GetHiddenStatRawValue(statType);
        }

        public float GetGrowthRate(Employee employee)
        {
            if (employee == null)
            {
                return 1f;
            }

            int minStat = config != null ? config.minStatValue : 0;
            int maxStat = config != null ? config.maxStatValue : 20;
            float minRate = config != null ? config.minGrowthRate : 0.7f;
            float maxRate = config != null ? config.maxGrowthRate : 1.5f;

            return EmployeeMath.MapLinear(employee.Ambition.Value, minStat, maxStat, minRate, maxRate);
        }

        public void RaiseStat(Employee employee, AbilityStatType statType, float amount)
        {
            if (employee == null)
            {
                return;
            }

            AbilityStat stat = employee.GetAbilityStat(statType);
            if (stat == null)
            {
                return;
            }

            bool increased = stat.AddProgress(amount);
            if (!increased)
            {
                return;
            }

            CareerGrade newGrade = employee.CalculateGrade(config);
            if (newGrade > employee.Grade)
            {
                employee.Grade = newGrade;
                OnGradeChanged?.Invoke(employee);
            }
        }

        public int CalculateMonthlySalary(Employee employee)
        {
            if (employee == null)
            {
                return 0;
            }

            float flat = config != null ? config.baseSalaryFlat : 250f;
            float statMult = config != null ? config.salaryStatMultiplier : 2.0f;

            float gradeMult = employee.Grade switch
            {
                CareerGrade.Rookie => config != null ? config.rookieSalaryMultiplier : 1.0f,
                CareerGrade.Mid => config != null ? config.midSalaryMultiplier : 1.3f,
                _ => config != null ? config.veteranSalaryMultiplier : 1.8f
            };

            int currentStatSum = employee.Analysis.Current
                + employee.Network.Current
                + employee.Eye.Current
                + employee.Legwork.Current
                + employee.Management.Current;

            float baseSalary = (flat + statMult * currentStatSum) * gradeMult;

            int minStat = config != null ? config.minStatValue : 0;
            int maxStat = config != null ? config.maxStatValue : 20;

            float minOffset0 = config != null ? config.ambition0SalaryMinOffset : -0.10f;
            float maxOffset0 = config != null ? config.ambition0SalaryMaxOffset : 0.10f;
            float minOffset20 = config != null ? config.ambition20SalaryMinOffset : -0.05f;
            float maxOffset20 = config != null ? config.ambition20SalaryMaxOffset : 0.30f;

            float minOffset = EmployeeMath.MapLinear(employee.Ambition.Value, minStat, maxStat, minOffset0, minOffset20);
            float maxOffset = EmployeeMath.MapLinear(employee.Ambition.Value, minStat, maxStat, maxOffset0, maxOffset20);

            float randomOffset = UnityEngine.Random.Range(minOffset, maxOffset);
            float finalSalary = baseSalary * (1f + randomOffset);

            return Mathf.RoundToInt(finalSalary);
        }

        public int GetRenewalSalary(Employee employee)
        {
            return CalculateMonthlySalary(employee);
        }

        public void Renew(Employee employee)
        {
            if (employee == null || !employees.Contains(employee))
            {
                return;
            }

            int contractDuration = config != null ? config.defaultContractTurns : 120;
            int baseTurn = Mathf.Max(currentTurn, employee.ContractEndTurn);

            employee.MonthlySalary = GetRenewalSalary(employee);
            employee.ContractEndTurn = baseTurn + contractDuration;
            employee.HasNotifiedContractExpired = false;
            employee.ScheduledLeaveTurn = -1;
        }

        public void Decline(Employee employee)
        {
            if (employee == null || !employees.Contains(employee))
            {
                return;
            }

            int graceTurns = config != null ? config.declineGraceTurns : 30;
            employee.ScheduledLeaveTurn = currentTurn + graceTurns;
        }

        public void Dismiss(Employee employee)
        {
            if (employee == null || !employees.Contains(employee))
            {
                return;
            }

            employees.Remove(employee);
            OnEmployeeLeft?.Invoke(employee);
        }

        public int GetSeveranceAmount(Employee employee)
        {
            if (employee == null)
            {
                return 0;
            }

            int remainingTurns = Mathf.Max(0, employee.ContractEndTurn - currentTurn);
            int turnsPerMonth = config != null && config.turnsPerMonth > 0 ? config.turnsPerMonth : 30;

            float months = (float)remainingTurns / turnsPerMonth;
            return Mathf.RoundToInt(employee.MonthlySalary * months);
        }

        private void ProcessScheduledLeaves()
        {
            for (int i = employees.Count - 1; i >= 0; i--)
            {
                Employee emp = employees[i];
                if (emp.ScheduledLeaveTurn >= 0 && currentTurn >= emp.ScheduledLeaveTurn)
                {
                    employees.RemoveAt(i);
                    OnEmployeeLeft?.Invoke(emp);
                }
            }
        }

        private void ProcessContractExpirations()
        {
            for (int i = 0; i < employees.Count; i++)
            {
                Employee emp = employees[i];
                if (!emp.HasNotifiedContractExpired && emp.ScheduledLeaveTurn < 0 && currentTurn >= emp.ContractEndTurn)
                {
                    emp.HasNotifiedContractExpired = true;
                    OnContractExpired?.Invoke(emp);
                }
            }
        }

        private void ProcessHiddenStatReveals()
        {
            int interval = config != null ? config.hiddenStatRevealIntervalTurns : 30;
            if (interval <= 0)
            {
                return;
            }

            for (int i = 0; i < employees.Count; i++)
            {
                Employee emp = employees[i];
                int elapsed = currentTurn - emp.HiredTurn;
                if (elapsed <= 0 || elapsed % interval != 0)
                {
                    continue;
                }

                List<HiddenStatType> unrevealed = emp.GetUnrevealedHiddenStats();
                if (unrevealed.Count == 0)
                {
                    continue;
                }

                int pick = UnityEngine.Random.Range(0, unrevealed.Count);
                HiddenStatType chosen = unrevealed[pick];
                emp.SetHiddenStatRevealed(chosen, true);
                OnStatRevealed?.Invoke(emp, chosen);
            }
        }

        private void GenerateApplicants()
        {
            applicants.Clear();
            Vector2Int range = GetApplicantCountRange();
            int count = UnityEngine.Random.Range(range.x, range.y + 1);

            for (int i = 0; i < count; i++)
            {
                applicants.Add(CreateRandomApplicant());
            }
        }

        private Vector2Int GetApplicantCountRange()
        {
            int stage = Mathf.Clamp(companyScaleStage, 1, 3);
            if (config == null)
            {
                return stage switch
                {
                    1 => new Vector2Int(3, 5),
                    2 => new Vector2Int(4, 7),
                    _ => new Vector2Int(6, 10)
                };
            }

            return stage switch
            {
                1 => config.stage1ApplicantRange,
                2 => config.stage2ApplicantRange,
                _ => config.stage3ApplicantRange
            };
        }

        private Employee CreateRandomApplicant()
        {
            int minStat = config != null ? config.minStatValue : 0;
            int maxStat = config != null ? config.maxStatValue : 20;
            float rookieMax = config != null ? config.rookieMaxRatio : 0.2f;
            float midMax = config != null ? config.midMaxRatio : 0.8f;

            int[] maxStats = new int[5];
            int potential = 0;
            for (int i = 0; i < 5; i++)
            {
                maxStats[i] = UnityEngine.Random.Range(minStat, maxStat + 1);
                potential += maxStats[i];
            }

            CareerGrade targetGrade = (CareerGrade)UnityEngine.Random.Range(0, 3);
            int[] curStats = new int[5];

            if (potential > 0)
            {
                float minRatio = targetGrade switch
                {
                    CareerGrade.Rookie => 0f,
                    CareerGrade.Mid => rookieMax + 0.0001f,
                    _ => midMax + 0.0001f
                };

                float maxRatio = targetGrade switch
                {
                    CareerGrade.Rookie => rookieMax,
                    CareerGrade.Mid => midMax,
                    _ => 1f
                };

                int minSum = Mathf.CeilToInt(minRatio * potential);
                int maxSum = Mathf.FloorToInt(maxRatio * potential);

                if (minSum > maxSum)
                {
                    if (targetGrade == CareerGrade.Rookie)
                    {
                        minSum = 0;
                        maxSum = 0;
                    }
                    else if (targetGrade == CareerGrade.Mid)
                    {
                        minSum = Mathf.Clamp(Mathf.RoundToInt(0.5f * potential), 0, potential);
                        maxSum = minSum;
                    }
                    else
                    {
                        minSum = potential;
                        maxSum = potential;
                    }
                }

                minSum = Mathf.Clamp(minSum, 0, potential);
                maxSum = Mathf.Clamp(maxSum, minSum, potential);

                int targetSum = UnityEngine.Random.Range(minSum, maxSum + 1);
                int remaining = targetSum;

                List<int> availableIndices = new List<int> { 0, 1, 2, 3, 4 };
                while (remaining > 0 && availableIndices.Count > 0)
                {
                    int pick = UnityEngine.Random.Range(0, availableIndices.Count);
                    int statIdx = availableIndices[pick];

                    if (curStats[statIdx] < maxStats[statIdx])
                    {
                        curStats[statIdx]++;
                        remaining--;
                    }
                    else
                    {
                        availableIndices.RemoveAt(pick);
                    }
                }
            }

            int diligence = UnityEngine.Random.Range(minStat, maxStat + 1);
            int loyalty = UnityEngine.Random.Range(minStat, maxStat + 1);
            int ambition = UnityEngine.Random.Range(minStat, maxStat + 1);

            string randomName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)] +
                                FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];

            Employee applicant = new Employee(
                randomName,
                curStats[0], maxStats[0],
                curStats[1], maxStats[1],
                curStats[2], maxStats[2],
                curStats[3], maxStats[3],
                curStats[4], maxStats[4],
                diligence, loyalty, ambition,
                0, 0, 0,
                config);

            applicant.Grade = targetGrade;
            applicant.MonthlySalary = CalculateMonthlySalary(applicant);
            return applicant;
        }
    }
}