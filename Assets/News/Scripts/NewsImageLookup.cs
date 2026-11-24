using System;
using System.Linq;
using UnityEngine;

internal static class NewsImageLookup
{
    public static Sprite getSprite(NewsEntry entry)
    {
        int imageEventID = entry.marketEventID;
        if (imageEventID == 0)
        {
            imageEventID = entry.title switch
            {
                "기준금리 0.25%p 인상… 건설 업계 자금 부담 커져" => 201,
                "원자재 가격 상승, 제조업 원가 압박" or "소비 심리 회복세, 식품·유통주 강세" => 6,
                "물류 수요 급증에 창고 자동화 투자 확대" => 23,
                string title when title.StartsWith(NewsFeed.MissionNewsPrefix, StringComparison.Ordinal) => 1,
                _ => 0,
            };
        }

        string fileName = imageEventID switch
        {
            1 or 3 => "MarketEvent001_EarningsBriefing",
            2 or 14 or 19 or 22 or 204 => "MarketEvent002_LargeContract",
            4 => "MarketEvent004_ManagementCorruption",
            5 => "MarketEvent005_KeyStaffDeparture",
            6 or 208 => "MarketEvent006_FundingSuccess",
            7 or 9 => "MarketEvent007_PublicContract",
            8 or 11 => "MarketEvent008_ApartmentSales",
            10 => "MarketEvent010_SiteCollapse",
            12 or 13 => "MarketEvent012_RetailEntry",
            15 or 16 => "MarketEvent015_MaterialContamination",
            17 or 18 or 20 or 21 => "MarketEvent017_ComponentCertification",
            23 or 24 or 25 => "MarketEvent023_WarehouseAutomation",
            26 => "MarketEvent026_WarehouseFire",
            201 => "MarketEvent201_InterestRateIncrease",
            202 or 207 => "MarketEvent202_207_Downturn",
            203 => "MarketEvent203_ShippingReroute",
            205 or 206 => "MarketEvent205_OilPriceSurge",
            _ => null,
        };

        return fileName == null ? null : Resources.LoadAll<Sprite>("News/" + fileName).Single();
    }
}
