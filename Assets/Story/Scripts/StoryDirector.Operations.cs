using System;
using System.Collections.Generic;
using TakeOver.Documents;
using TakeOver.Operations;
using TakeOver.Startups;

namespace TakeOver.Story
{
    public sealed partial class StoryDirector
    {
        private bool IsOperationsReading => (int)Goal >= (int)StoryGoal.ViewEmployees && (int)Goal <= (int)StoryGoal.ViewStartups;
        private OperationsTutorialView operationsPreview;

        private void OpenOperationsLesson()
        {
            IDocument document = Goal == StoryGoal.ViewDiscovery || Goal == StoryGoal.ViewContract || Goal == StoryGoal.ViewApproval
                ? new ApprovalPreview(Goal, clock.CurrentTurn + 7)
                : null;
            operationsPreview = view.CreateOperationsPreview(Goal, document, Current.id == "operations-002-work");
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
            private readonly StoryGoal goal;
            private const string CompanyName = StartupTabView.TutorialCompanyName;
            private const float CompanyValue = 240f;
            private const float InitialStake = .2f;
            public string TypeId => "tutorial-approval";
            public string Title => goal == StoryGoal.ViewDiscovery ? $"투자 후보 보고 · {CompanyName}"
                : goal == StoryGoal.ViewContract ? "투자 계약 체결" : "운영 계획 검토";
            public string Author => goal == StoryGoal.ViewDiscovery ? "김민준" : $"{CompanyName} 대표";
            public string BodyId => "generic";
            public string DedupKey => TypeId;
            public Company TargetCompany => null;
            public int DeadlineTurn { get; }
            public string ApproveLabel => goal == StoryGoal.ViewDiscovery ? "투자 검토" : goal == StoryGoal.ViewContract ? "확정" : "승인";
            public string RejectLabel => goal == StoryGoal.ViewDiscovery ? "넘기기" : "거절";
            public string DoneLabel => "";
            public IReadOnlyList<DocumentInfo> InfoRows { get; }
            public IReadOnlyList<string> BodyLines => Array.Empty<string>();
            public IReadOnlyList<DocumentField> Fields { get; }
            public IReadOnlyList<DocumentChoiceField> ChoiceFields => Array.Empty<DocumentChoiceField>();

            public ApprovalPreview(StoryGoal goal, int day)
            {
                this.goal = goal;
                DeadlineTurn = day;
                Fields = goal == StoryGoal.ViewContract
                    ? new[] { new DocumentField("stake", "지분율", InitialStake, new[] { InitialStake, .3f, .4f }, format: DocumentValueFormat.Stake) }
                    : Array.Empty<DocumentField>();
                InfoRows = goal switch
                {
                    StoryGoal.ViewDiscovery => new[] {
                        new DocumentInfo("대상 회사", CompanyName),
                        new DocumentInfo("업종", StartupTabView.TutorialCompanyIndustry),
                        new DocumentInfo("매출", MoneyText.Format(.81f)),
                        new DocumentInfo("보유 자금", MoneyText.Format(25f)),
                        new DocumentInfo("직원 수", "18명"),
                        new DocumentInfo("제시 기업가치", MoneyText.Format(CompanyValue)),
                        new DocumentInfo("성장 전망", "매출과 시장 수요가 꾸준히 성장하고 있습니다.")
                    },
                    StoryGoal.ViewContract => new[] {
                        new DocumentInfo("대상 회사", CompanyName),
                        new DocumentInfo("기업가치", MoneyText.Format(CompanyValue)),
                        new DocumentInfo("투자금", MoneyText.Format(StartupContractDocument.calculateInvestment(CompanyValue, InitialStake))),
                        new DocumentInfo("취득 지분", StakeText.Format(InitialStake * 100d))
                    },
                    _ => new[] {
                        new DocumentInfo("대상 회사", CompanyName),
                        new DocumentInfo("검토 항목", "운영 계획"),
                        new DocumentInfo("요청 내용", "직원 2명 파견 검토"),
                        new DocumentInfo("예상 비용", MoneyText.Format(2f)),
                        new DocumentInfo("검토 기준", "자금 여력과 업무 배치")
                    }
                };
            }
            public void SetField(string name, float value) { }
            public void SetChoice(string name, int key) { }
            public bool CanApprove(out string reason) { reason = "설명용 화면입니다."; return false; }
            public TakeOver.CompanyRules.RuleResult OnApprove() => new TakeOver.CompanyRules.RuleResult();
            public TakeOver.CompanyRules.RuleResult OnReject() => new TakeOver.CompanyRules.RuleResult();
            public DocumentSaveRecord ToSaveRecord() => throw new System.InvalidOperationException("설명용 문서는 저장하지 않습니다.");
        }
    }
}
