using System;

namespace TakeOver.Employees
{
    public enum AbilityStatType
    {
        Analysis,
        Network,
        Eye,
        Legwork,
        Management
    }

    public enum HiddenStatType
    {
        Diligence,
        Loyalty,
        Ambition,
        Potential
    }

    public enum CareerGrade
    {
        Rookie,
        Mid,
        Veteran
    }

    [Serializable]
    public struct AbilityStat
    {
        public int Current;
        public int Max;
        public float Progress;

        public AbilityStat(int current, int max, float progress = 0f)
        {
            Max = max < 0 ? 0 : max;
            Current = current < 0 ? 0 : (current > Max ? Max : current);
            Progress = progress < 0f ? 0f : progress;
        }
    }

    [Serializable]
    public struct HiddenStat
    {
        public int Value;
        public bool IsRevealed;

        public HiddenStat(int value, bool isRevealed = false)
        {
            Value = value;
            IsRevealed = isRevealed;
        }
    }

    [Serializable]
    public struct EmployeeStatSnapshot
    {
        public int Analysis;
        public int Network;
        public int Eye;
        public int Legwork;
        public int Management;

        public EmployeeStatSnapshot(int analysis, int network, int eye, int legwork, int management)
        {
            Analysis = analysis;
            Network = network;
            Eye = eye;
            Legwork = legwork;
            Management = management;
        }
    }
}