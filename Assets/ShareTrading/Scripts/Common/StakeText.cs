using System;
using System.Globalization;

public static class StakeText
{
    public static string Format(double percent) => FormatNumber(percent) + "%";

    public static string FormatShares(double ownedShares, int totalShares) =>
        Format(totalShares > 0 ? ownedShares * 100d / totalShares : 0d);

    public static string FormatNumber(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent) || percent <= 0d) return "0";

        double rounded = Math.Round(percent, 3, MidpointRounding.AwayFromZero);
        return Math.Max(0.001d, rounded).ToString("0.###", CultureInfo.InvariantCulture);
    }

    // Snapshot documents created by older versions store already-formatted percentages.
    public static string FormatSaved(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        string[] values = text.Split('→');
        if (values.Length > 2) return text;
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i].Trim();
            if (!value.EndsWith("%", StringComparison.Ordinal)) return text;

            string number = value.Substring(0, value.Length - 1).Trim();
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent) &&
                !double.TryParse(number, NumberStyles.Float, CultureInfo.CurrentCulture, out percent)) return text;
            values[i] = Format(percent);
        }
        return string.Join(" → ", values);
    }
}
