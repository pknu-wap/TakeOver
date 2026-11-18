using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.NPC;
using TakeOver.Startups;

namespace TakeOver.Documents
{
    public sealed class StartupListingDocument : IApprovalDocument
    {
        public const string typeId = "startup-listing";
        public const string reportTypeId = "startup-listing-report";

        private readonly StartupRecord record;
        private readonly Companies companies;
        private readonly StartupSettings settings;
        private readonly GameTurnClock clock;

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "상장 추진";
        public string Author => "";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";

        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("업종", TargetCompany.definition.industry)
        };

        public IReadOnlyList<string> BodyLines => new[]
        {
            $"{TargetCompany.definition.companyName}이(가) 상장 준비를 마쳤습니다.",
            "승인하면 바로 상장해 매매 목록에 오릅니다.",
            "거절하면 한 달 뒤 다시 올라옵니다."
        };

        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

        public StartupListingDocument(Company startup, StartupRecord record, Companies companies,
            StartupSettings settings, GameTurnClock clock, int deadlineTurn)
        {
            TargetCompany = startup;
            this.record = record;
            this.companies = companies;
            this.settings = settings;
            this.clock = clock;
            DeadlineTurn = deadlineTurn;
        }

        public static StartupListingDocument restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company startup = context.companies.getCompany(saved.companyId);
            if (startup == null || context.startupSystem == null) return null;
            context.startupSystem.tryGetRecord(saved.companyId, out StartupRecord record);
            return new StartupListingDocument(startup, record, context.companies, context.startupSystem.Settings,
                context.clock, saved.deadlineTurn);
        }

        public static bool listNow(Companies companies, Company startup, StartupSettings settings)
        {
            if (!companies.PromoteStartup(startup)) return false;
            startup.state.addStat(CompanyStat.DIVIDEND_RATIO,
                settings.listedDividendRatio - startup.state.getBaseStat(CompanyStat.DIVIDEND_RATIO));
            return true;
        }

        public static bool isStartup(Companies companies, Company company)
        {
            foreach (Company startup in companies.StartupList)
                if (startup == company) return true;
            return false;
        }

        public void SetField(string name, float value)
        {
            throw new ArgumentException("알 수 없는 문서 입력 칸입니다.", nameof(name));
        }

        public void SetChoice(string name, int key)
        {
            throw new ArgumentException("알 수 없는 문서 선택 칸입니다.", nameof(name));
        }

        public bool CanApprove(out string reason)
        {
            bool valid = isStartup(companies, TargetCompany);
            reason = valid ? "" : "이미 폐업했거나 상장한 회사입니다";
            return valid;
        }

        public RuleResult OnApprove()
        {
            listNow(companies, TargetCompany, settings);
            return new RuleResult();
        }

        public RuleResult OnReject()
        {
            if (record != null && clock != null) record.listingRetryTurn = clock.CurrentTurn + settings.listingRetryTurns;
            return new RuleResult();
        }

        public DocumentSaveRecord ToSaveRecord()
        {
            return new DocumentSaveRecord
            {
                type = TypeId,
                companyId = TargetCompany.definition.companyID,
                deadlineTurn = DeadlineTurn,
                fields = new List<NameValue>()
            };
        }
    }
}
