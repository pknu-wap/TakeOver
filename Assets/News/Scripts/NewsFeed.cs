using System;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;

// [최근 5일 뉴스 보관, 화면을 닫아도 날짜 변경 수신]
public partial class NewsFeed : MonoBehaviour
{
    public const int retainedDayCount = 5;
    internal const string MissionNewsPrefix = "내일 시작에 투자 목표 판정: ";
    private const string FormerMarketHeadline = "임시 시장 이벤트 1";
    private const string MarketBriefingHeadline = "기관투자자, 동방물산·하늘식품 실적에 주목";
    private const string MarketBriefingBody = "기관투자자들이 동방물산과 하늘식품의 정기 실적 설명회에 참석해 장기 거래처와 다음 분기 수주 계획을 물었다.";

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
    public void addNews(int turn, string title, IEnumerable<string> companyIDs, string description)
    {
        NewsEntry entry = new NewsEntry { turn = turn, title = title, description = describeNews(title, description) };
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

    private static string describeNews(string title, string description)
    {
        if (title == FormerMarketHeadline) return MarketBriefingBody;
        if (!string.IsNullOrWhiteSpace(description)) return description;
        if (title?.StartsWith(MissionNewsPrefix, StringComparison.Ordinal) == true)
            return "관련 기업의 주요 주주들이 협의 일정을 앞두고 지분 구도를 살피고 있다. 시장에서는 누가 이번 논의에 참여할지 주목하고 있다.";
        return title switch
        {
            "기준금리 0.25%p 인상… 건설 업계 자금 부담 커져" => "은행들이 새 대출 조건을 검토하면서 건설사들은 만기가 돌아오는 사업 자금의 차환 일정을 다시 짜고 있다.",
            "원자재 가격 상승, 제조업 원가 압박" => "철강과 포장재 공급사들이 새 견적을 내기 시작했다. 장기 납품 계약을 맺은 제조사들은 기존 단가로 생산을 이어갈 수 있을지 살피고 있다.",
            "소비 심리 회복세, 식품·유통주 강세" => "주말 장을 보러 나온 손님이 늘자 대형마트들은 주요 식품 납품업체에 다음 행사 물량을 문의했다.",
            "물류 수요 급증에 창고 자동화 투자 확대" => "당일 배송 주문이 몰리면서 물류업체들은 새 분류 설비를 시험 가동하고 있다. 주요 화주는 물량을 안정적으로 처리할 거점을 찾고 있다.",
            _ => ""
        };
    }

    private void handleTurnAdvanced(int newTurn)
    {
        entries.RemoveAll(entry => entry.turn < newTurn - retainedDayCount + 1);
        if (useDummyNews)
        {
            addDummyNews(newTurn, dummyRandom.Next(0, 3));
        }
        NewsChanged?.Invoke();
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
            addNews(turn, title, ids, "");
        }
    }
}
