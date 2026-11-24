using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.CompanyRules;
using TakeOver.NPC;

namespace TakeOver.Documents
{
    public abstract class AcquisitionAgendaDocument : IApprovalDocument
    {
        public const string scaleName = "scale";
        protected readonly Companies companies;
        protected readonly AcquisitionOperationsService operations;
        protected readonly GameTurnClock clock;
        protected DocumentSettings Settings => operations.Settings;
        protected readonly DocumentChoiceField scale;
        private IReadOnlyList<DocumentInfo> resolvedRows;
        public abstract string TypeId { get; }
        public abstract string Title { get; }
        public string Author => $"{TargetCompany.definition.companyName} 대표";
        public string BodyId => "generic";
        public string DedupKey => TargetCompany.definition.companyID;
        public Company TargetCompany { get; }
        public int DeadlineTurn { get; }
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";
        public IReadOnlyList<string> BodyLines => Array.Empty<string>();
        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields { get; }
        public IReadOnlyList<DocumentInfo> InfoRows => resolvedRows ?? BuildInfoRows();

        protected AcquisitionAgendaDocument(Company company, Companies companies, AcquisitionOperationsService operations,
            GameTurnClock clock, int deadlineTurn, string label)
        {
            TargetCompany = company;
            this.companies = companies;
            this.operations = operations;
            this.clock = clock;
            DeadlineTurn = deadlineTurn;
            scale = new DocumentChoiceField(scaleName, label, () => new[]
            {
                new DocumentChoice { key = 0, label = "작게" },
                new DocumentChoice { key = 1, label = "보통" },
                new DocumentChoice { key = 2, label = "크게" }
            });
            ChoiceFields = new[] { scale };
        }

        protected abstract IReadOnlyList<DocumentInfo> BuildInfoRows();
        protected abstract RuleResult ApprovedChanges();
        protected virtual void Rejected() { }

        protected static string Degree(float value, float[] thresholds) => value < thresholds[0] ? "작음" : value < thresholds[1] ? "보통" : "큼";

        public void SetField(string name, float value) => throw new ArgumentException("알 수 없는 입력 칸입니다.", nameof(name));
        public void SetChoice(string name, int key)
        {
            if (name != scaleName || key < 0 || key > 2) throw new ArgumentException("안건 규모가 올바르지 않습니다.");
            scale.value = key;
        }

        public bool CanApprove(out string reason)
        {
            if (!companies.CompanyList.Contains(TargetCompany) || TargetCompany.state.status == CompanyStatus.Delisted ||
                TargetCompany.state.status == CompanyStatus.Liquidation)
            {
                reason = "운영 대상 회사가 없습니다";
                return false;
            }
            if (TargetCompany.state.totalShares <= 0 || TargetCompany.state.getBaseStat(CompanyStat.PLAYER_SHARES) /
                TargetCompany.state.totalShares < Settings.takeoverRatio)
            {
                reason = "인수 지분이 부족합니다";
                return false;
            }
            reason = scale.value.HasValue ? "" : "규모를 선택해 주세요";
            return reason.Length == 0;
        }

        public RuleResult OnApprove()
        {
            if (!CanApprove(out string reason)) throw new InvalidOperationException(reason);
            resolvedRows = BuildInfoRows();
            return ApprovedChanges();
        }

        public RuleResult OnReject()
        {
            resolvedRows = BuildInfoRows();
            Rejected();
            return new RuleResult();
        }

        public DocumentSaveRecord ToSaveRecord() => new DocumentSaveRecord
        {
            type = TypeId, companyId = DedupKey, deadlineTurn = DeadlineTurn,
            fields = scale.value.HasValue ? new List<NameValue> { new NameValue { name = scaleName, value = scale.value.Value } } : new List<NameValue>(),
            infoRows = resolvedRows?.Select(row => new NameText { label = row.label, value = row.value }).ToList()
        };

        protected void RestoreRows(DocumentSaveRecord saved)
        {
            if (saved.fields == null || saved.fields.Count > 1 || saved.fields.Any(field => field.name != scaleName ||
                field.value < 0 || field.value > 2 || field.value != Math.Floor(field.value)))
                throw new InvalidDataException("인수 후 안건의 선택값이 올바르지 않습니다.");
            if (saved.resolved && saved.infoRows != null)
                resolvedRows = saved.infoRows.Select(row => new DocumentInfo(row.label, row.value)).ToList();
        }
    }
}
