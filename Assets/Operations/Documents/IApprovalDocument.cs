using System.Collections.Generic;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    public interface IApprovalDocument : IDocument
    {
        Company TargetCompany { get; }
        int DeadlineTurn { get; }
        string ApproveLabel { get; }
        string RejectLabel { get; }
        string DoneLabel { get; }
        IReadOnlyList<string> BodyLines { get; }
        IReadOnlyList<DocumentField> Fields { get; }
        IReadOnlyList<DocumentChoiceField> ChoiceFields { get; }
        void SetField(string name, float value);
        void SetChoice(string name, int key);
        bool CanApprove(out string reason);
        RuleResult OnApprove();
        RuleResult OnReject();
    }
}
