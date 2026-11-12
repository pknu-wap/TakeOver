using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;
using TakeOver.Events;

public sealed class TopBarNavigationController : MonoBehaviour
{
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private DashboardTab[] tabKinds = {
        DashboardTab.Home, DashboardTab.Map, DashboardTab.Companies, DashboardTab.Npcs, DashboardTab.News, DashboardTab.Log
    };
    [SerializeField] private TopBarIndicatorController indicator;
    [SerializeField] private CompanyBrowserScreen browser;
    [SerializeField] private BrowserViewToggle viewToggle;
    [SerializeField] private FloatingMessage message;
    [SerializeField] private NpcStatusScreen statusScreen;
    [SerializeField] private EventSystemController eventController;
    [SerializeField] private NewsScreen newsScreen;
    private int currentTab;
    private TakeOver.Help.HelpScreen help;
    private int helpTab;
    public void OpenHelp() => onTabClicked(helpTab);

    private void Start()
    {
        if (tabKinds.Length != tabButtons.Length || System.Array.IndexOf(tabKinds, DashboardTab.Home) < 0)
        { Debug.LogError("상단 메뉴 버튼과 화면 식별자 연결을 확인해 주십시오.", this); return; }
        help = gameObject.AddComponent<TakeOver.Help.HelpScreen>();
        var source = tabButtons[tabButtons.Length - 1];
        var helpButton = Instantiate(source, source.transform.parent, false);
        helpButton.name = "HelpTab";
        var label = helpButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null) label.text = "도움말";
        var legacy = helpButton.GetComponentInChildren<Text>();
        if (legacy != null) legacy.text = "도움말";
        if (source.transform.parent.GetComponent<HorizontalLayoutGroup>() == null)
            ((RectTransform)helpButton.transform).anchoredPosition += Vector2.right * (((RectTransform)source.transform).rect.width / 2 + 45 + 18);
        ((RectTransform)helpButton.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,90);
        helpTab = tabButtons.Length;
        System.Array.Resize(ref tabButtons, helpTab + 1);
        tabButtons[helpTab] = helpButton;
        System.Array.Resize(ref tabKinds, helpTab + 1);
        tabKinds[helpTab] = DashboardTab.Help;
        indicator.AddTab(helpButton);
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
        DashboardTab tab = tabKinds[index];
        if (!TakeOver.Story.StoryDirector.CanNavigate(tab)) return;
        if (tab == DashboardTab.Help)
        {
            if (newsScreen != null) newsScreen.close();
            browser.close(); statusScreen.Close(); help.Open();
            currentTab = index; indicator.OnTabClicked(index); return;
        }
        help.Close();
        if ((tab == DashboardTab.News && newsScreen == null) || tab == DashboardTab.Log || tab == DashboardTab.Operations)
        {
            message.Show("준비 중입니다");
            return;
        }
        if (newsScreen != null && tab != DashboardTab.News) newsScreen.close();
        if (tab == DashboardTab.News)
        {
            browser.close();
            statusScreen.Close();
            newsScreen.open();
        }
        else if (tab == DashboardTab.Npcs)
        {
            browser.close();
            statusScreen.Open();
        }
        else
        {
            statusScreen.Close();
            if (tab == DashboardTab.Home)
            {
                browser.close();
                if (eventController != null) eventController.refreshAvailability();
            }
            else
            {
                browser.open();
                if (tab == DashboardTab.Map) viewToggle.showMap();
                else viewToggle.showList();
            }
        }
        currentTab = index;
        indicator.OnTabClicked(index);
    }

    private void LateUpdate()
    {
        if(currentTab == helpTab && help != null && !help.IsOpen)
        { currentTab=System.Array.IndexOf(tabKinds, DashboardTab.Home); indicator.OnTabClicked(currentTab); }
        if (help == null || tabKinds[currentTab] != DashboardTab.Npcs || statusScreen.gameObject.activeInHierarchy) return;
        currentTab = System.Array.IndexOf(tabKinds, DashboardTab.Home);
        indicator.OnTabClicked(currentTab);
    }
}
