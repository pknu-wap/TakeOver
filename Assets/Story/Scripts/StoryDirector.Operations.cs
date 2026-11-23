using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using TakeOver.Operations;
using TakeOver.Startups;
using UnityEngine;

namespace TakeOver.Story
{
    public sealed partial class StoryDirector
    {
        private bool IsOperationsReading => (int)Goal >= (int)StoryGoal.ViewEmployees && (int)Goal <= (int)StoryGoal.ViewStartups;
        private OperationsScreen operationsLesson;
        private CanvasGroup operationsInput;
        private bool previousOperationsInput, previousOtherPanel;
        private GameObject otherOperationsPanel;
        private Vector3 operationsScale, operationsPosition;
        private DocumentDetailView documentPreview;

        private void OpenOperationsLesson()
        {
            operationsLesson = FindFirstObjectByType<OperationsScreen>(FindObjectsInactive.Include);
            if (operationsLesson == null) return;
            operationsLesson.ShowTutorialPage(Goal == StoryGoal.ViewEmployees, Goal == StoryGoal.ViewStartups);
            var rect = operationsLesson.TutorialContent;
            operationsScale = rect.localScale;
            operationsPosition = rect.localPosition;
            otherOperationsPanel = operationsLesson.OtherTutorialContent;
            previousOtherPanel = otherOperationsPanel.activeSelf;
            otherOperationsPanel.SetActive(false);
            // Keep the real screen visible beside the shared explanation, with no active transactions.
            operationsInput = operationsLesson.GetComponent<CanvasGroup>();
            if (operationsInput == null) operationsInput = operationsLesson.gameObject.AddComponent<CanvasGroup>();
            previousOperationsInput = operationsInput.interactable;
            operationsInput.interactable = false;
            if (Goal == StoryGoal.ViewDiscovery || Goal == StoryGoal.ViewContract || Goal == StoryGoal.ViewApproval)
            {
                var source = operationsLesson.GetComponentInChildren<DocumentDetailView>(true);
                if (source != null)
                {
                    documentPreview = Instantiate(source, source.transform.parent, false);
                    documentPreview.name = "Tutorial document preview";
                    documentPreview.transform.SetAsLastSibling();
                    documentPreview.gameObject.SetActive(true);
                    IDocument document;
                    var companies = FindFirstObjectByType<Companies>();
                    var candidate = companies?.StartupList.FirstOrDefault();
                    if (Goal == StoryGoal.ViewContract)
                        document = new ApprovalPreview(clock.CurrentTurn + 7, true);
                    else if (Goal == StoryGoal.ViewDiscovery && candidate != null)
                        document = new StartupDiscoveryDocument(candidate, companies, "발굴 담당", clock.CurrentTurn + 7, "설명용 미리보기", false);
                    else if (Goal == StoryGoal.ViewApproval)
                        document = new ApprovalPreview(clock.CurrentTurn + 7);
                    else
                        document = Goal == StoryGoal.ViewDiscovery
                            ? new SnapshotDocument("tutorial-preview", "스타트업 발굴 보고 · 설명용", "발굴 담당",
                                new List<DocumentInfo> { new DocumentInfo("회사 정보", "매출·보유 자금·기업가치와 성장 전망을 확인합니다."),
                                    new DocumentInfo("투자 검토", "후보의 조건을 읽고 계약을 검토합니다.") })
                            : new SnapshotDocument("tutorial-preview", "투자 계약서 · 설명용", "",
                                new List<DocumentInfo> { new DocumentInfo("투자 조건", "취득 지분과 필요한 투자금을 확인합니다."),
                                    new DocumentInfo("확정", "자금과 지분이 바뀌므로 조건을 읽고 결정합니다.") });
                    documentPreview.Show(document);
                    documentPreview.ShowTutorialPreview();
                }
            }
            view.ShowOperationsFocus(rect);
        }

        private void CloseOperationsLesson()
        {
            if (documentPreview != null) { documentPreview.gameObject.SetActive(false); Destroy(documentPreview.gameObject); }
            documentPreview = null;
            if (operationsLesson == null) return;
            if (operationsInput != null)
            {
                operationsInput.interactable = previousOperationsInput;
            }
            var rect = operationsLesson.TutorialContent;
            rect.localScale = operationsScale;
            rect.localPosition = operationsPosition;
            if (otherOperationsPanel != null) otherOperationsPanel.SetActive(previousOtherPanel);
            otherOperationsPanel = null;
            operationsLesson.close();
            operationsLesson = null;
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
                new DocumentInfo("대상 회사", "예시 회사"),
                new DocumentInfo("기업가치", "1,000,000"), new DocumentInfo("투자금", "250,000"),
                new DocumentInfo("안내", "지분 20% 취득 예시")
            } : new[] { new DocumentInfo("검토 내용", "요청의 내용과 조건을 확인합니다.") };
            public IReadOnlyList<string> BodyLines => new[] { "설명용 예시입니다. 실제 계약이나 결재는 진행하지 않습니다." };
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
