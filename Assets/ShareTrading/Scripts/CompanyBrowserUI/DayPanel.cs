using System;
using TakeOver.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DayPanel : MonoBehaviour
{
    [Header("날짜")]
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text[] weekDayTexts = new TMP_Text[7];
    [SerializeField] private Image[] weekDayHighlights = new Image[7];
    [SerializeField] private Color highlightTextColor = new Color32(14, 24, 48, 255);
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;

    [Header("하루 마감")]
    [SerializeField] private Button endDayButton;

    [Header("턴 넘어가면 새로고침할 목록")]
    [SerializeField] private CompanyListView[] listsToRefresh;

    private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

    private GameTurnClock turnClock;
    private Color[] baseColors;

    void Awake()
    {
        baseColors = new Color[weekDayTexts.Length];
        for (int i = 0; i < weekDayTexts.Length; i++)
        {
            baseColors[i] = weekDayTexts[i] != null ? weekDayTexts[i].color : Color.white;
        }

        if (endDayButton != null)
        {
            endDayButton.onClick.AddListener(endDay);
        }
    }

    void OnEnable()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.TurnAdvanced += handleTurnAdvanced;
        }
        refresh();
    }

    void OnDisable()
    {
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= handleTurnAdvanced;
        }
    }

    public void endDay()
    {
        if (turnClock == null)
        {
            Debug.LogWarning("GameTurnClock을 찾을 수 없습니다.", this);
            return;
        }
        turnClock.AdvanceTurn();
    }

    private void handleTurnAdvanced(int newTurn)
    {
        refresh();
        foreach (CompanyListView list in listsToRefresh)
        {
            if (list != null && list.isActiveAndEnabled)
            {
                list.refresh();
            }
        }
    }

    private void refresh()
    {
        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        DateTime today = new DateTime(startYear, startMonth, startDay).AddDays(turn - 1);

        if (dateText != null)
        {
            dateText.text = $"{today:yyyy.MM.dd} ({DayNames[(int)today.DayOfWeek]})";
        }

        DateTime sunday = today.AddDays(-(int)today.DayOfWeek);
        for (int i = 0; i < weekDayTexts.Length; i++)
        {
            DateTime day = sunday.AddDays(i);
            bool isToday = day == today;

            if (weekDayTexts[i] != null)
            {
                weekDayTexts[i].text = day.Day.ToString();
                weekDayTexts[i].color = isToday ? highlightTextColor : baseColors[i];
            }
            if (i < weekDayHighlights.Length && weekDayHighlights[i] != null)
            {
                weekDayHighlights[i].enabled = isToday;
            }
        }
    }
}
