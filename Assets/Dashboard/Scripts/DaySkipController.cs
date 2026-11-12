using TakeOver.Events;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

public class DaySkipController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private Button daySkipButton;
    public Button DaySkipButton => daySkipButton;

    private void Awake()
    {
        if (daySkipButton != null)
        {
            daySkipButton.onClick.AddListener(OnClickNextDay);
        }
    }

    public void OnClickNextDay()
    {
        GameTurnClock turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.AdvanceTurn();
        }
    }
}
