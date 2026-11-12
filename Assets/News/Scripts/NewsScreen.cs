using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 뉴스 화면. 상단바 "뉴스" 탭으로 열고 닫는다. 최신 날짜가 위로 오게 날짜별로 묶어서 보여준다.
public class NewsScreen : MonoBehaviour
{
    [Tooltip("비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private NewsFeed feed;

    [Tooltip("Scroll View/Viewport/Content")]
    [SerializeField] private RectTransform listContent;

    [SerializeField] private Text dateHeaderPrefab;
    [SerializeField] private NewsRow newsRowPrefab;

    [Tooltip("뉴스가 하나도 없을 때 보여줄 안내")]
    [SerializeField] private GameObject emptyMessage;

    [Header("시작 날짜 (DayPanel과 같은 값)")]
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;

    private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

    private Companies companies;

    // 이 화면이 만든 줄만 기억해 두고 지운다. (List Content 안의 다른 오브젝트는 건드리지 않음)
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    // 마지막으로 그린 뒤에 뉴스가 바뀌었는지. 바뀌지 않았으면 다시 열 때 새로 그리지 않는다.
    private bool isDirty = true;

    void Awake()
    {
        if (feed == null)
        {
            feed = FindFirstObjectByType<NewsFeed>();
        }

        if (feed != null)
        {
            feed.NewsChanged += markDirty;
        }
    }

    void OnDestroy()
    {
        if (feed != null)
        {
            feed.NewsChanged -= markDirty;
        }
    }

    void OnEnable()
    {
        if (isDirty)
        {
            rebuild();
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

    private void markDirty()
    {
        isDirty = true;
        if (isActiveAndEnabled)
        {
            rebuild();
        }
    }

    private void rebuild()
    {
        isDirty = false;

        foreach (GameObject spawned in spawnedRows)
        {
            Destroy(spawned);
        }
        spawnedRows.Clear();

        IReadOnlyList<NewsEntry> entries = feed != null ? feed.Entries : null;
        bool hasNews = entries != null && entries.Count > 0;
        emptyMessage.SetActive(!hasNews);
        if (!hasNews)
        {
            return;
        }

        if (companies == null)
        {
            companies = FindFirstObjectByType<Companies>();
        }

        // 뉴스는 턴 순서대로 쌓여 있으므로 뒤에서부터 읽으면 최신 날짜가 위로 온다.
        int lastTurn = -1;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            NewsEntry entry = entries[i];
            if (entry.turn != lastTurn)
            {
                Text header = Instantiate(dateHeaderPrefab, listContent);
                header.text = formatDate(entry.turn);
                spawnedRows.Add(header.gameObject);
                lastTurn = entry.turn;
            }

            NewsRow row = Instantiate(newsRowPrefab, listContent);
            row.setup(entry.title, getCompanyNames(entry));
            spawnedRows.Add(row.gameObject);
        }
    }

    private string formatDate(int turn)
    {
        DateTime date = new DateTime(startYear, startMonth, startDay).AddDays(turn - 1);
        return $"{date:yyyy.MM.dd} ({DayNames[(int)date.DayOfWeek]})";
    }

    private string getCompanyNames(NewsEntry entry)
    {
        if (entry.companyIDs.Count == 0)
        {
            return "";
        }

        List<string> names = new List<string>();
        foreach (string id in entry.companyIDs)
        {
            if (companies != null && companies.tryGetCompany(id, out Company company))
            {
                names.Add(company.definition.companyName);
            }
            else
            {
                names.Add(id);
            }
        }
        return string.Join(", ", names);
    }
}
