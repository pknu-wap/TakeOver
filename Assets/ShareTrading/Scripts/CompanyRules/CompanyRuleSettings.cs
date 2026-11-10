using System;
using UnityEngine;

namespace TakeOver.CompanyRules
{
    [CreateAssetMenu(fileName = "CompanyRuleSettings", menuName = "Company/Rule Settings")]
    public sealed class CompanyRuleSettings : ScriptableObject
    {
        public PerformanceSettings performance = new PerformanceSettings();
        public EmployeeSettings employees = new EmployeeSettings();
        public DividendSettings dividend = new DividendSettings();
        public DelistingSettings delisting = new DelistingSettings();

        [Serializable]
        public sealed class PerformanceSettings
        {
            [Min(1)] public int periodTurns = 7;
            public float marketWeight = 0.02f;
            public float positionWeight = 0.01f;
            public float reputationWeight = 0.01f;
            public float employeeWeight = 0.01f;
            public float minGrowth = -0.5f;
            public float maxGrowth = 0.5f;
            public float fixedCost = 100f;
            public float costPerEmployee = 1f;
            public float variableCostRatio = 0.8f;
        }

        [Serializable]
        public sealed class EmployeeSettings
        {
            [Min(1)] public int periodTurns = 7;
            public float revenueChangeWeight = 0.01f;
            public float lossWeight = 0.01f;
        }

        [Serializable]
        public sealed class DividendSettings
        {
            [Min(1)] public int periodTurns = 7;
        }

        [Serializable]
        public sealed class DelistingSettings
        {
            [Min(1)] public int periodTurns = 7;
            public float watchlistRatio = 0.5f;
            [Min(1)] public int graceReviews = 8;
            [Min(1)] public int liquidationTurns = 7;
        }
    }
}
