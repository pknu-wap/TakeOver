using System.Collections;          // [추가] IEnumerator (코루틴)
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


public class CompanyListView : MonoBehaviour
{
    [Header("리스트")]
    [SerializeField] private CompanyRow rowPrefab;
    [SerializeField] private Transform rowParent;

    [Header("정렬")]
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("상세 패널")]
    [SerializeField] private CompanyDetailPanel detailPanel;


    private static readonly string[] SortOptions = { "주가순", "매출순", "순이익순", "부채비율순", "지분율순" };


    private Company[] companies;


    private readonly List<CompanyRow> rows = new List<CompanyRow>();

    // [변경] void Start() → IEnumerator Start()
    // Start를 IEnumerator로 만들면 "코루틴"이 된다. 코루틴은 중간에 yield return으로 잠깐 쉬었다가
    // 다음 프레임에 이어서 실행할 수 있는 함수다.
    IEnumerator Start()
    {
        companies = FindObjectsByType<Company>(FindObjectsSortMode.None);


        foreach (Transform child in rowParent)
        {
            Destroy(child.gameObject);
        }

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(SortOptions.ToList());

        sortDropdown.onValueChanged.AddListener(_ => Refresh());

        // [추가] "상세 패널에서 거래되면 내 Refresh도 불러줘"라고 알림 명단에 등록
        detailPanel.OnTraded += Refresh;

        // [추가] 한 프레임 기다렸다가 목록을 그린다.
        // 이유: 주가·순이익 같은 파생 수치는 팀원 코드가 각자의 Start()에서 계산한다.
        //       Unity는 여러 스크립트의 Start() 순서를 보장하지 않아서, 이 리스트가 먼저 실행되면
        //       아직 계산 전이라 0으로 표시된다.
        //       yield return null = "이번 프레임은 여기서 멈추고 다음 프레임에 이어서 해줘".
        //       다음 프레임이면 모든 Start()가 끝난 뒤라 계산된 값이 들어 있다.
        yield return null;

        Refresh();
    }

    // [추가] 이 오브젝트가 삭제될 때 알림 명단에서 빠진다.
    // 빠지지 않으면, 삭제된 리스트의 Refresh를 패널이 계속 부르려다 에러가 날 수 있다.
    void OnDestroy()
    {
        if (detailPanel != null)
        {
            detailPanel.OnTraded -= Refresh;
        }
    }


    void OnEnable()
    {
        if (companies != null)
        {
            Refresh();
        }
    }


    public void Refresh()
    {

        IEnumerable<Company> valid = companies.Where(c => c != null && c.state != null);


        int index = 0;
        foreach (Company company in Sort(valid))
        {
            // 줄이 모자랄 때만 새로 만든다 (보통 처음 한 번만 여기로 들어옴)
            if (index >= rows.Count)
            {
                rows.Add(Instantiate(rowPrefab, rowParent));
            }

            CompanyRow row = rows[index];
            row.gameObject.SetActive(true);
            row.Setup(company, detailPanel.Show);
            index++;
        }

        // 3) [변경] 쓰고 남은 줄은 지우지 않고 꺼 둔다 (나중에 다시 쓸 수 있게)
        for (int i = index; i < rows.Count; i++)
        {
            rows[i].gameObject.SetActive(false);
        }
    }

    // 드롭다운에서 고른 기준으로 내림차순(큰 값이 위) 정렬한 결과를 돌려준다
    private IEnumerable<Company> Sort(IEnumerable<Company> list)
    {
        switch (sortDropdown.value)
        {
            case 0: return list.OrderByDescending(c => CompanyUIUtil.GetDerived(c, CompanyDerivedStat.SHARE_PRICE));
            case 1: return list.OrderByDescending(c => CompanyUIUtil.GetStat(c, CompanyStat.REVENUE));
            case 2: return list.OrderByDescending(c => CompanyUIUtil.GetDerived(c, CompanyDerivedStat.NET_PROFIT));
            case 3: return list.OrderByDescending(c => CompanyUIUtil.GetDerived(c, CompanyDerivedStat.DEBT_RATIO));
            case 4: return list.OrderByDescending(c => CompanyUIUtil.GetDerived(c, CompanyDerivedStat.STAKE));
            default: return list;
        }
    }
}