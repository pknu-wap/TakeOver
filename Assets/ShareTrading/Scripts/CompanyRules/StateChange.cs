namespace TakeOver.CompanyRules
{
    public sealed class StateChange
    {
        public CompanyStatus newStatus { get; }

        public StateChange(CompanyStatus newStatus)
        {
            this.newStatus = newStatus;
        }
    }
}
