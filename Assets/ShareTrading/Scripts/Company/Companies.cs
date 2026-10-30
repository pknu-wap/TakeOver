using System.Collections.Generic;
using UnityEngine;

public class Companies : MonoBehaviour
{
    [SerializeField] private List<Company> companies = new List<Company>();
    private readonly Dictionary<string, Company> companiesByID = new Dictionary<string, Company>();

    public IReadOnlyList<Company> CompanyList => companies;

    private void Awake()
    {
        foreach (Company company in companies)
        {
            companiesByID.Add(company.definition.companyID, company);
        }
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
}
