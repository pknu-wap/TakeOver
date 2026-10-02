using System.Collections.Generic;   


public static class CompanyUIUtil
{
    

    //   UI가 계산보다 먼저 실행될 수도 있으니, 에러가 나면 0을 돌려주도록 감싼다.
    

    public static float getDerived(Company company, CompanyDerivedStat stat)
    {
        
        if (company == null || company.state == null)
        {
            return 0f;
        }

        // try { 시도할 코드 } catch (에러종류) { 그 에러가 났을 때 실행할 코드 }
        try
        {
            return company.state.getDerivedStat(stat);
        }
        catch (KeyNotFoundException)
        {
            return 0f;   
        }
    }

    
    public static float getStat(Company company, CompanyStat stat)
    {
        if (company == null || company.state == null)
        {
            return 0f;
        }
        return company.state.getStat(stat);
    }
}
