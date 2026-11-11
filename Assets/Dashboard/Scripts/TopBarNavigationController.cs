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
    [SerializeField] private NewsScreen newsScreen;
    private int currentTab;

    private void Start()
    {
        if (eventController == null) eventController = FindFirstObjectByType<EventSystemController>();
        if (eventController == null) Debug.LogWarning("Top bar could not find EventSystemController.", this);
        if (newsScreen == null) newsScreen = FindFirstObjectByType<NewsScreen>(FindObjectsInactive.Include);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => onTabClicked(index));
        }
    }

    private void onTabClicked(int index)
    {
        if (index >= 5 || (index == 4 && newsScreen == null))
        {
            message.Show("준비 중입니다");
            return;
        }
        if (newsScreen != null && index != 4) newsScreen.close();
        if (index == 4)
        {
            browser.close();
            statusScreen.Close();
            newsScreen.open();
        }
        else if (index == 3)
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
