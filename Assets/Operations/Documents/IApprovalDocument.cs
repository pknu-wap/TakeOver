using System.Collections.Generic;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public interface IApprovalDocument
    {
        string TypeId { get; }
        Company TargetCompany { get; }
        int DeadlineTurn { get; }
        string BodyId { get; }
        string Title { get; }
        IReadOnlyList<string> BodyLines { get; }
        IReadOnlyList<DocumentField> Fields { get; }
        void SetField(string name, float value);
        RuleResult OnApprove();
        RuleResult OnReject();
        DocumentSaveRecord ToSaveRecord();
    }
}
