using System;           
using TMPro;            
using UnityEngine;
using UnityEngine.UI;   


public class CompanyRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;       
    [SerializeField] private TMP_Text priceText;      
    [SerializeField] private TMP_Text revenueText;   
    [SerializeField] private TMP_Text netProfitText;  
    [SerializeField] private TMP_Text stakeText;      
    [SerializeField] private Button button;           

    
    public void Setup(Company company, Action<Company> onClick)
    {
        
        nameText.text = company.definition.companyName;

        
        priceText.text = $"{CompanyUIUtil.GetDerived(company, CompanyDerivedStat.SHARE_PRICE):N2}";
        
        revenueText.text = $"{CompanyUIUtil.GetStat(company, CompanyStat.REVENUE):N0}";
        netProfitText.text = $"{CompanyUIUtil.GetDerived(company, CompanyDerivedStat.NET_PROFIT):N0}";
        
        stakeText.text = $"{CompanyUIUtil.GetDerived(company, CompanyDerivedStat.STAKE):0.#}%";

        button.onClick.RemoveAllListeners();              
        button.onClick.AddListener(() => onClick(company)); 
    }
}
