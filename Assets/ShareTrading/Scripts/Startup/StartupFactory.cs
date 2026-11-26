using System.Collections.Generic;
using UnityEngine;

namespace TakeOver.Startups
{
    public static class StartupFactory
    {
        public static StartupRecord createRecord(StartupSettings settings, System.Random random,
            string companyId, string companyName)
        {
            StartupSettings.IndustryTemplate template = settings.industries[random.Next(settings.industries.Length)];

            float revenue = Mathf.Round(pick(random, template.revenue) * 100f) / 100f;
            return new StartupRecord
            {
                companyId = companyId,
                companyName = companyName,
                industry = template.industry,
                coreValue = template.coreValue,
                description = template.description,
                baseCompanyValue = Mathf.Round(pick(random, template.baseCompanyValue)),
                revenue = revenue,
                cost = Mathf.Round(revenue * template.initialCostRatio * 100f) / 100f,
                cash = Mathf.Round(pick(random, template.cash) * 100f) / 100f,
                debt = Mathf.Round(pick(random, template.debt) * 100f) / 100f,
                tangibleAssets = Mathf.Round(pick(random, template.tangibleAssets) * 100f) / 100f,
                intangibleAssets = Mathf.Round(pick(random, template.intangibleAssets) * 100f) / 100f,
                growthRate = pick(random, template.growthRate),
                employeeCount = pick(random, template.employees),
                revenueHistory = new List<float> { revenue }
            };
        }

        public static Company createCompany(StartupRecord record, Transform parent)
        {
            CompanyDefinition definition = ScriptableObject.CreateInstance<CompanyDefinition>();
            definition.name = record.companyId;
            definition.companyID = record.companyId;
            definition.companyName = record.companyName;
            definition.industry = record.industry;
            definition.coreValue = record.coreValue;
            definition.description = record.description;
            definition.totalShares = record.totalShares;
            definition.initialBaseCompanyValue = record.baseCompanyValue;
            definition.initialRevenue = record.revenue;
            definition.initialCost = record.cost;
            definition.initialCash = record.cash;
            definition.initialDebt = record.debt;
            definition.initialTangibleAssets = record.tangibleAssets;
            definition.initialIntangibleAssets = record.intangibleAssets;
            definition.initialPlayerShares = record.playerShares;
            definition.growthRate = record.growthRate;
            definition.initialEmployeeCount = record.employeeCount;
            definition.dividendRatio = 0f;

            GameObject startupObject = new GameObject(record.companyId);
            startupObject.SetActive(false);
            startupObject.transform.SetParent(parent, false);

            Company company = startupObject.AddComponent<Company>();
            company.initialize(definition);
            startupObject.SetActive(true);
            return company;
        }

        private static float pick(System.Random random, Vector2 range)
        {
            return range.x + (float)random.NextDouble() * (range.y - range.x);
        }

        private static int pick(System.Random random, Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return random.Next(min, max + 1);
        }
    }
}
