using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;

namespace TakeOver.Documents
{
    public sealed class FacilityInvestmentDocument : AcquisitionAgendaDocument
    {
        public const string typeId = "acquisition-facility-investment";
        public override string TypeId => typeId;
        public override string Title => "설비 투자 승인 요청";

        public FacilityInvestmentDocument(Company company, Companies companies, AcquisitionOperationsService operations,
            GameTurnClock clock, int deadlineTurn) : base(company, companies, operations, clock, deadlineTurn, "투자 규모") { }

        public static FacilityInvestmentDocument Restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company company = context.companies.getCompany(saved.companyId);
            if (company == null) return null;
            var document = new FacilityInvestmentDocument(company, context.companies,
                context.acquisitionOperations, context.clock, saved.deadlineTurn);
            document.RestoreRows(saved);
            return document;
        }

        private float Amount => TargetCompany.state.getBaseStat(CompanyStat.CASH) * Settings.facilityInvestmentRatios[scale.value.Value];
        private float Bonus => CompanyCalculationMath.safeDivide(Amount,
            CompanyCalculationMath.safeDenominator(TargetCompany.state.getBaseStat(CompanyStat.REVENUE), TargetCompany.state.initialRevenue)) * Settings.facilityGrowthCoefficient;

        internal static float Liquidity(Company company) => CompanyCalculationMath.safeDivide(company.state.getStat(CompanyStat.CASH),
            CompanyCalculationMath.safeDenominator(company.state.getStat(CompanyStat.COST), company.state.initialCost));

        protected override IReadOnlyList<DocumentInfo> BuildInfoRows()
        {
            float cash = TargetCompany.state.getBaseStat(CompanyStat.CASH);
            return new[]
            {
                new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
                new DocumentInfo("현금 여유", $"비용 {Liquidity(TargetCompany):0.#}주치"),
                new DocumentInfo("투자 금액", scale.value.HasValue ? MoneyText.Format(Amount) : "—"),
                new DocumentInfo("회사 현금", scale.value.HasValue ? $"{MoneyText.Format(cash)} → {MoneyText.Format(cash - Amount)}" : MoneyText.Format(cash) + " → —"),
                new DocumentInfo("예상 효과", scale.value.HasValue ? Degree(Bonus, Settings.facilityEffectThresholds) : "—")
            };
        }

        protected override RuleResult ApprovedChanges()
        {
            float amount = Amount;
            operations.AddInvestment(TargetCompany, clock.CurrentTurn, Bonus);
            var result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.CASH, -amount));
            result.stats.Add(new StatChange(CompanyStat.TANGIBLE_ASSETS, amount));
            return result;
        }
    }
}
