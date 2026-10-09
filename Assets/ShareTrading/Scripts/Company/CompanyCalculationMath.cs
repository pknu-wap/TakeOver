using System;
using UnityEngine;

internal static class CompanyCalculationMath
{
    // 초기 순자산가치가 0이어도 런타임 계산의 분모가 0이 되지 않도록 함.
    private const float MIN_DENOMINATOR = 0.0001f;

    public static float finite(double value, float fallback = 0f)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return fallback;
        // 중간 계산 결과를 float에 저장할 수 있는 유한한 범위로 제한한다.
        return (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value));
    }

    public static float safeDenominator(float value, float initialScale, float scaleRatio = 0.1f)
    {
        return Mathf.Max(MIN_DENOMINATOR,
            Mathf.Max(Mathf.Abs(finite(value)), Mathf.Abs(finite(initialScale)) * scaleRatio));
    }

    public static float positiveDenominator(double value)
    {
        return Mathf.Max(MIN_DENOMINATOR, finite(value));
    }

    public static float safeDivide(double numerator, float denominator, float fallback = 0f)
    {
        return finite(numerator / denominator, fallback);
    }

    public static float clamp(double value, float min, float max, float fallback = 0f)
    {
        return Mathf.Clamp(finite(value, fallback), min, max);
    }

    public static float clampFactor(double value, float min = 0.25f, float max = 2.5f)
    {
        return clamp(value, min, max, 1f);
    }

    public static float normalizeScore(float value)
    {
        return clamp(((double)value - 50d) / 50d, -1f, 1f);
    }
}
