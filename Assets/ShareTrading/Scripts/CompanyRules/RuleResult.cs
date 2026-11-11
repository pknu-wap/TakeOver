using System.Collections.Generic;

namespace TakeOver.CompanyRules
{
    public sealed class RuleResult
    {
        public readonly List<StatChange> stats = new List<StatChange>();
        public readonly List<StateChange> states = new List<StateChange>();
        public float playerCash;
    }
}
