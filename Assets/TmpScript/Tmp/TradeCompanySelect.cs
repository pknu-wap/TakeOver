using System.Collections.Generic;
using UnityEngine;

public class TradeCompanySelect : MonoBehaviour
{
    [SerializeField] private List<Company> companies;
    [SerializeField] TMPro.TMP_Dropdown dropdown;

    public Company selectedCompany => companies[dropdown.value];
}
