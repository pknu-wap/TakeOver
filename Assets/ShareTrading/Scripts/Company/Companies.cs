using System.Collections.Generic;
using UnityEngine;

public class Companies : MonoBehaviour
{
    [SerializeField] private List<Company> companies = new List<Company>();

    public IReadOnlyList<Company> CompanyList => companies;

    public Company getCompany(string companyID)
    {
        foreach (Company company in companies)
        {
            if (company != null && company.definition != null && company.definition.companyID == companyID)
            {
                return company;
            }
        }

        return null;
    }
}
