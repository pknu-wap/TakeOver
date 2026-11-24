using System;
using System.Collections.Generic;
using TakeOver.NPC;
using TMPro;
using UnityEngine;

public class CalendarController : MonoBehaviour
{
    [Header("Top Title UI")]
    [SerializeField] private TextMeshProUGUI dateTitleText;
    [SerializeField] private TextMeshProUGUI dayCountText;

    [Header("Date Grid Items (7개 - 일~토)")]
    [SerializeField] private List<TextMeshProUGUI> dateNumberTexts;
    [SerializeField] private List<GameObject> selectionCircles;

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.black;
    [SerializeField] private Color sundayColor = new Color(0.9f, 0.3f, 0.3f);
    [SerializeField] private Color saturdayColor = new Color(0.4f, 0.6f, 1f);

    private DateTime currentDate = GameCalendar.DateOf(1);
    private GameTurnClock turnClock;

    private void OnEnable()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.TurnAdvanced += HandleTurnAdvanced;
            turnClock.TurnRestored += HandleTurnAdvanced;
        }
        RefreshFromTurn();
    }

    private void OnDisable()
    {
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= HandleTurnAdvanced;
            turnClock.TurnRestored -= HandleTurnAdvanced;
        }
    }

    private void HandleTurnAdvanced(int newTurn)
    {
        RefreshFromTurn();
    }

    private void RefreshFromTurn()
    {
        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        if (dayCountText != null)
        {
            if (dateTitleText != null && dayCountText.font != dateTitleText.font) dayCountText.font = dateTitleText.font;
            dayCountText.text = $"{turn}일째";
        }
        UpdateCalendarUI(GameCalendar.DateOf(turn));
    }

    public void UpdateCalendarUI(DateTime targetDate)
    {
        currentDate = targetDate;

        if (dateTitleText != null)
        {
            dateTitleText.text = GameCalendar.Format(currentDate);
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

}
