using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

public class NewsScreen : MonoBehaviour
{
    [SerializeField] private NewsFeed feed;
    [SerializeField] private GameObject cardsRoot;
    [SerializeField] private NewsRow[] newsCards;
    [SerializeField] private Text dateText;
    [SerializeField] private Button generalTabButton;
    [SerializeField] private Button secretTabButton;
    [SerializeField] private GameObject generalTabUnderline;
    [SerializeField] private GameObject secretTabUnderline;
    [SerializeField] private Button nextDateButton;
    [SerializeField] private Button previousDateButton;
    [SerializeField] private Image[] dateIndicators;

    [Header("시작 날짜 (DayPanel과 같은 값)")]
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;

    private static readonly string[] dayNames = { "일", "월", "화", "수", "목", "금", "토" };
    private static readonly Color activeColor = new Color32(17, 35, 61, 255);
    private static readonly Color inactiveColor = new Color32(160, 171, 183, 255);

    private GameTurnClock turnClock;
    private Companies companies;
    private int selectedTurn;
    private int latestTurn;
    private bool isSecretNews;

    private void Awake()
    {
        if (feed == null) feed = FindFirstObjectByType<NewsFeed>();
        turnClock = FindFirstObjectByType<GameTurnClock>();
        companies = FindFirstObjectByType<Companies>();
        selectedTurn = turnClock.CurrentTurn;
        latestTurn = selectedTurn;

        feed.NewsChanged += handleNewsChanged;
        generalTabButton.onClick.AddListener(showGeneralNews);
        secretTabButton.onClick.AddListener(showSecretNews);
        nextDateButton.onClick.AddListener(showNextDate);
        previousDateButton.onClick.AddListener(showPreviousDate);
    }

    private void OnDestroy()
    {
        feed.NewsChanged -= handleNewsChanged;
    }

    private void OnEnable()
    {
        handleNewsChanged();
    }

    public void open()
    {
        gameObject.SetActive(true);
    }

    public void close()
    {
        gameObject.SetActive(false);
    }

    private void handleNewsChanged()
    {
        if (latestTurn != turnClock.CurrentTurn)
        {
            latestTurn = turnClock.CurrentTurn;
            selectedTurn = latestTurn;
        }
        if (isActiveAndEnabled) refresh();
    }

    private void showGeneralNews()
    {
        isSecretNews = false;
        refresh();
    }

    private void showSecretNews()
    {
        isSecretNews = true;
        refresh();
    }

    private void showNextDate()
    {
        selectedTurn++;
        refresh();
    }

    private void showPreviousDate()
    {
        selectedTurn--;
        refresh();
    }

    private void refresh()
    {
        int currentTurn = turnClock.CurrentTurn;
        dateText.text = formatDate(selectedTurn);
        nextDateButton.interactable = selectedTurn < currentTurn;
        previousDateButton.interactable = selectedTurn > Mathf.Max(1, currentTurn - NewsFeed.retainedDayCount + 1);

        for (int i = 0; i < dateIndicators.Length; i++)
        {
            int turn = currentTurn - i;
            dateIndicators[i].gameObject.SetActive(turn >= 1);
            dateIndicators[i].color = turn == selectedTurn ? activeColor : inactiveColor;
        }

        generalTabUnderline.SetActive(!isSecretNews);
        secretTabUnderline.SetActive(isSecretNews);
        generalTabButton.GetComponentInChildren<Text>().color = isSecretNews ? inactiveColor : activeColor;
        secretTabButton.GetComponentInChildren<Text>().color = isSecretNews ? activeColor : inactiveColor;
        cardsRoot.SetActive(!isSecretNews);
        if (isSecretNews) return;

        foreach (NewsRow card in newsCards) card.setup("", "", "뉴스가 없습니다");

        int cardIndex = 0;
        IReadOnlyList<NewsEntry> entries = feed.Entries;
        for (int i = entries.Count - 1; i >= 0 && cardIndex < newsCards.Length; i--)
        {
            NewsEntry entry = entries[i];
            if (entry.turn != selectedTurn) continue;

            newsCards[cardIndex].setup(entry.title, getCompanyNames(entry), entry.description);
            cardIndex++;
        }
    }

    private string formatDate(int turn)
    {
        DateTime date = new DateTime(startYear, startMonth, startDay).AddDays(turn - 1);
        return $"{date:yyyy.MM.dd} ({dayNames[(int)date.DayOfWeek]})";
    }

    private string getCompanyNames(NewsEntry entry)
    {
        if (entry.companyIDs.Count == 0) return "";

        List<string> names = new List<string>();
        foreach (string id in entry.companyIDs)
        {
            if (companies != null && companies.tryGetCompany(id, out Company company))
                names.Add(company.definition.companyName);
            else
                names.Add(id);
        }
        return string.Join(", ", names);
    }
}
