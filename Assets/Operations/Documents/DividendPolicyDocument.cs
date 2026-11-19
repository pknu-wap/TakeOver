using System;
using System.Collections.Generic;
using System.IO;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public sealed class DividendPolicyDocument : IApprovalDocument
    {
        public const string typeId = "dividend-policy";
        private readonly List<DocumentField> fields;
        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "배당 정책";
        public string Author => "";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";
        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("현재 배당 비율", $"{TargetCompany.state.getBaseStat(CompanyStat.DIVIDEND_RATIO):P0}"),
            new DocumentInfo("선택한 비율", $"{fields[0].value:P0}")
        };
        public IReadOnlyList<DocumentField> Fields => fields;
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();
        public IReadOnlyList<string> BodyLines => new[]
        {
            TargetCompany.definition.companyName,
            $"현재 배당 비율: {TargetCompany.state.getBaseStat(CompanyStat.DIVIDEND_RATIO):P0}",
            $"선택한 비율: {fields[0].value:P0}"
        };

        public DividendPolicyDocument(Company company, int deadlineTurn, DocumentSettings settings)
        {
            TargetCompany = company;
            DeadlineTurn = deadlineTurn;
            fields = new List<DocumentField>
            {
                new DocumentField("dividendRatio", "배당 비율",
                    company.state.getBaseStat(CompanyStat.DIVIDEND_RATIO), settings.dividendRatioOptions)
            };
        }

        internal static DividendPolicyDocument CreateFromSaveRecord(DocumentSaveRecord record, DocumentContext context)
        {
            if (string.IsNullOrEmpty(record.companyId) || record.fields.Count != 1 ||
                record.fields[0].name != "dividendRatio" || record.fields[0].value < 0 || record.fields[0].value > 1)
                throw new InvalidDataException("배당 정책 문서의 회사 또는 배당 비율이 올바르지 않습니다.");

            return new DividendPolicyDocument(context.companies.getCompany(record.companyId), record.deadlineTurn, context.settings);
        }

        public void SetField(string name, float value)
        {
            if (name != fields[0].name) throw new ArgumentException("알 수 없는 문서 입력 칸입니다.", nameof(name));
            fields[0].value = value;
        }

        public void SetChoice(string name, int key)
        {
            throw new ArgumentException("알 수 없는 문서 선택 칸입니다.", nameof(name));
        }

        public bool CanApprove(out string reason)
        {
            reason = "";
            return true;
        }

        public RuleResult OnApprove()
        {
            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.DIVIDEND_RATIO,
                fields[0].value - TargetCompany.state.getBaseStat(CompanyStat.DIVIDEND_RATIO)));
            return result;
        }

        public RuleResult OnReject() => new RuleResult();

        public DocumentSaveRecord ToSaveRecord()
        {
            return new DocumentSaveRecord
            {
                type = TypeId,
                companyId = TargetCompany.definition.companyID,
                deadlineTurn = DeadlineTurn,
                fields = new List<NameValue> { new NameValue { name = fields[0].name, value = fields[0].value } }
            };
        }
    }
}
