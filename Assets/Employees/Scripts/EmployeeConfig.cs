using UnityEngine;

namespace TakeOver.Employees
{
    [CreateAssetMenu(fileName = "EmployeeConfig", menuName = "TakeOver/Employees/EmployeeConfig")]
    public class EmployeeConfig : ScriptableObject
    {
        [Header("공통 스탯 규칙")]
        public int minStatValue = 0;
        public int maxStatValue = 20;
        public int statCount = 5;

        [Header("경력 등급 기준 비율")]
        public float rookieMaxRatio = 0.2f;
        public float midMaxRatio = 0.8f;

        [Header("성장 속도 배율 (야망 0 ~ 20)")]
        public float minGrowthRate = 0.7f;
        public float maxGrowthRate = 1.5f;

        [Header("모집 및 고용 설정")]
        public int recruitApCost = 5;
        public int recruitDurationTurns = 2;
        public Vector2Int stage1ApplicantRange = new Vector2Int(3, 5);
        public Vector2Int stage2ApplicantRange = new Vector2Int(4, 7);
        public Vector2Int stage3ApplicantRange = new Vector2Int(6, 10);

        [Header("월급 계산 설정")]
        public float baseSalaryFlat = 250f;
        public float salaryStatMultiplier = 2.0f;
        public float rookieSalaryMultiplier = 1.0f;
        public float midSalaryMultiplier = 1.3f;
        public float veteranSalaryMultiplier = 1.8f;
        public float ambition0SalaryMinOffset = -0.10f;
        public float ambition0SalaryMaxOffset = 0.10f;
        public float ambition20SalaryMinOffset = -0.05f;
        public float ambition20SalaryMaxOffset = 0.30f;

        [Header("숨은 스탯 공개 설정")]
        public int hiddenStatRevealIntervalTurns = 30;

        [Header("계약 및 해고 설정")]
        public int defaultContractTurns = 120;
        public int declineGraceTurns = 30;
        public int turnsPerMonth = 30;
    }
}