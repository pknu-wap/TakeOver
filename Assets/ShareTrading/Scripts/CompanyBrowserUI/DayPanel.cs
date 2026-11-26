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

    [Header("하루 마감")]
    [SerializeField] private Button endDayButton;


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
            turnClock.TurnRestored += handleTurnAdvanced;
        }
        refresh();
    }

    void OnDisable()
    {
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= handleTurnAdvanced;
            turnClock.TurnRestored -= handleTurnAdvanced;
        }
    }

    public void endDay()
    {
        if (turnClock == null)
        {
            Debug.LogWarning("GameTurnClock을 찾을 수 없습니다.", this);
            return;
        }
        turnClock.AdvanceWithTransition();
    }

    private void handleTurnAdvanced(int newTurn)
    {
        refresh();
    }

    private void refresh()
    {
        int turn = turnClock != null ? turnClock.CurrentTurn : 1;

        if (dayCountText != null)
        {
            dayCountText.text = $"{turn}일째";
        }
        if (dateText != null)
        {
            dateText.text = GameCalendar.Format(turn);
        }
    }
}
