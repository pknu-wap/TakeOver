using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NewsScreen : MonoBehaviour, IPointerClickHandler
{
    private const float cardWidth = 350f;
    private const float hoveredCardWidth = 470f;
    private const float cardHeight = 600f;

    [SerializeField] private NewsFeed feed;
    [SerializeField] private GameObject cardsRoot;
    [SerializeField] private GameObject secretNewsPlaceholder;
    [SerializeField] private NewsRow[] newsCards;
    [SerializeField] private GameObject detailsOverlay;
    [SerializeField] private UnityEngine.UI.Text detailsTitle;
    [SerializeField] private UnityEngine.UI.Text detailsDate;
    [SerializeField] private UnityEngine.UI.Text detailsCompanies;
    [SerializeField] private UnityEngine.UI.Text detailsBody;
    [SerializeField] private UnityEngine.UI.ScrollRect detailsScroll;
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
    private MarketEventManager marketEventManager;
    private UnityEngine.UI.HorizontalLayoutGroup cardsLayout;
    private NewsEntry[] displayedEntries;
    private int selectedTurn;
    private int latestTurn;
    private bool isSecretNews;
    private bool detailsDismissRequested;
    private EscMenu suspendedEscMenu;

    private void Awake()
    {
        if (feed == null) feed = FindFirstObjectByType<NewsFeed>();
        turnClock = FindFirstObjectByType<GameTurnClock>();
        companies = FindFirstObjectByType<Companies>();
        marketEventManager = FindFirstObjectByType<MarketEventManager>();
        selectedTurn = turnClock.CurrentTurn;
        latestTurn = selectedTurn;

        feed.NewsChanged += handleNewsChanged;
        generalTabButton.onClick.AddListener(showGeneralNews);
        secretTabButton.onClick.AddListener(showSecretNews);
        nextDateButton.onClick.AddListener(showNextDate);
        previousDateButton.onClick.AddListener(showPreviousDate);
        displayedEntries = new NewsEntry[newsCards.Length];
        for (int i = 0; i < newsCards.Length; i++)
        {
            int cardIndex = i;
            newsCards[i].DetailsButton.onClick.AddListener(() => showDetails(displayedEntries[cardIndex]));
        }
        cardsLayout = cardsRoot.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
    }

    private void OnDestroy()
    {
        feed.NewsChanged -= handleNewsChanged;
    }

    private void OnEnable()
    {
        updateCardSizes();
        handleNewsChanged();
    }

    private void OnDisable()
    {
        closeDetails();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (cardsLayout != null && isActiveAndEnabled) updateCardSizes();
    }

    private void updateCardSizes()
    {
        int gaps = newsCards.Length - 1;
        Rect area = ((RectTransform)cardsRoot.transform).rect;
        float availableWidth = area.width - cardsLayout.padding.horizontal - cardsLayout.spacing * gaps;
        float availableHeight = area.height - cardsLayout.padding.vertical;
        float designRowWidth = cardWidth * gaps + hoveredCardWidth;
        float scale = Mathf.Max(0f, Mathf.Min(availableWidth / designRowWidth, availableHeight / cardHeight));
        float normalWidth = cardWidth * scale;
        float hoveredWidth = hoveredCardWidth * scale;
        float height = cardHeight * scale;

        foreach (NewsRow card in newsCards)
        {
            card.setSize(normalWidth, hoveredWidth, height);
        }
    }

    public void open()
    {
        gameObject.SetActive(true);
    }

    public void close()
    {
        gameObject.SetActive(false);
    }

    private void showDetails(NewsEntry entry)
    {
        string companyNames = getCompanyNames(entry);
        bool isMarketNews = marketEventManager.tryGetDefinition(entry.marketEventID, out MarketEventDefinition definition);
        bool hasArticle = isMarketNews && !string.IsNullOrWhiteSpace(definition.articleBody);

        detailsTitle.text = entry.title;
        detailsDate.text = formatDate(entry.turn);
        detailsCompanies.text = isMarketNews && entry.companyIDs.Count == 0 ? "전체 기업" : companyNames;
        detailsBody.supportRichText = hasArticle;
        if (hasArticle)
        {
            string articleTarget = entry.companyIDs.Count > 1 ? "해당 업체들" : detailsCompanies.text;
            detailsBody.text = definition.articleBody.Replace("{targetCompany}", articleTarget);
        }
        else
        {
            detailsBody.text = entry.description;
        }

        detailsOverlay.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(detailsScroll.content);
        detailsScroll.StopMovement();
        detailsScroll.verticalNormalizedPosition = 1f;
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        EscMenu escMenu = FindFirstObjectByType<EscMenu>();
        if (escMenu != null && escMenu.isActiveAndEnabled)
        {
            suspendedEscMenu = escMenu;
            suspendedEscMenu.enabled = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (detailsOverlay.activeSelf) detailsDismissRequested = true;
    }

    private void LateUpdate()
    {
        if (!detailsOverlay.activeSelf) return;

        // [일시정지 메뉴의 Update가 끝난 뒤 Esc 입력 복구]
        if (detailsDismissRequested || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            closeDetails();
        }
    }

    private void closeDetails()
    {
        detailsOverlay.SetActive(false);
        detailsDismissRequested = false;
        if (suspendedEscMenu != null)
        {
            suspendedEscMenu.enabled = true;
            suspendedEscMenu = null;
        }
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
        Array.Clear(displayedEntries, 0, displayedEntries.Length);
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
        secretNewsPlaceholder.SetActive(isSecretNews);
        if (isSecretNews) return;

        int cardIndex = 0;
        IReadOnlyList<NewsEntry> entries = feed.Entries;
        for (int i = entries.Count - 1; i >= 0 && cardIndex < newsCards.Length; i--)
        {
            NewsEntry entry = entries[i];
            if (entry.turn != selectedTurn) continue;

            Sprite thumbnailSprite = NewsImageLookup.getSprite(entry);
            displayedEntries[cardIndex] = entry;
            newsCards[cardIndex].setup(entry.title, getCompanyNames(entry), entry.description, thumbnailSprite: thumbnailSprite);
            newsCards[cardIndex].gameObject.SetActive(true);
            cardIndex++;
        }
        if (cardIndex == 0 && newsCards.Length > 0)
        {
            newsCards[0].setup("", "", "이 날짜에는 뉴스가 없습니다.", hasNews: false);
            newsCards[0].gameObject.SetActive(true);
            cardIndex++;
        }
        for (; cardIndex < newsCards.Length; cardIndex++)
        {
            newsCards[cardIndex].gameObject.SetActive(false);
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
