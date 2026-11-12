namespace TakeOver.CompanyRules
{
    public sealed class StatChange
    {
        public CompanyStat stat { get; }
        public float delta { get; }

        public StatChange(CompanyStat stat, float delta)
        {
            this.stat = stat;
            this.delta = delta;
        }
    }
}
