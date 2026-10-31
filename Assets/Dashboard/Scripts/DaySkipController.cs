using UnityEngine;
using UnityEngine.UI;
using TakeOver.Events;

public class DaySkipController : MonoBehaviour
{
    [Header("Controller References")]
    [SerializeField] private CalendarController calendarController;
    [SerializeField] private EventSystemController eventSystemController;

    [Header("UI Reference")]
    [SerializeField] private Button daySkipButton;

    private void Awake()
    {
        if (daySkipButton != null)
        {
            daySkipButton.onClick.AddListener(OnClickNextDay);
        }

        if (eventSystemController == null)
        {
            eventSystemController = FindFirstObjectByType<EventSystemController>();
        }
    }

    public void OnClickNextDay()
    {
        ResetAP();

        if (calendarController != null)
        {
            calendarController.AddDays(1);
        }

        if (eventSystemController != null)
        {
            int nextDay = eventSystemController.getCurrentDay() + 1;
            Debug.Log($"[DaySkip] 다음 날로 전환 시도: {nextDay}일차");

            eventSystemController.onDayStarted(nextDay);
        }

        RefreshDashboard();
    }

    private void ResetAP()
    {
        Debug.Log("AP가 리셋되었습니다.");
    }

    private void RefreshDashboard()
    {
        Debug.Log("다음 날 대시보드 UI를 갱신합니다.");
    }
}