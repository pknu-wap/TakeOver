using System;
using System.Collections.Generic;

namespace TakeOver.Startups
{
    [Serializable]
    public sealed class StartupRecord
    {
        public string companyId;
        public string companyName;
        public string industry;
        public string coreValue;
        public string description;

        public int totalShares = 1000;
        public float baseCompanyValue;
        public float revenue;
        public float cost;
        public float cash;
        public float debt;
        public float tangibleAssets;
        public float intangibleAssets;
        public float growthRate;
        public int employeeCount;

        public List<float> revenueHistory = new List<float>();

        public bool fundingRejected;
    }
}
