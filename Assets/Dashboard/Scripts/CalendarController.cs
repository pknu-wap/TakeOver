using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CalendarController : MonoBehaviour
{
    [Header("Top Title UI")]
    [SerializeField] private TextMeshProUGUI dateTitleText;

    [Header("Date Grid Items (7개 - 일~토)")]
    [SerializeField] private List<TextMeshProUGUI> dateNumberTexts;
    [SerializeField] private List<GameObject> selectionCircles;

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.black;
    [SerializeField] private Color sundayColor = new Color(0.9f, 0.3f, 0.3f);
    [SerializeField] private Color saturdayColor = new Color(0.4f, 0.6f, 1f);

    private DateTime currentDate = new DateTime(2026, 1, 1);

    private void Start()
    {
        UpdateCalendarUI(currentDate);
    }

    public void UpdateCalendarUI(DateTime targetDate)
    {
        currentDate = targetDate;

        string dayOfWeekKorean = GetKoreanDayOfWeek(currentDate.DayOfWeek);
        if (dateTitleText != null)
        {
            dateTitleText.text = $"{currentDate:yyyy.MM.dd} ({dayOfWeekKorean})";
        }

        int diffToSunday = (int)currentDate.DayOfWeek;
        DateTime sundayOfWeek = currentDate.AddDays(-diffToSunday);

        for (int i = 0; i < 7; i++)
        {
            DateTime thisDay = sundayOfWeek.AddDays(i);

            if (i < dateNumberTexts.Count && dateNumberTexts[i] != null)
            {
                dateNumberTexts[i].text = thisDay.Day.ToString();

                bool isSelected = (thisDay.Date == currentDate.Date);

                if (i < selectionCircles.Count && selectionCircles[i] != null)
                {
                    selectionCircles[i].SetActive(isSelected);
                }

                if (isSelected)
                {
                    dateNumberTexts[i].color = selectedTextColor;
                }
                else if (thisDay.DayOfWeek == DayOfWeek.Sunday)
                {
                    dateNumberTexts[i].color = sundayColor;
                }
                else if (thisDay.DayOfWeek == DayOfWeek.Saturday)
                {
                    dateNumberTexts[i].color = saturdayColor;
                }
                else
                {
                    dateNumberTexts[i].color = normalTextColor;
                }
            }
        }
    }

    private string GetKoreanDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "일",
            DayOfWeek.Monday => "월",
            DayOfWeek.Tuesday => "화",
            DayOfWeek.Wednesday => "수",
            DayOfWeek.Thursday => "목",
            DayOfWeek.Friday => "금",
            DayOfWeek.Saturday => "토",
            _ => ""
        };
    }

    public void AddDays(int days)
    {
        UpdateCalendarUI(currentDate.AddDays(days));
    }
}