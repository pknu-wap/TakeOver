using System;
using System.Collections.Generic;
using System.Reflection;
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

        private readonly List<Employee> employees = new List<Employee>();
        private readonly List<Employee> applicants = new List<Employee>();

        private bool isRecruiting;
        private int recruitRemainingTurns;
        private int lastSeenClockTurn;
        private bool hasSeenClockTurn;

        public EmployeeConfig Config => config;
        public IReadOnlyList<Employee> Employees => employees;
        public IReadOnlyList<Employee> Applicants => applicants;
        public bool IsRecruiting => isRecruiting;

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
        public event Action<Employee> OnContractExpired;
        public event Action<Employee> OnEmployeeLeft;

        private void OnEnable()
        {
            if (gameTurnClock != null)
            {
                if (TryReadClockTurn(out int initialTurn))
                {
                    lastSeenClockTurn = initialTurn;
                    hasSeenClockTurn = true;
                }
                gameTurnClock.TurnAdvanced += HandleTurnAdvanced;
            }
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
            if (config == null)
            {
                Debug.LogWarning("[EmployeeService] 모집 실패: Config(EmployeeConfig)가 할당되지 않았습니다.");
                return false;
            }

            if (isRecruiting)
            {
                Debug.LogWarning($"[EmployeeService] 모집 실패: 이미 모집이 진행 중입니다. (남은 턴: {recruitRemainingTurns})");
                return false;
            }

            if (actionPointService != null && !actionPointService.TryConsume(config.recruitApCost))
            {
                Debug.LogWarning($"[EmployeeService] 모집 실패: AP가 부족합니다. (필요 AP: {config.recruitApCost})");
                return false;
            }

            isRecruiting = true;
            recruitRemainingTurns = Mathf.Max(1, config.recruitDurationTurns);
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

            applicants.Remove(applicant);

            int nowTurn = GetCurrentTurn();
            applicant.HiredTurn = nowTurn;
            applicant.ContractEndTurn = nowTurn + (config != null ? config.contractDurationTurns : 120);
            applicant.SetPendingLeave(-1);
            applicant.NextRevealTurn = nowTurn + (config != null ? config.hiddenStatRevealIntervalTurns : 10);

            employees.Add(applicant);
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

            float diligenceFactor = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(emp.Diligence.Value / 20f));
            float potentialFactor = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(emp.Potential / 100f));
            return diligenceFactor * potentialFactor;
        }

        public int GetRenewalSalary(Employee emp)
        {
            return CalculateMonthlySalary(emp);
        }

        public void Renew(Employee emp)
        {
            if (emp == null || !employees.Contains(emp))
            {
                return;
            }

            int nowTurn = GetCurrentTurn();
            int duration = config != null ? config.contractDurationTurns : 120;
            emp.ContractEndTurn = nowTurn + duration;
            emp.SetPendingLeave(-1);
            emp.MonthlySalary = GetRenewalSalary(emp);
        }

        public void Decline(Employee emp)
        {
            if (emp == null || !employees.Contains(emp))
            {
                return;
            }

            int nowTurn = GetCurrentTurn();
            int graceTurns = config != null ? config.declineGraceTurns : 30;
            emp.SetPendingLeave(nowTurn + graceTurns);
        }

        public void Dismiss(Employee emp)
        {
            if (emp == null || !employees.Contains(emp))
            {
                return;
            }

            employees.Remove(emp);
            OnEmployeeLeft?.Invoke(emp);
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
                return 0;
            }

            return emp.GetHiddenStat(statType);
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
            lastSeenClockTurn = newTurn;
            hasSeenClockTurn = true;

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
            string[] firstNames = { "민수", "서연", "지훈", "하은", "도윤", "수빈", "현우", "지민", "준호", "예진" };
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

            candidate.MonthlySalary = CalculateMonthlySalary(candidate);
            return candidate;
        }

        private int GetCurrentTurn()
        {
            if (TryReadClockTurn(out int turn))
            {
                return turn;
            }

            return hasSeenClockTurn ? lastSeenClockTurn : 0;
        }

        private bool TryReadClockTurn(out int turn)
        {
            turn = 0;
            if (gameTurnClock == null)
            {
                return false;
            }

            PropertyInfo prop = typeof(GameTurnClock).GetProperty("CurrentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(int))
            {
                turn = (int)prop.GetValue(gameTurnClock);
                return true;
            }

            FieldInfo field = typeof(GameTurnClock).GetField("currentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetField("turn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null && field.FieldType == typeof(int))
            {
                turn = (int)field.GetValue(gameTurnClock);
                return true;
            }

            return false;
        }
    }
}