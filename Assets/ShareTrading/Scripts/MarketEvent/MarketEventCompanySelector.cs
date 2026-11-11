using System.Collections.Generic;
using TakeOver.SaveSystem;
using UnityEngine;

public class MarketEventCompanySelector
{
    private readonly Companies companies;

    public MarketEventCompanySelector(Companies companies)
    {
        this.companies = companies;
    }

    public string selectCompanyID()
    {
        // 활성 여부 구분 전까지 전체 회사에서 추첨
        IReadOnlyList<Company> candidates = companies.CompanyList;
        // float 반올림으로 최댓값이 반환돼도 유효한 인덱스 유지
        int index = Mathf.Min((int)FixedDaySeed.range(FixedDaySeed.seed, 0f, candidates.Count),
            candidates.Count - 1);
        return candidates[index].definition.companyID;
    }
}
