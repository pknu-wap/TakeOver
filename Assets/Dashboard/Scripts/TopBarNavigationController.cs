using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;
using TakeOver.Events;

public sealed class TopBarNavigationController : MonoBehaviour
{
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private DashboardTab[] tabKinds = {
        DashboardTab.Home, DashboardTab.Operations, DashboardTab.Map, DashboardTab.Companies, DashboardTab.Npcs, DashboardTab.News, DashboardTab.Log
    };
    [SerializeField] private TopBarIndicatorController indicator;
    [SerializeField] private CompanyBrowserScreen browser;
    [SerializeField] private BrowserViewToggle viewToggle;
    [SerializeField] private FloatingMessage message;
    [SerializeField] private NpcStatusScreen statusScreen;
    [SerializeField] private EventSystemController eventController;
    [SerializeField] private NewsScreen newsScreen;
    [SerializeField] private TakeOver.Journal.JournalScreen journalScreen;
    private TakeOver.Operations.OperationsScreen operationsScreen;
    [SerializeField] private GameObject operationsSelection;
    [SerializeField] private TMPro.TMP_Text operationsLabel;
    private int currentTab;
    private TakeOver.Help.HelpScreen help;
    private int helpTab;
    public DashboardTab CurrentTab => tabKinds[currentTab];
    public void OpenHelp() => onTabClicked(helpTab);
    public void OpenCompanies() => Navigate(DashboardTab.Companies);

    public bool OpenNpc(string npcId)
    {
        var registry = FindFirstObjectByType<NpcStateRegistry>();
        if (registry == null || !registry.TryGetProfile(npcId, out _) || !Navigate(DashboardTab.Npcs)) return false;
        statusScreen.SelectNpc(npcId);
        return true;
    }

    private void Start()
    {
        if (tabKinds.Length != tabButtons.Length || System.Array.IndexOf(tabKinds, DashboardTab.Home) < 0)
        { Debug.LogError("상단 메뉴 버튼과 화면 식별자 연결을 확인해 주십시오.", this); return; }
        operationsSelection.SetActive(false);
        operationsLabel.color = Color.white;
        help = gameObject.AddComponent<TakeOver.Help.HelpScreen>();
        gameObject.AddComponent<TakeOver.Dashboard.StoryDeadlineBar>();
        var source = tabButtons[tabButtons.Length - 1];
        var helpButton = Instantiate(source, source.transform.parent, false);
        helpButton.name = "HelpTab";
        var copiedBadge = helpButton.GetComponent<TakeOver.Operations.DocumentCountBadge>();
        if (copiedBadge != null) copiedBadge.Hide();
        var label = helpButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null) label.text = "도움말";
        var legacy = helpButton.GetComponentInChildren<Text>();
        if (legacy != null) legacy.text = "도움말";
        TakeOver.UI.ButtonHoverFeedback.Attach(helpButton);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)source.transform.parent);
        helpTab = tabButtons.Length;
        System.Array.Resize(ref tabButtons, helpTab + 1);
        tabButtons[helpTab] = helpButton;
        System.Array.Resize(ref tabKinds, helpTab + 1);
        tabKinds[helpTab] = DashboardTab.Help;
        indicator.AddTab(helpButton);
        if (eventController == null) eventController = FindFirstObjectByType<EventSystemController>();
        if (eventController == null) Debug.LogWarning("Top bar could not find EventSystemController.", this);
        if (newsScreen == null) newsScreen = FindFirstObjectByType<NewsScreen>(FindObjectsInactive.Include);
        if (journalScreen == null) journalScreen = FindFirstObjectByType<TakeOver.Journal.JournalScreen>(FindObjectsInactive.Include);
        operationsScreen = FindFirstObjectByType<TakeOver.Operations.OperationsScreen>(FindObjectsInactive.Include);
        var dialogueTemplate = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
        if (dialogueTemplate != null)
        {
            var saves = Instantiate(source, source.transform.parent, false);
            saves.name = "SaveMenuButton";
            var badge = saves.GetComponent<TakeOver.Operations.DocumentCountBadge>();
            if (badge != null) badge.Hide();
            var saveLabel = saves.GetComponentInChildren<TMPro.TMP_Text>();
            if (saveLabel != null) saveLabel.text = "저장";
            var saveLegacy = saves.GetComponentInChildren<Text>();
            if (saveLegacy != null) saveLegacy.text = "저장";
            saves.onClick.RemoveAllListeners();
            saves.onClick.AddListener(() => TakeOver.SaveSystem.SaveSlotWindow.Show(dialogueTemplate));
        }
        var menuLayout = source.transform.parent.GetComponent<HorizontalLayoutGroup>();
        if (menuLayout != null) menuLayout.spacing = 8f;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)source.transform.parent);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => onTabClicked(index));
        }
        SelectHomeIndicator();
    }

    private void SelectHomeIndicator()
    {
        currentTab = System.Array.IndexOf(tabKinds, DashboardTab.Home);
        indicator.OnTabClicked(currentTab, false);
    }

    public void ReturnHome()
    {
        if (Navigate(DashboardTab.Home)) SelectHomeIndicator();
    }

    public bool Navigate(DashboardTab tab)
    {
        if (!TakeOver.Story.StoryDirector.CanNavigate(tab)) return false;
        int index = System.Array.IndexOf(tabKinds, tab);
        if (index < 0) return false;
        onTabClicked(index);
        return true;
    }

    private void onTabClicked(int index)
    {
        DashboardTab tab = tabKinds[index];
        if (!TakeOver.Story.StoryDirector.CanNavigate(tab)) return;
        if (tab == DashboardTab.Help)
        {
            if (operationsScreen != null) operationsScreen.close();
            if (newsScreen != null) newsScreen.close();
            if (journalScreen != null) journalScreen.close();
            browser.close(); statusScreen.Close(); help.Open();
            currentTab = index; indicator.OnTabClicked(index); return;
        }
        help.Close();
        if ((tab == DashboardTab.News && newsScreen == null) || (tab == DashboardTab.Log && journalScreen == null) || (tab == DashboardTab.Operations && operationsScreen == null))
        {
            message.Show("준비 중입니다");
            return;
        }
        if (newsScreen != null && tab != DashboardTab.News) newsScreen.close();
        if (journalScreen != null && tab != DashboardTab.Log) journalScreen.close();
        if (operationsScreen != null && tab != DashboardTab.Operations) operationsScreen.close();
        if (tab == DashboardTab.Operations)
        {
            browser.close();
            statusScreen.Close();
            operationsScreen.open();
        }
        else if (tab == DashboardTab.News)
        {
            browser.close();
            statusScreen.Close();
            newsScreen.open();
        }
        else if (tab == DashboardTab.Log)
        {
            browser.close();
            statusScreen.Close();
            journalScreen.open();
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
            SelectHomeIndicator();
        if (help == null || tabKinds[currentTab] != DashboardTab.Npcs || statusScreen.gameObject.activeInHierarchy) return;
        SelectHomeIndicator();
    }
}
