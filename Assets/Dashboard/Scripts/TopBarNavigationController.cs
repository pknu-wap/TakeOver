using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;

public sealed class TopBarNavigationController : MonoBehaviour
{
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private TopBarIndicatorController indicator;
    [SerializeField] private CompanyBrowserScreen browser;
    [SerializeField] private BrowserViewToggle viewToggle;
    [SerializeField] private FloatingMessage message;
    [SerializeField] private NpcStatusScreen statusScreen;
    private int currentTab;

    private void Start()
    {
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
            if (index == 0) browser.close();
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
