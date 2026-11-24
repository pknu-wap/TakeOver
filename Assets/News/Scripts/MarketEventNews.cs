using System.Collections.Generic;
using UnityEngine;

public class MarketEventNews : MonoBehaviour
{
    private MarketEventManager marketEventManager;
    private NewsFeed newsFeed;
    private Companies companies;

    private void Awake()
    {
        marketEventManager = FindFirstObjectByType<MarketEventManager>();
        newsFeed = GetComponent<NewsFeed>();
        companies = FindFirstObjectByType<Companies>();
    }

    private void OnEnable()
    {
        marketEventManager.marketEventAdded += handleMarketEventAdded;
    }

    private void OnDisable()
    {
        marketEventManager.marketEventAdded -= handleMarketEventAdded;
    }

    private void handleMarketEventAdded(MarketEventManager.ActiveMarketEvent entry, int currentTurn)
    {
        var companyIDs = new List<string>();
        bool isAllCompanies = false;
        foreach (MarketEventDefinition.EventEffect effect in entry.definition.effects)
        {
            if (effect.companyTargetType == MarketEventCompanyTargetType.AllCompanies)
            {
                companyIDs.Clear();
                isAllCompanies = true;
                break;
            }
            else if (effect.companyTargetType == MarketEventCompanyTargetType.RandomCompany)
            {
                companyIDs.Add(entry.selectedCompanyID);
            }
            else
            {
                companyIDs.Add(effect.companyID);
            }
        }

        var companyNames = new List<string>();
        foreach (string id in companyIDs)
        {
            string companyName = companies.getCompany(id)?.definition.companyName;
            if (!string.IsNullOrEmpty(companyName) && !companyNames.Contains(companyName)) companyNames.Add(companyName);
        }
        string targets = isAllCompanies ? "전체 기업" : companyNames.Count > 0 ? string.Join(", ", companyNames) : "관련 기업";
        string description = entry.definition.description?.Replace("{targetCompany}", targets);
        newsFeed.addNews(currentTurn, entry.definition.MarketEventName, companyIDs, description, entry.definition.MarketEventID);
    }
}
