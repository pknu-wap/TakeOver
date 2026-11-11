using UnityEngine;

namespace TakeOver.CompanyRules
{
    public sealed class ChangeApplier
    {
        private readonly Player player;

        public ChangeApplier(Player player)
        {
            this.player = player;
        }

        public void Apply(RuleResult result, Company company)
        {
            foreach (StatChange change in result.stats)
            {
                company.state.addStat(change.stat, change.delta);
                Debug.Log($"[CompanyRules] {company.definition.companyName} {change.stat} delta={change.delta} value={company.state.getStat(change.stat)}");
            }
            foreach (StateChange change in result.states)
            {
                CompanyStatus before = company.state.status;
                company.state.status = change.newStatus;
                Debug.Log($"[CompanyRules] {company.definition.companyName} status {before} -> {change.newStatus}");
            }
            if (result.playerCash != 0f)
            {
                player.state.addCash(result.playerCash);
                Debug.Log($"[CompanyRules] {company.definition.companyName} playerCash delta={result.playerCash} value={player.state.cash}");
            }
        }
    }
}
