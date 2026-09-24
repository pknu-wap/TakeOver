using System.Collections.Generic;
using UnityEngine;

public class TradeCompanySelect : MonoBehaviour
{
    [SerializeField] private List<Company> companies; // 나중에 Company 풀을 한번에 들고있는 객체, 스크립트가 있어야 편리할듯
    [SerializeField] TMPro.TMP_Dropdown dropdown;

    public Company selectedCompany => companies[dropdown.value];
}
