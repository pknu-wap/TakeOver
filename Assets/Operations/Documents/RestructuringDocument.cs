using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class RestructuringDocument : AcquisitionAgendaDocument
    {
        public const string typeId = "acquisition-restructuring";
        public override string TypeId => typeId;
        public override string Title => "구조조정 승인 요청";

        public RestructuringDocument(Company company, Companies companies, AcquisitionOperationsService operations,
            GameTurnClock clock, int deadlineTurn) : base(company, companies, operations, clock, deadlineTurn, "구조조정 규모") { }

        public static RestructuringDocument Restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company company = context.companies.getCompany(saved.companyId);
            if (company == null) return null;
            var document = new RestructuringDocument(company, context.companies,
                context.acquisitionOperations, context.clock, saved.deadlineTurn);
            document.RestoreRows(saved);
            return document;
        }

        private float Employees => TargetCompany.state.getBaseStat(CompanyStat.EMPLOYEE_COUNT);
        private int NextEmployees => Mathf.Max(0, Mathf.RoundToInt(Employees) - Mathf.RoundToInt(Employees * Settings.restructuringEmployeeRatios[scale.value.Value]));
        private float ReputationPenalty => Settings.restructuringReputationLosses[scale.value.Value];

        protected override IReadOnlyList<DocumentInfo> BuildInfoRows()
        {
            float profit = TargetCompany.state.getBaseStat(CompanyStat.REVENUE) - TargetCompany.state.getBaseStat(CompanyStat.COST);
            float cost = TargetCompany.state.getBaseStat(CompanyStat.COST);
            float saving = scale.value.HasValue ? CompanyCalculationMath.safeDivide((Employees - NextEmployees) * operations.CostPerEmployee,
                CompanyCalculationMath.safeDenominator(cost, TargetCompany.state.initialCost)) : 0f;
            float reputationLoss = scale.value.HasValue ? Mathf.Max(
                Mathf.Min(TargetCompany.state.getBaseStat(CompanyStat.REPUTATION_B2C), ReputationPenalty),
                Mathf.Min(TargetCompany.state.getBaseStat(CompanyStat.REPUTATION_B2B), ReputationPenalty)) : 0f;
            return new[]
            {
                new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
                new DocumentInfo("순이익", MoneyText.Format(profit)),
                new DocumentInfo("연속 적자", $"{operations.LossWeeks(TargetCompany)}주째"),
                new DocumentInfo("직원 수", scale.value.HasValue ? $"{Employees:N0}명 → {NextEmployees:N0}명" : "—"),
                new DocumentInfo("비용 절감", scale.value.HasValue ? Degree(saving, Settings.restructuringSavingThresholds) : "—"),
                new DocumentInfo("평판 영향", !scale.value.HasValue ? "—" : reputationLoss < Settings.restructuringReputationThreshold ? "조금 하락" : "크게 하락")
            };
        }

        protected override RuleResult ApprovedChanges()
        {
            var result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.EMPLOYEE_COUNT, NextEmployees - Employees));
            foreach (CompanyStat stat in new[] { CompanyStat.REPUTATION_B2C, CompanyStat.REPUTATION_B2B })
                result.stats.Add(new StatChange(stat, -Mathf.Min(Mathf.Max(0f, TargetCompany.state.getBaseStat(stat)), ReputationPenalty)));
            operations.SetLossWeeks(TargetCompany, 0);
            return result;
        }

        protected override void Rejected() => operations.SetLossWeeks(TargetCompany, 0);
    }
}
