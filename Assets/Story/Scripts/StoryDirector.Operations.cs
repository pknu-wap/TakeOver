using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using UnityEngine;

namespace TakeOver.Story
{
    public sealed partial class StoryDirector
    {
        private bool IsOperationsReading => (int)Goal >= (int)StoryGoal.ViewEmployees && (int)Goal <= (int)StoryGoal.ViewStartups;
        private OperationsTutorialView operationsPreview;

        private void OpenOperationsLesson()
        {
            IDocument document = null;
            if (Goal == StoryGoal.ViewContract) document = new ApprovalPreview(clock.CurrentTurn + 7, true);
            else if (Goal == StoryGoal.ViewApproval) document = new ApprovalPreview(clock.CurrentTurn + 7);
            else if (Goal == StoryGoal.ViewDiscovery)
            {
                var companies = FindFirstObjectByType<Companies>();
                var candidate = companies?.StartupList.FirstOrDefault();
                document = candidate != null
                    ? (IDocument)new StartupDiscoveryDocument(candidate, companies, "발굴 담당", clock.CurrentTurn + 7, "매출과 시장 수요가 꾸준히 성장하고 있습니다.", false)
                    : new SnapshotDocument("tutorial-preview", "스타트업 발굴 보고", "발굴 담당",
                        new List<DocumentInfo> { new DocumentInfo("회사 정보", "매출·자금·기업가치를 확인합니다.") });
            }
            operationsPreview = view.CreateOperationsPreview(Goal, document);
        }

        private void CloseOperationsLesson()
        {
            if (operationsPreview == null) return;
            operationsPreview.gameObject.SetActive(false);
            Destroy(operationsPreview.gameObject);
            operationsPreview = null;
        }

        // Never registered in the inbox or saved; its decision methods have no effects.
        private sealed class ApprovalPreview : IApprovalDocument
        {
            private readonly bool contract;
            public string TypeId => "tutorial-approval";
            public string Title => contract ? "투자 계약서" : "운영 계획 검토";
            public string Author => "운영 담당";
            public string BodyId => "generic";
            public string DedupKey => TypeId;
            public Company TargetCompany => null;
            public int DeadlineTurn { get; }
            public string ApproveLabel => "승인";
            public string RejectLabel => "거절";
            public string DoneLabel => "";
            public IReadOnlyList<DocumentInfo> InfoRows => contract ? new[] {
                new DocumentInfo("대상 회사", "새봄테크"),
                new DocumentInfo("기업가치", MoneyText.Format(300f)), new DocumentInfo("투자금", MoneyText.Format(60f)),
                new DocumentInfo("취득 지분", "20%")
            } : new[] { new DocumentInfo("대상 회사", "새봄테크"), new DocumentInfo("검토 항목", "운영 계획"),
                new DocumentInfo("요청 내용", "직원 2명 파견 검토"), new DocumentInfo("예상 비용", MoneyText.Format(2f)),
                new DocumentInfo("검토 기준", "자금 여력과 업무 배치") };
            public IReadOnlyList<string> BodyLines => contract
                ? new[] { "투자 조건과 취득 지분을 확인한 뒤 계약 여부를 결정합니다." }
                : new[] { "운영 계획과 예상 비용을 검토한 뒤 승인 여부를 결정합니다." };
            public IReadOnlyList<DocumentField> Fields => contract ? new[] { new DocumentField("stake", "지분율", .2f, new[] { .2f }) } : System.Array.Empty<DocumentField>();
            public IReadOnlyList<DocumentChoiceField> ChoiceFields => System.Array.Empty<DocumentChoiceField>();
            public ApprovalPreview(int day, bool contract = false) { DeadlineTurn = day; this.contract = contract; }
            public void SetField(string name, float value) { }
            public void SetChoice(string name, int key) { }
            public bool CanApprove(out string reason) { reason = "설명용 화면입니다."; return false; }
            public TakeOver.CompanyRules.RuleResult OnApprove() => new TakeOver.CompanyRules.RuleResult();
            public TakeOver.CompanyRules.RuleResult OnReject() => new TakeOver.CompanyRules.RuleResult();
            public DocumentSaveRecord ToSaveRecord() => throw new System.InvalidOperationException("설명용 문서는 저장하지 않습니다.");
        }
    }
}
