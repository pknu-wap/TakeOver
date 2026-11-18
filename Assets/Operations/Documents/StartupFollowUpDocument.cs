using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Documents
{
    public sealed class StartupFollowUpDocument : IApprovalDocument
    {
        public const string typeId = "startup-follow-up";
        public const string amountName = "amount";

        private readonly StartupRecord record;
        private readonly Companies companies;

        public float Amount { get; private set; }

        public string TypeId => typeId;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public string Title => "후속 투자";
        public string Author => "";
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";

        // 처리한 순간의 지분 미리보기. 처리 뒤 기록에서 다시 열어도 숫자가 다시 계산되지 않게 고정한다.
        private string resolvedStakeText;

        public IReadOnlyList<DocumentInfo> InfoRows => new[]
        {
            new DocumentInfo("대상 회사", TargetCompany.definition.companyName),
            new DocumentInfo("투자금", $"{Amount:N0}원"),
            new DocumentInfo("내 지분", resolvedStakeText ?? stakeText())
        };

        private string stakeText()
        {
            return $"{stakePercent(TargetCompany.state.totalShares):0.##}% → {stakePercent(newTotalShares()):0.##}%";
        }

        public IReadOnlyList<string> BodyLines => new[]
        {
            $"{TargetCompany.definition.companyName}이(가) {Amount:N0}원의 후속 투자를 받으려 합니다.",
            "승인하면 회사 현금이 늘고, 새 주식이 발행되어 내 지분율이 낮아집니다.",
            "내 주식 수와 보유 현금은 바뀌지 않습니다."
        };

        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

        public StartupFollowUpDocument(Company startup, StartupRecord record, Companies companies,
            float amount, int deadlineTurn)
        {
            TargetCompany = startup;
            this.record = record;
            this.companies = companies;
            Amount = amount;
            DeadlineTurn = deadlineTurn;
        }

        public static StartupFollowUpDocument restore(DocumentSaveRecord saved, DocumentContext context)
        {
            Company startup = context.companies.getCompany(saved.companyId);
            if (startup == null) return null;
            StartupRecord record = null;
            context.startupSystem?.tryGetRecord(saved.companyId, out record);
            return new StartupFollowUpDocument(startup, record, context.companies, 0f, saved.deadlineTurn);
        }

        // 투자 뒤 전체 주식 수. 기업가치 V, 투자금 I일 때 새 전체 = 지금 전체 × (V + I) ÷ V
        // 플레이어 주식 수 P는 그대로라서 새 지분율 = P ÷ 새 전체 = 현재 지분율 × V ÷ (V + I)
        // 주식 수는 정수라서 반올림한다 (1,000주 기준 오차 최대 약 0.05%p)
        private int newTotalShares()
        {
            int total = TargetCompany.state.totalShares;
            float value = companyValue();
            if (value <= 0f) return total;
            return Mathf.Max(total, Mathf.RoundToInt(total * (value + Amount) / value));
        }

        private float companyValue()
        {
            return TargetCompany.state.getDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE);
        }

        private float stakePercent(int totalShares)
        {
            if (totalShares <= 0) return 0f;
            return TargetCompany.state.getBaseStat(CompanyStat.PLAYER_SHARES) * 100f / totalShares;
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
            if (!StartupListingDocument.isStartup(companies, TargetCompany))
            {
                reason = "이미 폐업했거나 상장한 회사입니다";
                return false;
            }
            if (companyValue() <= 0f)
            {
                reason = "기업가치가 0 이하라 투자를 받을 수 없습니다";
                return false;
            }
            reason = "";
            return true;
        }

        public RuleResult OnApprove()
        {
            resolvedStakeText = stakeText();
            int newTotal = newTotalShares();
            TargetCompany.state.totalShares = newTotal;
            if (record != null) record.totalShares = newTotal;

            RuleResult result = new RuleResult();
            result.stats.Add(new StatChange(CompanyStat.CASH, Amount));
            return result;
        }

        public RuleResult OnReject()
        {
            resolvedStakeText = stakeText();
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
    }
}
