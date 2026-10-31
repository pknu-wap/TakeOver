using System.Collections;          // IEnumerator (코루틴)
using System.Collections.Generic;
using System.Linq;
using TakeOver.NPC;                 // GameTurnClock (하루가 넘어갈 때 알림)
using TMPro;
using UnityEngine;

// 기업 목록 (표 모양 큰 목록 + 오른쪽 작은 목록이 같이 쓴다).
// 큰 목록은 열 제목 버튼을 누르면 그 열 기준으로 정렬된다. 같은 제목을 또 누르면 오름/내림이 바뀐다.
public class CompanyListView : MonoBehaviour
{
    // 정렬할 수 있는 열. 열 제목 버튼 OnClick에 이 숫자를 넣는다.
    // 0 기업명, 1 업종, 2 주가, 3 매출, 4 순이익, 5 부채비율, 6 보유 지분
    private static readonly string[] ColumnNames = { "기업", "업종", "주가", "매출", "순이익", "부채비율", "보유 지분" };

    [Header("리스트")]
    [SerializeField] private CompanyRow rowPrefab;
    [SerializeField] private Transform rowParent;

    [Header("열 제목 (큰 목록만, 왼쪽부터 7개. 작은 목록은 비워 둠)")]
    [SerializeField] private TMP_Text[] headerLabels;

    [Header("처음 정렬")]
    [SerializeField] private int defaultSortColumn = 2;     // 2 = 주가
    [SerializeField] private bool defaultAscending = false; // false = 큰 값이 위

    [Header("상세 패널")]
    [SerializeField] private CompanyDetailPanel detailPanel;

    private Company[] companies;
    private readonly List<CompanyRow> rows = new List<CompanyRow>();

    private int sortColumn;
    private bool ascending;
    private GameTurnClock turnClock;

    // Start를 IEnumerator로 만들면 "코루틴"이 된다.
    // 한 프레임 쉬었다가 그리는 이유: 주가 같은 값은 팀원 코드가 각자의 Start()에서 계산하는데,
    // Unity는 Start() 순서를 보장하지 않아서 바로 그리면 0으로 나올 수 있다.
    IEnumerator Start()
    {
        companies = FindObjectsByType<Company>(FindObjectsSortMode.None);

        foreach (Transform child in rowParent)
        {
            Destroy(child.gameObject);
        }

        sortColumn = defaultSortColumn;
        ascending = defaultAscending;
        updateHeaderLabels();

        if (detailPanel != null)
        {
            detailPanel.OnTraded += refresh;   // 매수·매도하면 목록도 새로 고침
        }

        // 하루가 넘어가면(하루 마감) 숫자가 바뀌니까 목록도 새로 고친다.
        turnClock = FindFirstObjectByType<GameTurnClock>();
        if (turnClock != null)
        {
            turnClock.TurnAdvanced += handleTurnAdvanced;
        }

        yield return null;
        refresh();
    }

    void OnDestroy()
    {
        if (detailPanel != null)
        {
            detailPanel.OnTraded -= refresh;
        }
        if (turnClock != null)
        {
            turnClock.TurnAdvanced -= handleTurnAdvanced;
        }
    }

    // 지도 ↔ 리스트 전환으로 다시 켜질 때 최신 숫자로 다시 그린다.
    void OnEnable()
    {
        if (companies != null)
        {
            refresh();
        }
    }

    private void handleTurnAdvanced(int newTurn)
    {
        // 꺼져 있으면 코루틴을 못 돌리니까, 그때는 다음에 켜질 때 OnEnable이 새로 고친다.
        if (isActiveAndEnabled)
        {
            StartCoroutine(refreshNextFrame());
        }
    }

    // 하루 계산(팀원 코드)이 같은 알림에서 돌기 때문에, 계산이 끝난 다음 프레임에 그린다.
    private IEnumerator refreshNextFrame()
    {
        yield return null;
        refresh();
    }

    // 열 제목 버튼 OnClick에 연결 (숫자는 위 ColumnNames 순서)
    public void sortBy(int column)
    {
        if (column == sortColumn)
        {
            ascending = !ascending;   // 같은 열을 또 누르면 순서 뒤집기
        }
        else
        {
            sortColumn = column;
            // 이름·업종은 가나다순(오름)이 자연스럽고, 숫자는 큰 값이 위(내림)가 자연스럽다.
            ascending = column <= 1;
        }

        updateHeaderLabels();
        refresh();
    }

    public void refresh()
    {
        if (companies == null)
        {
            return;
        }

        IEnumerable<Company> valid = companies.Where(c => c != null && c.state != null);

        int index = 0;
        foreach (Company company in sort(valid))
        {
            // 줄이 모자랄 때만 새로 만든다 (보통 처음 한 번만)
            if (index >= rows.Count)
            {
                rows.Add(Instantiate(rowPrefab, rowParent));
            }

            CompanyRow row = rows[index];
            row.gameObject.SetActive(true);
            row.setup(company, detailPanel.show);
            index++;
        }

        // 남은 줄은 지우지 않고 꺼 둔다 (나중에 다시 쓸 수 있게)
        for (int i = index; i < rows.Count; i++)
        {
            rows[i].gameObject.SetActive(false);
        }
    }

    private IEnumerable<Company> sort(IEnumerable<Company> list)
    {
        switch (sortColumn)
        {
            case 0: return order(list, c => c.definition.companyName);
            case 1: return order(list, c => c.definition.industry ?? "");
            case 2: return order(list, c => CompanyUIUtil.getDerived(c, CompanyDerivedStat.SHARE_PRICE));
            case 3: return order(list, c => CompanyUIUtil.getStat(c, CompanyStat.REVENUE));
            case 4: return order(list, c => CompanyUIUtil.getDerived(c, CompanyDerivedStat.NET_PROFIT));
            case 5: return order(list, c => CompanyUIUtil.getDerived(c, CompanyDerivedStat.DEBT_RATIO));
            case 6: return order(list, c => CompanyUIUtil.getDerived(c, CompanyDerivedStat.STAKE));
            default: return list;
        }
    }

    // 오름/내림 방향만 다르고 나머지는 같아서 한 곳에 모았다.
    // <TKey> : 정렬 기준의 자료형(글자든 숫자든)을 그때그때 맞춰 쓰겠다는 뜻 (제네릭)
    private IEnumerable<Company> order<TKey>(IEnumerable<Company> list, System.Func<Company, TKey> key)
    {
        return ascending ? list.OrderBy(key) : list.OrderByDescending(key);
    }

    // 지금 정렬 중인 열 제목 옆에 ▲(오름) / ▼(내림)를 붙인다.
    private void updateHeaderLabels()
    {
        if (headerLabels == null)
        {
            return;
        }

        for (int i = 0; i < headerLabels.Length && i < ColumnNames.Length; i++)
        {
            if (headerLabels[i] == null)
            {
                continue;
            }

            string arrow = i == sortColumn ? (ascending ? " ▲" : " ▼") : "";
            headerLabels[i].text = ColumnNames[i] + arrow;
        }
    }
}
