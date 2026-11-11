namespace TakeOver.CompanyRules
{
    public interface ICompanyRule
    {
        int PeriodTurns { get; }
        RuleResult Evaluate(Company company, RuleContext context);
    }
}
