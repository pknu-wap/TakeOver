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

    void Start()
    {
        companies = FindObjectsByType<Company>(FindObjectsSortMode.None);

        
        foreach (Transform child in rowParent)
        {
            Destroy(child.gameObject);
        }

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(SortOptions.ToList());

        sortDropdown.onValueChanged.AddListener(_ => Refresh());

        Refresh();
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