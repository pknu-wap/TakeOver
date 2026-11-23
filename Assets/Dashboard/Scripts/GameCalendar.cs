using System;
using System.Globalization;

public static class GameCalendar
{
    private static readonly string[] dayNames = { "일", "월", "화", "수", "목", "금", "토" };

    public static DateTime DateOf(int turn) => new DateTime(2026, 1, 1).AddDays(turn - 1);
    public static string Format(int turn) => Format(DateOf(turn));
    public static string Format(DateTime date) => date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)
        + " (" + dayNames[(int)date.DayOfWeek] + ")";
}
