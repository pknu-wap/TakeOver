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

    
    public void setup(Company company, Action<Company> onClick)
    {
        
        nameText.text = company.definition.companyName;

        
        priceText.text = $"{CompanyUIUtil.getDerived(company, CompanyDerivedStat.SHARE_PRICE):N2}";
        
        revenueText.text = $"{CompanyUIUtil.getStat(company, CompanyStat.REVENUE):N0}";
        netProfitText.text = $"{CompanyUIUtil.getDerived(company, CompanyDerivedStat.NET_PROFIT):N0}";
        
        stakeText.text = $"{CompanyUIUtil.getDerived(company, CompanyDerivedStat.STAKE):0.#}%";

        button.onClick.RemoveAllListeners();              
        button.onClick.AddListener(() => onClick(company)); 
    }
}
