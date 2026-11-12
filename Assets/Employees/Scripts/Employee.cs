using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Employees
{
    [Serializable]
    public class AbilityStat
    {
        [SerializeField] private int current;
        [SerializeField] private int max;
        [SerializeField] private float progress;

        public int Current => current;
        public int Max => max;
        public float Progress => progress;

        public AbilityStat(int current, int max, EmployeeConfig config)
        {
            this.max = EmployeeMath.ClampStat(max, config);
            this.current = Mathf.Clamp(EmployeeMath.ClampStat(current, config), 0, this.max);
            this.progress = 0f;
        }

        public bool AddProgress(float amount)
        {
            if (current >= max || amount <= 0f)
            {
                if (current >= max)
                {
                    progress = 0f;
                }
                return false;
            }

            progress += amount;
            bool increased = false;

            while (progress >= 1f && current < max)
            {
                current++;
                progress -= 1f;
                increased = true;
            }

            if (current >= max)
            {
                current = max;
                progress = 0f;
            }

            return increased;
        }
    }

    [Serializable]
    public class HiddenStat
    {
        [SerializeField] private int value;
        [SerializeField] private bool isRevealed;

        public int Value => value;
        public bool IsRevealed
        {
            get => isRevealed;
            set => isRevealed = value;
        }

        public HiddenStat(int value, bool isRevealed, EmployeeConfig config)
        {
            this.value = EmployeeMath.ClampStat(value, config);
            this.isRevealed = isRevealed;
        }
    }

    [Serializable]
    public class Employee
    {
        [SerializeField] private string employeeName;
        [SerializeField] private CareerGrade grade;

        [SerializeField] private AbilityStat analysis;
        [SerializeField] private AbilityStat network;
        [SerializeField] private AbilityStat eye;
        [SerializeField] private AbilityStat legwork;
        [SerializeField] private AbilityStat management;

        [SerializeField] private HiddenStat diligence;
        [SerializeField] private HiddenStat loyalty;
        [SerializeField] private HiddenStat ambition;
        [SerializeField] private bool isPotentialRevealed;

        [SerializeField] private int monthlySalary;
        [SerializeField] private int hiredTurn;
        [SerializeField] private int contractEndTurn;
        [SerializeField] private bool hasNotifiedContractExpired;
        [SerializeField] private int scheduledLeaveTurn = -1;

        public string Name => employeeName;
        public CareerGrade Grade
        {
            get => grade;
            set => grade = value;
        }

        public AbilityStat Analysis => analysis;
        public AbilityStat Network => network;
        public AbilityStat Eye => eye;
        public AbilityStat Legwork => legwork;
        public AbilityStat Management => management;

        public HiddenStat Diligence => diligence;
        public HiddenStat Loyalty => loyalty;
        public HiddenStat Ambition => ambition;
        public bool IsPotentialRevealed
        {
            get => isPotentialRevealed;
            set => isPotentialRevealed = value;
        }

        public int Potential => analysis.Max + network.Max + eye.Max + legwork.Max + management.Max;

        public int MonthlySalary
        {
            get => monthlySalary;
            set => monthlySalary = value;
        }

        public int HiredTurn
        {
            get => hiredTurn;
            set => hiredTurn = value;
        }

        public int ContractEndTurn
        {
            get => contractEndTurn;
            set => contractEndTurn = value;
        }

        public bool HasNotifiedContractExpired
        {
            get => hasNotifiedContractExpired;
            set => hasNotifiedContractExpired = value;
        }

        public int ScheduledLeaveTurn
        {
            get => scheduledLeaveTurn;
            set => scheduledLeaveTurn = value;
        }

        public Employee(
            string name,
            int analysisCur, int analysisMax,
            int networkCur, int networkMax,
            int eyeCur, int eyeMax,
            int legworkCur, int legworkMax,
            int managementCur, int managementMax,
            int diligenceVal, int loyaltyVal, int ambitionVal,
            int monthlySalary, int hiredTurn, int contractEndTurn,
            EmployeeConfig config)
        {
            employeeName = name;

            analysis = new AbilityStat(analysisCur, analysisMax, config);
            network = new AbilityStat(networkCur, networkMax, config);
            eye = new AbilityStat(eyeCur, eyeMax, config);
            legwork = new AbilityStat(legworkCur, legworkMax, config);
            management = new AbilityStat(managementCur, managementMax, config);

            diligence = new HiddenStat(diligenceVal, false, config);
            loyalty = new HiddenStat(loyaltyVal, false, config);
            ambition = new HiddenStat(ambitionVal, false, config);
            isPotentialRevealed = false;

            this.monthlySalary = monthlySalary;
            this.hiredTurn = hiredTurn;
            this.contractEndTurn = contractEndTurn;
            this.hasNotifiedContractExpired = false;
            this.scheduledLeaveTurn = -1;

            grade = CalculateGrade(config);
        }

        public AbilityStat GetAbilityStat(AbilityStatType type)
        {
            return type switch
            {
                AbilityStatType.Analysis => analysis,
                AbilityStatType.Network => network,
                AbilityStatType.Eye => eye,
                AbilityStatType.Legwork => legwork,
                AbilityStatType.Management => management,
                _ => null
            };
        }

        public HiddenStat GetHiddenStat(HiddenStatType type)
        {
            return type switch
            {
                HiddenStatType.Diligence => diligence,
                HiddenStatType.Loyalty => loyalty,
                HiddenStatType.Ambition => ambition,
                _ => null
            };
        }

        public bool IsHiddenStatRevealed(HiddenStatType type)
        {
            return type switch
            {
                HiddenStatType.Diligence => diligence.IsRevealed,
                HiddenStatType.Loyalty => loyalty.IsRevealed,
                HiddenStatType.Ambition => ambition.IsRevealed,
                HiddenStatType.Potential => isPotentialRevealed,
                _ => false
            };
        }

        public void SetHiddenStatRevealed(HiddenStatType type, bool revealed)
        {
            switch (type)
            {
                case HiddenStatType.Diligence:
                    diligence.IsRevealed = revealed;
                    break;
                case HiddenStatType.Loyalty:
                    loyalty.IsRevealed = revealed;
                    break;
                case HiddenStatType.Ambition:
                    ambition.IsRevealed = revealed;
                    break;
                case HiddenStatType.Potential:
                    isPotentialRevealed = revealed;
                    break;
            }
        }

        public int GetHiddenStatRawValue(HiddenStatType type)
        {
            return type switch
            {
                HiddenStatType.Diligence => diligence.Value,
                HiddenStatType.Loyalty => loyalty.Value,
                HiddenStatType.Ambition => ambition.Value,
                HiddenStatType.Potential => Potential,
                _ => 0
            };
        }

        public List<HiddenStatType> GetUnrevealedHiddenStats()
        {
            List<HiddenStatType> list = new List<HiddenStatType>(4);
            if (!diligence.IsRevealed) list.Add(HiddenStatType.Diligence);
            if (!loyalty.IsRevealed) list.Add(HiddenStatType.Loyalty);
            if (!ambition.IsRevealed) list.Add(HiddenStatType.Ambition);
            if (!isPotentialRevealed) list.Add(HiddenStatType.Potential);
            return list;
        }

        public float GetFillRatio()
        {
            int maxSum = Potential;
            if (maxSum <= 0)
            {
                return 0f;
            }

            int currentSum = analysis.Current + network.Current + eye.Current + legwork.Current + management.Current;
            return (float)currentSum / maxSum;
        }

        public CareerGrade CalculateGrade(EmployeeConfig config)
        {
            float ratio = GetFillRatio();
            float rookieMax = config != null ? config.rookieMaxRatio : 0.2f;
            float midMax = config != null ? config.midMaxRatio : 0.8f;

            if (ratio <= rookieMax)
            {
                return CareerGrade.Rookie;
            }

            if (ratio <= midMax)
            {
                return CareerGrade.Mid;
            }

            return CareerGrade.Veteran;
        }
    }
}