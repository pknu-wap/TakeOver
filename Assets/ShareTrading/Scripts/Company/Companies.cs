using System.Collections.Generic;
using UnityEngine;

public class Companies : MonoBehaviour
{
    [SerializeField] private List<Company> companies = new List<Company>();

    public IReadOnlyList<Company> CompanyList => companies;
}
