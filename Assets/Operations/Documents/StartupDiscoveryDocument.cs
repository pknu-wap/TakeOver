using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public sealed class StartupDiscoveryDocument : IApprovalDocument
    {
        public const string typeId = "startup-discovery";

        private readonly Companies companies;
        private readonly List<DocumentInfo> infoRows;

        public string CompanyId { get; }
        public string CompanyName { get; }

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => CompanyId;
        public string Title { get; }
        public string Author { get; }
        public string ApproveLabel => "투자 검토";
        public string RejectLabel => "넘기기";
        public string DoneLabel => "";
        public IReadOnlyList<DocumentInfo> InfoRows => infoRows;

        public IReadOnlyList<string> BodyLines => new[]
        {
            "넘기면 다른 투자자가 가져가 다시 후보로 나오지 않습니다."
        };

        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

        public StartupDiscoveryDocument(Company startup, Companies companies, string author,
            int deadlineTurn, string outlook, bool isLastShow)
        {
            TargetCompany = startup;
            this.companies = companies;
            CompanyId = startup.definition.companyID;
            CompanyName = startup.definition.companyName;
            Author = author ?? "";
            DeadlineTurn = deadlineTurn;
            Title = isLastShow ? $"투자 후보 보고 · {CompanyName} (마지막)" : $"투자 후보 보고 · {CompanyName}";

            infoRows = new List<DocumentInfo>
            {
                new DocumentInfo("대상 회사", CompanyName),
                new DocumentInfo("업종", startup.definition.industry),
                new DocumentInfo("매출", MoneyText.Format(CompanyUIUtil.getStat(startup, CompanyStat.REVENUE))),
                new DocumentInfo("보유 자금", MoneyText.Format(CompanyUIUtil.getStat(startup, CompanyStat.CASH))),
                new DocumentInfo("직원 수", $"{CompanyUIUtil.getStat(startup, CompanyStat.EMPLOYEE_COUNT):N0}명"),
                new DocumentInfo("제시 기업가치",
                    MoneyText.Format(CompanyUIUtil.getDerived(startup, CompanyDerivedStat.FUNDAMENTAL_VALUE))),
                new DocumentInfo("성장 전망", outlook)
            };
        }

        private StartupDiscoveryDocument(DocumentSaveRecord saved, Companies companies)
        {
            this.companies = companies;
            CompanyId = saved.companyId ?? "";
            Title = saved.title ?? "발굴 보고";
            Author = saved.author ?? "";
            DeadlineTurn = saved.deadlineTurn;
            TargetCompany = companies != null ? companies.getCompany(CompanyId) : null;
            CompanyName = TargetCompany != null ? TargetCompany.definition.companyName : "";

            infoRows = new List<DocumentInfo>();
            if (saved.infoRows != null)
                foreach (NameText row in saved.infoRows) infoRows.Add(new DocumentInfo(row.label, row.value));
        }

        public static StartupDiscoveryDocument restore(DocumentSaveRecord saved, DocumentContext context) =>
            new StartupDiscoveryDocument(saved, context.companies);

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
            if (!isStillStartup())
            {
                reason = "이미 상장했거나 폐업한 회사입니다";
                return false;
            }
            if (TargetCompany.state.getStat(CompanyStat.PLAYER_SHARES) > 0f)
            {
                reason = "이미 투자한 회사입니다";
                return false;
            }
            reason = "";
            return true;
        }

        public RuleResult OnApprove() => new RuleResult();
        public RuleResult OnReject() => new RuleResult();

        public DocumentSaveRecord ToSaveRecord()
        {
            DocumentSaveRecord record = new DocumentSaveRecord
            {
                type = TypeId,
                companyId = CompanyId,
                deadlineTurn = DeadlineTurn,
                title = Title,
                author = Author,
                fields = new List<NameValue>(),
                infoRows = new List<NameText>()
            };
            foreach (DocumentInfo row in infoRows)
                record.infoRows.Add(new NameText { label = row.label, value = row.value });
            return record;
        }

        private bool isStillStartup()
        {
            if (TargetCompany == null || companies == null) return false;
            foreach (Company startup in companies.StartupList)
                if (startup == TargetCompany) return true;
            return false;
        }
    }
}
