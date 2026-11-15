using UnityEngine;

namespace TakeOver.CompanyRules
{
    public enum CompanyScale { Small, Medium, Large }

    /// <summary>초기 기업가치 기준 고정 분류. 현재 주가·실적·이벤트 보정으로 바뀌지 않는다.</summary>
    public static class CompanyScaleRules
    {
        public const float MediumMinimum = 1000;
        public const float LargeMinimum = 10000;

        public static bool TryGetScale(CompanyDefinition definition, out CompanyScale scale)
        {
            scale = CompanyScale.Small;
            if (definition == null) return false;
            float value = definition.initialBaseCompanyValue;
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) return false;
            scale = value >= LargeMinimum ? CompanyScale.Large : value >= MediumMinimum ? CompanyScale.Medium : CompanyScale.Small;
            return true;
        }

        public static bool TryGetGreenmailStake(CompanyDefinition definition, out float stake)
        {
            stake = 0;
            if (!TryGetScale(definition, out var scale)) return false;
            stake = scale == CompanyScale.Small ? 35 : scale == CompanyScale.Medium ? 30 : 25;
            return true;
        }
    }
}
