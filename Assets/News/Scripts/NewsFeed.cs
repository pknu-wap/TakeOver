using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

// 지금까지 나온 뉴스를 모아 두는 곳. 화면이 닫혀 있어도 하루 마감을 들어야 하므로 항상 켜진 오브젝트에 붙인다.
public class NewsFeed : MonoBehaviour
{
    [Tooltip("시장 이벤트를 연결하기 전까지 하루 마감마다 임시 뉴스를 만들지")]
    [SerializeField] private bool useDummyNews = true;

    [SerializeField] private string[] dummyTitles =
    {
        "기준금리 0.25%p 인상… 건설 업계 자금 부담 커져",
        "원자재 가격 상승, 제조업 원가 압박",
        "소비 심리 회복세, 식품·유통주 강세",
        "물류 수요 급증에 창고 자동화 투자 확대",
    };

    private readonly List<NewsEntry> entries = new List<NewsEntry>();

    // 게임 전체가 같이 쓰는 UnityEngine.Random을 건드리지 않도록 임시 뉴스 전용 난수를 따로 쓴다.
    private readonly System.Random dummyRandom = new System.Random();

    private GameTurnClock turnClock;
    private Companies companies;

    public IReadOnlyList<NewsEntry> Entries => entries;

    public event Action NewsChanged;

    void OnEnable()
    {
        turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.TurnAdvanced += handleTurnAdvanced;
        }
        else
        {
            Debug.LogWarning("[NewsFeed] GameTurnClock을 찾지 못해서 하루 마감 때 뉴스를 만들 수 없어요.", this);
        }
    }

    void OnDisable()
    {
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= handleTurnAdvanced;
        }
    }

    // 시장 이벤트 쪽에서 뉴스를 넣을 때 부르는 함수. 턴 순서대로 넣는다고 가정한다.
    public void addNews(int turn, string title, IEnumerable<string> companyIDs)
    {
        NewsEntry entry = new NewsEntry { turn = turn, title = title };
        if (companyIDs != null)
        {
            foreach (string id in companyIDs)
            {
                if (!string.IsNullOrEmpty(id) && !entry.companyIDs.Contains(id))
                {
                    entry.companyIDs.Add(id);
                }
            }
        }

        entries.Add(entry);
        NewsChanged?.Invoke();
    }

    private void handleTurnAdvanced(int newTurn)
    {
        if (useDummyNews)
        {
            addDummyNews(newTurn, dummyRandom.Next(0, 3));
        }
    }

    // 에디터 전용: Inspector에서 NewsFeed 컴포넌트 오른쪽 ⋮ 메뉴로 실행
    [ContextMenu("테스트 뉴스 3개 추가")]
    private void addTestNews()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NewsFeed] 테스트 뉴스는 플레이 중에만 추가할 수 있어요.", this);
            return;
        }

        int turn = turnClock != null ? turnClock.CurrentTurn : 1;
        addDummyNews(turn, 3);
    }

    private void addDummyNews(int turn, int count)
    {
        if (dummyTitles == null || dummyTitles.Length == 0)
        {
            return;
        }

        if (companies == null)
        {
            companies = FindFirstObjectByType<Companies>();
        }

        for (int i = 0; i < count; i++)
        {
            string title = dummyTitles[dummyRandom.Next(dummyTitles.Length)];
            List<string> ids = new List<string>();
            if (companies != null && companies.CompanyList.Count > 0)
            {
                Company company = companies.CompanyList[dummyRandom.Next(companies.CompanyList.Count)];
                ids.Add(company.definition.companyID);
            }
            addNews(turn, title, ids);
        }
    }
}
