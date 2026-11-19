using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Documents;

namespace TakeOver.Startups
{
    public sealed class StartupContractDocument : IApprovalDocument
    {
        private static readonly float[] stakeOptions = { .2f, .3f, .4f };
        private readonly Player player;
        private readonly Companies companies;
        private readonly float companyValue;
        private readonly DocumentField stake;

        public string TypeId => "startup-investment";
        public string Title => "벤처 캐피탈 투자 계약서";
        public string Author => $"{TargetCompany.definition.companyName} 대표";
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string ApproveLabel => "확정";
        public string RejectLabel => "거절";
        public string DoneLabel => "계약 완료";
        public IReadOnlyList<string> BodyLines => Array.Empty<string>();
        public IReadOnlyList<DocumentField> Fields { get; }
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();
        public float investment => calculateInvestment(companyValue, stake.value);
        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("기업가치", $"{companyValue:N0}"),
            new DocumentInfo("투자금", $"{investment:N0}"),
            new DocumentInfo("직원 배치", "배치하지 않음")
        };

        public StartupContractDocument(Company company, Player player, Companies companies, int turn)
        {
            TargetCompany = company;
            this.player = player;
            this.companies = companies;
            DeadlineTurn = turn;
            companyValue = company.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE);
            stake = new DocumentField("stake", "지분율", stakeOptions[0], stakeOptions);
            Fields = new[] { stake };
        }

        public static float calculateInvestment(float companyValue, float stakeRatio) =>
            (float)((double)stakeRatio * companyValue / (1d - stakeRatio));

        public void SetField(string name, float value)
        {
            if (name != stake.name || Array.IndexOf(stakeOptions, value) < 0)
                throw new ArgumentException("계약서의 지분율 선택이 올바르지 않습니다.");
            stake.value = value;
        }

        public void SetChoice(string name, int key) =>
            throw new ArgumentException("지원 인원 배치는 아직 연결되지 않았습니다.", nameof(name));

        public bool CanApprove(out string reason)
        {
            bool registered = false;
            foreach (Company startup in companies.StartupList)
                if (startup == TargetCompany) { registered = true; break; }
            if (!registered)
            {
                reason = "계약할 수 있는 스타트업이 아닙니다";
                return false;
            }
            if (TargetCompany.state.getStat(CompanyStat.PLAYER_SHARES) > 0f)
            {
                reason = "이미 투자한 스타트업입니다";
                return false;
            }
            reason = player.state.cash >= investment ? "" : "보유자금이 부족합니다";
            return reason.Length == 0;
        }

        public RuleResult OnApprove()
        {
            var result = new RuleResult { playerCash = -investment };
            result.stats.Add(new StatChange(CompanyStat.CASH, investment));
            result.stats.Add(new StatChange(CompanyStat.PLAYER_SHARES, TargetCompany.state.totalShares * stake.value));
            return result;
        }

        public RuleResult OnReject() => new RuleResult();

        public DocumentSaveRecord ToSaveRecord() => new DocumentSaveRecord
        {
            type = TypeId,
            companyId = TargetCompany.definition.companyID,
            fields = new List<NameValue> { new NameValue { name = stake.name, value = stake.value } }
        };
    }
}
