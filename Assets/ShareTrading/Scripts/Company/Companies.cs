using System.Collections.Generic;
using UnityEngine;
using System;

public class Companies : MonoBehaviour
{
    [SerializeField] private List<Company> companies = new List<Company>();
    private readonly List<Company> startups = new List<Company>();
    private readonly Dictionary<string, Company> companiesByID = new Dictionary<string, Company>();

    public IReadOnlyList<Company> CompanyList => companies;
    public IReadOnlyList<Company> StartupList => startups;

    private void Awake()
    {
        foreach (Company company in companies)
        {
            companiesByID.Add(company.definition.companyID, company);
        }
    }

    public bool RegisterStartup(Company startup)
    {
        if (startup == null || startup.definition == null || startup.state == null)
        {
            Debug.LogError("초기화되지 않은 스타트업은 등록할 수 없습니다.", this);
            return false;
        }

        string companyID = startup.definition.companyID;
        if (string.IsNullOrEmpty(companyID) || companiesByID.ContainsKey(companyID))
        {
            Debug.LogError($"스타트업 ID '{companyID}'가 비었거나 이미 쓰이고 있습니다.", this);
            return false;
        }

        startups.Add(startup);
        companiesByID.Add(companyID, startup);
        return true;
    }

    public bool PromoteStartup(Company startup)
    {
        if (startup == null || !startups.Remove(startup))
        {
            Debug.LogError("스타트업 목록에 없는 기업은 상장할 수 없습니다.", this);
            return false;
        }

        companies.Add(startup);
        return true;
    }

    public bool CloseStartup(Company startup)
    {
        if (startup == null || !startups.Remove(startup)) return false;

        companiesByID.Remove(startup.definition.companyID);
        Destroy(startup.gameObject);
        return true;
    }

    public bool tryGetCompany(string companyID, out Company company)
    {
        return companiesByID.TryGetValue(companyID ?? string.Empty, out company);
    }

    public Company getCompany(string companyID)
    {
        tryGetCompany(companyID, out Company company);
        return company;
    }

    public bool tryGetCompanyStake(string companyID, out float returnStake)
    {
        Company company = getCompany(companyID);
        if (company != null)
        {
            returnStake = company.state.getDerivedStat(CompanyDerivedStat.STAKE);
            return true;
        }
        returnStake = 0f;
        return false;
    }

    public bool tryGetCompanyStat(string companyID, string statName, out float returnStat)
    {
        Company company = getCompany(companyID);
        if (company != null)
            return company.state.tryGetStat(statName, out returnStat);

        returnStat = 0f;
        return false;
    }

    public bool tryGetCompanyBaseStat(string companyID, string statName, out float returnStat)
    {
        Company company = getCompany(companyID);
        if (company != null && Enum.TryParse(statName, out CompanyStat stat)
            && Enum.IsDefined(typeof(CompanyStat), stat))
        {
            returnStat = company.state.getBaseStat(stat);
            return true;
        }

        returnStat = 0f;
        return false;
    }
}
