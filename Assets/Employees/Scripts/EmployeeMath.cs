using UnityEngine;

namespace TakeOver.Employees
{
    public static class EmployeeMath
    {
        public static int ClampStat(int value, EmployeeConfig config)
        {
            if (config == null)
            {
                return Mathf.Clamp(value, 0, 20);
            }
            return Mathf.Clamp(value, config.minStatValue, config.maxStatValue);
        }
        public static float LerpLinear(float minValue, float maxValue, float t)
        {
            return Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(t));
        }
        public static int LerpLinearInt(float minValue, float maxValue, float t)
        {
            return Mathf.RoundToInt(Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(t)));
        }
        public static float MapLinear(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMin, inMax))
            {
                return outMin;
            }
            float t = Mathf.InverseLerp(inMin, inMax, value);
            return Mathf.Lerp(outMin, outMax, t);
        }
    }
}