using System;
using System.Collections.Generic;
using TakeOver.NPC;
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

    [Header("Start Date (DayPanel과 같은 값)")]
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;

    private DateTime currentDate = new DateTime(2026, 1, 1);
    private GameTurnClock turnClock;

    private void OnEnable()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.TurnAdvanced += HandleTurnAdvanced;
        }
        RefreshFromTurn();
    }

    private void OnDisable()
    {
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= HandleTurnAdvanced;
        }
    }

    private void HandleTurnAdvanced(int newTurn)
    {
        RefreshFromTurn();
    }

    private void RefreshFromTurn()
    {
        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        UpdateCalendarUI(new DateTime(startYear, startMonth, startDay).AddDays(turn - 1));
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
}