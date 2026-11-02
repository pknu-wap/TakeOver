using TakeOver.Events;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

public class DaySkipController : MonoBehaviour
{
    [Header("Controller References")]
    [SerializeField] private CalendarController calendarController;
    [SerializeField] private DashboardController dashboardController;

    [Header("UI Reference")]
    [SerializeField] private Button daySkipButton;

    private void Awake()
    {
        if (daySkipButton != null)
        {
            daySkipButton.onClick.AddListener(OnClickNextDay);
        }

        if (dashboardController == null)
        {
            dashboardController = FindFirstObjectByType<DashboardController>();
        }
    }

    public void OnClickNextDay()
    {
        if (dashboardController != null)
        {
            dashboardController.ResetAP();
        }

        if (calendarController != null)
        {
            calendarController.AddDays(1);
        }

        GameTurnClock turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.AdvanceTurn();
        }
    }
}