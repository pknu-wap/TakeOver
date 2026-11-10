namespace TakeOver.CompanyRules
{
    public sealed class RuleContext
    {
        public int currentTurn { get; }
        public float marketScore { get; }

        public RuleContext(int currentTurn, float marketScore)
        {
            this.currentTurn = currentTurn;
            this.marketScore = marketScore;
        }
    }
}
