using System.Collections.Generic;
using UnityEngine;

public class CompanyListController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject companyItemPrefab;

    [Header("Display Settings")]
    [SerializeField] private int maxDisplayCount = 6;

    [Header("Data Assets")]
    [SerializeField] private List<CompanyDefinition> companyDefinitions;

    private void Start()
    {
        GenerateCompanyList();
    }

    public void GenerateCompanyList()
    {
        if (contentParent == null || companyItemPrefab == null)
            return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        int count = Mathf.Min(maxDisplayCount, companyDefinitions.Count);

        for (int i = 0; i < count; i++)
        {
            CompanyDefinition company = companyDefinitions[i];
            if (company == null) continue;

            GameObject itemObj = Instantiate(companyItemPrefab, contentParent);
            CompanyItemUI itemUI = itemObj.GetComponent<CompanyItemUI>();

            if (itemUI != null)
            {
                float stake = 0f;
                if (company.totalShares > 0)
                {
                    stake = ((float)company.initialPlayerShares / company.totalShares) * 100f;
                }

                float value = company.initialBaseCompanyValue;
                float changeRate = 0.0f; // 등락률 연동 시스템 구축 전 기본값 0

                itemUI.SetData(company.companyName, stake, value, changeRate);
            }
        }
    }
}