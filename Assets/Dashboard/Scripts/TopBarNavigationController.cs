using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;
using TakeOver.Events;

public sealed class TopBarNavigationController : MonoBehaviour
{
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private TopBarIndicatorController indicator;
    [SerializeField] private CompanyBrowserScreen browser;
    [SerializeField] private BrowserViewToggle viewToggle;
    [SerializeField] private FloatingMessage message;
    [SerializeField] private NpcStatusScreen statusScreen;
    [SerializeField] private EventSystemController eventController;
    private int currentTab;

    private void Start()
    {
        if (eventController == null) eventController = FindFirstObjectByType<EventSystemController>();
        if (eventController == null) Debug.LogWarning("Top bar could not find EventSystemController.", this);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => onTabClicked(index));
        }
    }

    private void onTabClicked(int index)
    {
        if (index >= 4)
        {
            message.Show("준비 중입니다");
            return;
        }
        if (index == 3)
        {
            browser.close();
            statusScreen.Open();
        }
        else
        {
            statusScreen.Close();
            if (index == 0)
            {
                browser.close();
                if (eventController != null) eventController.refreshAvailability();
            }
            else
            {
                browser.open();
                if (index == 1) viewToggle.showMap();
                else viewToggle.showList();
            }
        }
        currentTab = index;
        indicator.OnTabClicked(index);
    }

    private void LateUpdate()
    {
        if (currentTab != 3 || statusScreen.gameObject.activeInHierarchy) return;
        currentTab = 0;
        indicator.OnTabClicked(0);
    }
}
