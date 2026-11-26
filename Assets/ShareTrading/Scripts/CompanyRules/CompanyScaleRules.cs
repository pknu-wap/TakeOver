using UnityEngine;

namespace TakeOver.CompanyRules
{
    public enum CompanyScale { Small, Medium, Large }

    /// <summary>이벤트 조건을 확인하는 시점의 기업가치로 규모를 판정한다.</summary>
    public static class CompanyScaleRules
    {
        public const float MediumMinimum = 2000;
        public const float LargeMinimum = 7000;

        public static bool TryGetScale(float currentCompanyValue, out CompanyScale scale)
        {
            scale = CompanyScale.Small;
            float value = currentCompanyValue;
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) return false;
            scale = value >= LargeMinimum ? CompanyScale.Large : value >= MediumMinimum ? CompanyScale.Medium : CompanyScale.Small;
            return true;
        }

        public static bool TryGetGreenmailStake(float currentCompanyValue, out float stake)
        {
            stake = 0;
            if (!TryGetScale(currentCompanyValue, out var scale)) return false;
            stake = scale == CompanyScale.Small ? 35 : scale == CompanyScale.Medium ? 30 : 25;
            return true;
        }
    }
}
