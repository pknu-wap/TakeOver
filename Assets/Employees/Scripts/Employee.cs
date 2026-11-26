using System;
using System.Collections.Generic;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    [Serializable]
    public class Employee : ISerializationCallbackReceiver
    {
        // EmployeeService가 지원자를 만들 때 붙이는 번호. 1부터 증가하고 재사용하지 않는다(0은 번호 없음).
        [field: SerializeField] public int Id { get; internal set; }
        public string Name;
        [SerializeField] private int appearance;
        public NpcMobAppearance Appearance
        {
            get => appearance == 0 ? GuessAppearance(Name) : (NpcMobAppearance)(appearance - 1);
            set => appearance = (int)value + 1;
        }
        public string ProfileNumber => $"NO. 2026-{Id:D4}";
        public static NpcMobAppearance GuessAppearance(string name)
        {
            foreach (string given in new[] { "서연", "하은", "수빈", "지민", "예진" })
                if (!string.IsNullOrEmpty(name) && name.EndsWith(given)) return NpcMobAppearance.Female;
            return NpcMobAppearance.Male;
        }
        public void OnBeforeSerialize() { appearance = (int)Appearance + 1; }
        public void OnAfterDeserialize() { appearance = (int)Appearance + 1; }

        public AbilityStat Analysis;
        public AbilityStat Network;
        public AbilityStat Eye;
        public AbilityStat Legwork;
        public AbilityStat Management;

        public HiddenStat Diligence;
        public HiddenStat Loyalty;
        public HiddenStat Ambition;
        public HiddenStat PotentialStat;

        [field: SerializeField] public EmployeeStatSnapshot ContractStartStats { get; private set; }

        public int MonthlySalary;
        public int HiredTurn;
        public int ContractEndTurn;
        public int NextRevealTurn;
        public int LeaveTurn;
        public bool IsPendingLeave;

        [field: SerializeField] public bool HasSentRenewalNotice { get; set; }
        [SerializeField] private int pendingRenewalSalary = -1;
        public int? PendingRenewalSalary
        {
            get => pendingRenewalSalary < 0 ? (int?)null : pendingRenewalSalary;
            set => pendingRenewalSalary = value ?? -1;
        }

        public int Potential => Analysis.Max + Network.Max + Eye.Max + Legwork.Max + Management.Max;

        public CareerGrade Grade
        {
            get
            {
                int totalMax = Potential;
                if (totalMax <= 0)
                {
                    return CareerGrade.Rookie;
                }

                int totalCur = Analysis.Current + Network.Current + Eye.Current + Legwork.Current + Management.Current;
                float ratio = (float)totalCur / totalMax;

                if (ratio >= 0.81f)
                {
                    return CareerGrade.Veteran;
                }

                if (ratio >= 0.31f)
                {
                    return CareerGrade.Mid;
                }

                return CareerGrade.Rookie;
            }
        }

        public Employee(
            string name,
            int analysisCur, int analysisMax,
            int networkCur, int networkMax,
            int eyeCur, int eyeMax,
            int legworkCur, int legworkMax,
            int managementCur, int managementMax,
            int diligence, int loyalty, int ambition,
            int monthlySalary, int hiredTurn, int contractEndTurn,
            EmployeeConfig employeeConfig)
        {
            Name = name;
            Appearance = GuessAppearance(name);

            Analysis = new AbilityStat(analysisCur, analysisMax, 0f);
            Network = new AbilityStat(networkCur, networkMax, 0f);
            Eye = new AbilityStat(eyeCur, eyeMax, 0f);
            Legwork = new AbilityStat(legworkCur, legworkMax, 0f);
            Management = new AbilityStat(managementCur, managementMax, 0f);

            Diligence = new HiddenStat(Mathf.Clamp(diligence, 0, 20), false);
            Loyalty = new HiddenStat(Mathf.Clamp(loyalty, 0, 20), false);
            Ambition = new HiddenStat(Mathf.Clamp(ambition, 0, 20), false);
            PotentialStat = new HiddenStat(Potential, false);

            MonthlySalary = monthlySalary;
            HiredTurn = hiredTurn;
            ContractEndTurn = contractEndTurn;
            NextRevealTurn = hiredTurn + (employeeConfig != null ? employeeConfig.hiddenStatRevealIntervalTurns : 10);
            LeaveTurn = -1;
            IsPendingLeave = false;
            HasSentRenewalNotice = false;
            PendingRenewalSalary = null;

            CaptureContractStartStats();
        }

        public void CaptureContractStartStats()
        {
            ContractStartStats = new EmployeeStatSnapshot(
                Analysis.Current,
                Network.Current,
                Eye.Current,
                Legwork.Current,
                Management.Current);
        }

        public void RaiseStat(AbilityStatType statType, float delta)
        {
            AbilityStat stat = GetStat(statType);
            float total = stat.Progress + delta;
            int gain = Mathf.FloorToInt(total);
            float remain = total - gain;

            if (gain > 0)
            {
                stat.Current = Mathf.Clamp(stat.Current + gain, 0, stat.Max);
            }

            stat.Progress = stat.Current >= stat.Max ? 0f : Mathf.Max(0f, remain);
            SetStat(statType, stat);
        }

        public bool IsHiddenRevealed(HiddenStatType statType)
        {
            switch (statType)
            {
                case HiddenStatType.Diligence: return Diligence.IsRevealed;
                case HiddenStatType.Loyalty: return Loyalty.IsRevealed;
                case HiddenStatType.Ambition: return Ambition.IsRevealed;
                case HiddenStatType.Potential: return PotentialStat.IsRevealed;
                default: return false;
            }
        }

        public int GetHiddenStat(HiddenStatType statType)
        {
            if (!IsHiddenRevealed(statType))
            {
                throw new InvalidOperationException(
                    $"[Employee] 직원 '{Name}'(ID:{Id})의 히든 스탯 '{statType}'이(가) 아직 공개되지 않은 상태에서 조회되었습니다.");
            }

            return GetHiddenStatRaw(statType);
        }

        public bool TryGetHiddenStat(HiddenStatType statType, out int value)
        {
            if (!IsHiddenRevealed(statType))
            {
                value = 0;
                return false;
            }

            value = GetHiddenStatRaw(statType);
            return true;
        }

        private int GetHiddenStatRaw(HiddenStatType statType)
        {
            switch (statType)
            {
                case HiddenStatType.Diligence: return Diligence.Value;
                case HiddenStatType.Loyalty: return Loyalty.Value;
                case HiddenStatType.Ambition: return Ambition.Value;
                case HiddenStatType.Potential: return Potential;
                default: return 0;
            }
        }

        public void SetRevealed(HiddenStatType statType, bool revealed)
        {
            switch (statType)
            {
                case HiddenStatType.Diligence:
                    Diligence.IsRevealed = revealed;
                    break;
                case HiddenStatType.Loyalty:
                    Loyalty.IsRevealed = revealed;
                    break;
                case HiddenStatType.Ambition:
                    Ambition.IsRevealed = revealed;
                    break;
                case HiddenStatType.Potential:
                    PotentialStat.Value = Potential;
                    PotentialStat.IsRevealed = revealed;
                    break;
            }
        }

        public bool RevealNextHiddenStat(out HiddenStatType revealedStat)
        {
            revealedStat = HiddenStatType.Diligence;
            List<HiddenStatType> candidates = new List<HiddenStatType>(4);

            if (!Diligence.IsRevealed) candidates.Add(HiddenStatType.Diligence);
            if (!Loyalty.IsRevealed) candidates.Add(HiddenStatType.Loyalty);
            if (!Ambition.IsRevealed) candidates.Add(HiddenStatType.Ambition);
            if (!PotentialStat.IsRevealed) candidates.Add(HiddenStatType.Potential);

            if (candidates.Count == 0)
            {
                return false;
            }

            revealedStat = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            SetRevealed(revealedStat, true);
            return true;
        }

        public void SetPendingLeave(int leaveTurn)
        {
            if (leaveTurn < 0)
            {
                IsPendingLeave = false;
                LeaveTurn = -1;
            }
            else
            {
                IsPendingLeave = true;
                LeaveTurn = leaveTurn;
            }
        }

        private AbilityStat GetStat(AbilityStatType statType)
        {
            switch (statType)
            {
                case AbilityStatType.Analysis: return Analysis;
                case AbilityStatType.Network: return Network;
                case AbilityStatType.Eye: return Eye;
                case AbilityStatType.Legwork: return Legwork;
                case AbilityStatType.Management: return Management;
                default: return Analysis;
            }
        }

        private void SetStat(AbilityStatType statType, AbilityStat stat)
        {
            switch (statType)
            {
                case AbilityStatType.Analysis: Analysis = stat; break;
                case AbilityStatType.Network: Network = stat; break;
                case AbilityStatType.Eye: Eye = stat; break;
                case AbilityStatType.Legwork: Legwork = stat; break;
                case AbilityStatType.Management: Management = stat; break;
            }
        }
    }
}
