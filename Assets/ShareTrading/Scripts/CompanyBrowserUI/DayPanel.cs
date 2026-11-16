using System;
using TakeOver.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DayPanel : MonoBehaviour
{
    [Header("날짜")]
    [SerializeField] private TMP_Text dayCountText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;

    [Header("하루 마감")]
    [SerializeField] private Button endDayButton;

    private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

    private GameTurnClock turnClock;

    void Awake()
    {
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
    }

    private void refresh()
    {
        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        DateTime today = new DateTime(startYear, startMonth, startDay).AddDays(turn - 1);

        if (dayCountText != null)
        {
            dayCountText.text = $"{turn}일째";
        }
        if (dateText != null)
        {
            dateText.text = $"{today:yyyy.MM.dd} ({DayNames[(int)today.DayOfWeek]})";
        }
    }
}
