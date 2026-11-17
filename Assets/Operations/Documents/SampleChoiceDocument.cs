using System;
using System.Collections.Generic;
using TakeOver.CompanyRules;

namespace TakeOver.Documents
{
    // 문서 틀 검증용 임시 문서. 실제 게임 흐름에는 연결하지 않는다.
    public sealed class SampleChoiceDocument : IApprovalDocument
    {
        public const string typeId = "sample-choice";
        public const int invalidKey = 3;
        // 선택지 목록을 코드로 바꿔 "열 때마다 새로 읽는지" 확인하는 용도.
        public static List<DocumentChoice> sampleChoices = new List<DocumentChoice>
        {
            new DocumentChoice { key = 1, label = "직원 A" },
            new DocumentChoice { key = 2, label = "직원 B" },
            new DocumentChoice { key = invalidKey, label = "직원 C" }
        };
        private readonly List<DocumentChoiceField> choiceFields;
        public string TypeId => typeId;
        public string Title => "임시 검증 문서";
        public string Author => "검증 담당";
        public string BodyId => "generic";
        public string DedupKey => "sample";
        public Company TargetCompany => null;
        public int DeadlineTurn { get; }
        public string ApproveLabel => "";
        public string RejectLabel => "";
        public string DoneLabel => "";
        public IReadOnlyList<DocumentInfo> InfoRows => new[] { new DocumentInfo("종류", "선택칸 승인 샘플") };
        public IReadOnlyList<string> BodyLines => new[] { "선택칸 승인 샘플" };
        public IReadOnlyList<DocumentField> Fields => Array.Empty<DocumentField>();
        public IReadOnlyList<DocumentChoiceField> ChoiceFields => choiceFields;

        public SampleChoiceDocument(int deadlineTurn)
        {
            DeadlineTurn = deadlineTurn;
            choiceFields = new List<DocumentChoiceField>
            {
                new DocumentChoiceField("employee", "직원", () => sampleChoices)
            };
        }

        public void SetField(string name, float value)
        {
            throw new ArgumentException("알 수 없는 문서 입력 칸입니다.", nameof(name));
        }

        public void SetChoice(string name, int key)
        {
            if (name != choiceFields[0].name) throw new ArgumentException("알 수 없는 문서 선택 칸입니다.", nameof(name));
            choiceFields[0].value = key;
        }

        public bool CanApprove(out string reason)
        {
            bool valid = choiceFields[0].value != invalidKey;
            reason = valid ? "" : "선택한 직원이 유효하지 않습니다";
            return valid;
        }

        public RuleResult OnApprove() => new RuleResult();

        public RuleResult OnReject() => new RuleResult();

        public DocumentSaveRecord ToSaveRecord()
        {
            DocumentSaveRecord record = new DocumentSaveRecord
            {
                type = TypeId,
                deadlineTurn = DeadlineTurn,
                fields = new List<NameValue>()
            };
            if (choiceFields[0].value.HasValue)
                record.fields.Add(new NameValue { name = choiceFields[0].name, value = choiceFields[0].value.Value });
            return record;
        }
    }
}
