using System.Globalization;

public static class MoneyText
{
    private static readonly decimal[] UnitValues = { 1000000000000m, 100000000m, 10000m, 1m };
    private static readonly string[] UnitNames = { "\uc870", "\uc5b5", "\ub9cc", "\uc6d0" };

    public static string Format(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || System.Math.Abs(value) > 1e12f) return "-";
        return format(value.ToString("R", CultureInfo.InvariantCulture), value < 0f);
    }

    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || System.Math.Abs(value) > 1e12d) return "-";
        return format(value.ToString("R", CultureInfo.InvariantCulture), value < 0d);
    }

    private static string format(string value, bool negative)
    {
        decimal numericValue = roundToSignificantDigits(System.Math.Abs(decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)), 6);
        decimal won = roundToSignificantDigits(System.Math.Abs(numericValue) * 100000000m, 6);
        if (won == 0m) return "0\uc6d0";

        string sign = negative ? "-" : "";
        int largest = findLargestUnit(won);
        if (largest < 3)
        {
            decimal nextUnit = UnitValues[largest + 1];
            won = decimal.Round(won / nextUnit, 0, System.MidpointRounding.AwayFromZero) * nextUnit;
            largest = findLargestUnit(won);
        }

        if (largest == 3)
            return sign + won.ToString("N0", CultureInfo.InvariantCulture) + "\uc6d0";

        decimal high = decimal.Truncate(won / UnitValues[largest]);
        decimal low = decimal.Truncate((won % UnitValues[largest]) / UnitValues[largest + 1]);
        string result = high.ToString("N0", CultureInfo.InvariantCulture) + UnitNames[largest];
        if (low > 0m)
        {
            string lowerSuffix = largest + 1 == 3 ? "\uc6d0" : UnitNames[largest + 1] + "\uc6d0";
            result += " " + low.ToString("N0", CultureInfo.InvariantCulture) + lowerSuffix;
        }
        else
        {
            result += "\uc6d0";
        }
        return sign + result;
    }

    private static decimal roundToSignificantDigits(decimal value, int digits)
    {
        if (value == 0m) return 0m;
        int integerDigits = (int)System.Math.Floor(System.Math.Log10((double)value)) + 1;
        int exponent = integerDigits - digits;
        decimal factor = 1m;
        for (int i = 0; i < System.Math.Abs(exponent); i++) factor *= 10m;
        return exponent > 0
            ? decimal.Round(value / factor, 0, System.MidpointRounding.AwayFromZero) * factor
            : decimal.Round(value * factor, 0, System.MidpointRounding.AwayFromZero) / factor;
    }

    private static int findLargestUnit(decimal won)
    {
        for (int i = 0; i < UnitValues.Length; i++)
            if (won >= UnitValues[i]) return i;
        return UnitValues.Length - 1;
    }

    public static string FormatSigned(float value)
    {
        string formatted = Format(value);
        return value > 0f ? "+" + formatted : formatted;
    }
}
