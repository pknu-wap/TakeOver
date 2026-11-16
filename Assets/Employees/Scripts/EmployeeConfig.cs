using UnityEngine;

namespace TakeOver.Employees
{
    [CreateAssetMenu(fileName = "EmployeeConfig", menuName = "TakeOver/Employees/EmployeeConfig")]
    public class EmployeeConfig : ScriptableObject
    {
        [Header("Stat Range Settings")]
        public int minStatValue = 0;
        public int maxStatValue = 20;

        [Header("Recruit Settings")]
        public int recruitApCost = 5;
        public int recruitDurationTurns = 2;
        public int[] applicantCountByStage = { 3, 5, 7, 9, 10 };
        public int[] maxStatCapByStage = { 8, 11, 14, 17, 20 };

        [Header("Contract & Reveal Settings")]
        public int contractDurationTurns = 120;
        public int declineGraceTurns = 30;
        public int hiddenStatRevealIntervalTurns = 10;

        [Header("Salary Settings")]
        public int baseSalary = 250;
        public int statSalaryWeight = 2;
        public float rookieGradeMultiplier = 1.0f;
        public float midGradeMultiplier = 1.3f;
        public float veteranGradeMultiplier = 1.6f;
        public float lowAmbitionMinMult = 0.9f;
        public float lowAmbitionMaxMult = 1.05f;
        public float highAmbitionMinMult = 1.15f;
        public float highAmbitionMaxMult = 1.3f;

        public float GetGradeMultiplier(CareerGrade grade)
        {
            switch (grade)
            {
                case CareerGrade.Rookie: return rookieGradeMultiplier;
                case CareerGrade.Mid: return midGradeMultiplier;
                case CareerGrade.Veteran: return veteranGradeMultiplier;
                default: return 1.0f;
            }
        }

        public int GetApplicantCountForStage(int stage)
        {
            if (applicantCountByStage == null || applicantCountByStage.Length == 0)
            {
                return 3;
            }

            int idx = Mathf.Clamp(stage - 1, 0, applicantCountByStage.Length - 1);
            return applicantCountByStage[idx];
        }

        public int GetMaxStatCapForStage(int stage)
        {
            if (maxStatCapByStage == null || maxStatCapByStage.Length == 0)
            {
                return maxStatValue;
            }

            int idx = Mathf.Clamp(stage - 1, 0, maxStatCapByStage.Length - 1);
            return maxStatCapByStage[idx];
        }
    }
}