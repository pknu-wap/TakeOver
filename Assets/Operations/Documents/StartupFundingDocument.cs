using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Startups;

namespace TakeOver.Documents
{
    public sealed class StartupFundingDocument : IApprovalDocument
    {
        public const string typeId = "startup-funding";
        public const string amountName = "amount";

        private readonly StartupRecord record;
        private readonly Player player;
        private readonly Companies companies;

        public float Amount { get; private set; }

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "자금 요청";
        public string Author => "";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";

        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("요청 금액", $"{Amount:N0}원")
        };

        public IReadOnlyList<string> BodyLines => new[]
        {
            $"{TargetCompany.definition.companyName}의 현금이 바닥났습니다.",
            $"{Amount:N0}원을 지원하면 회사 현금이 같은 금액만큼 늘어납니다.",
            "거절하면 이 회사는 다시 자금을 요청하지 않습니다."
        };

        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

        public StartupFundingDocument(Company startup, StartupRecord record, Player player, Companies companies,
            float amount, int deadlineTurn)
        {
            TargetCompany = startup;
            this.record = record;
            this.player = player;
            this.companies = companies;
            Amount = amount;
            DeadlineTurn = deadlineTurn;
        }

        public static StartupFundingDocument restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company startup = context.companies.getCompany(saved.companyId);
            if (startup == null) return null;
            StartupRecord record = null;
            context.startupSystem?.tryGetRecord(saved.companyId, out record);
            return new StartupFundingDocument(startup, record, context.player, context.companies, 0f, saved.deadlineTurn);
        }

        public void SetField(string name, float value)
        {
            if (name != amountName) throw new ArgumentException("알 수 없는 문서 입력 칸입니다.", nameof(name));
            Amount = value;
        }

        public void SetChoice(string name, int key)
        {
            throw new ArgumentException("알 수 없는 문서 선택 칸입니다.", nameof(name));
        }

        public bool CanApprove(out string reason)
        {
            if (!isStillStartup())
            {
                reason = "이미 폐업했거나 상장한 회사입니다";
                return false;
            }
            if (player.state.cash < Amount)
            {
                reason = "보유 현금이 부족합니다";
                return false;
            }
            reason = "";
            return true;
        }

        public RuleResult OnApprove()
        {
            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.CASH, Amount));
            result.playerCash = -Amount;
            return result;
        }

        public RuleResult OnReject()
        {
            if (record != null) record.fundingRejected = true;
            return new RuleResult();
        }

        public DocumentSaveRecord ToSaveRecord()
        {
            return new DocumentSaveRecord
            {
                type = TypeId,
                companyId = TargetCompany.definition.companyID,
                deadlineTurn = DeadlineTurn,
                fields = new List<NameValue> { new NameValue { name = amountName, value = Amount } }
            };
        }

        private bool isStillStartup()
        {
            foreach (Company startup in companies.StartupList)
                if (startup == TargetCompany) return true;
            return false;
        }
    }
}
