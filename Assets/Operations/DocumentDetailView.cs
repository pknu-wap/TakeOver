using System;
using System.Collections.Generic;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject documentRoot;
        [SerializeField] private GameObject emptyMessage;
        [SerializeField] private Text title;
        [SerializeField] private Text sender;
        [SerializeField] private Text deadline;
        [SerializeField] private Transform infoRoot;
        [SerializeField] private DocumentInfoRow infoTemplate;
        [SerializeField] private Transform fieldsRoot;
        [SerializeField] private DocumentFieldView fieldTemplate;
        [SerializeField] private Button approve;
        [SerializeField] private Button reject;
        private readonly List<GameObject> fields = new List<GameObject>();
        private readonly List<GameObject> infoRows = new List<GameObject>();
        private IApprovalDocument document;
        private bool readOnly;
        private GameTurnClock clock;
        public event Action<IApprovalDocument, bool> Decided;

        private void Awake()
        {
            clock = FindFirstObjectByType<GameTurnClock>();
            approve.onClick.AddListener(() => decide(true));
            reject.onClick.AddListener(() => decide(false));
        }

        public void Clear()
        {
            document = null;
            documentRoot.SetActive(false);
            emptyMessage.SetActive(true);
        }

        public void Show(IApprovalDocument doc, ResolvedDocument resolved = null)
        {
            document = doc;
            readOnly = resolved != null;
            documentRoot.SetActive(true);
            emptyMessage.SetActive(false);
            title.text = string.Join(" ", doc.Title.ToCharArray());
            sender.text = $"대상 · {doc.TargetCompany?.definition.companyName}";
            int remaining = doc.DeadlineTurn - clock.CurrentTurn;
            deadline.text = remaining < 0 ? "기한 지남" : remaining == 0 ? "오늘 마감" : $"D-{remaining}";
            deadline.color = remaining <= 0 ? new Color(.73f, .18f, .22f) : new Color(.3f, .34f, .4f);
            if (doc.BodyId != "generic") Debug.LogWarning($"알 수 없는 문서 본문 {doc.BodyId}: 범용 본문을 사용합니다.");
            refreshInfo(doc, resolved);
            foreach (var field in fields) { field.SetActive(false); Destroy(field); }
            fields.Clear();
            fieldTemplate.gameObject.SetActive(false);
            foreach (DocumentField field in doc.Fields)
            {
                float value = field.value;
                if (resolved != null)
                    foreach (NameValue saved in resolved.fields)
                        if (saved.name == field.name) { value = saved.value; break; }
                var view = Instantiate(fieldTemplate, fieldsRoot, false);
                view.gameObject.SetActive(true);
                view.Bind(doc, field, readOnly, value, () => refreshInfo(doc, resolved));
                fields.Add(view.gameObject);
            }
            approve.gameObject.SetActive(!readOnly);
            reject.gameObject.SetActive(!readOnly);
        }

        private void refreshInfo(IApprovalDocument doc, ResolvedDocument resolved)
        {
            foreach (var row in infoRows) { row.SetActive(false); Destroy(row); }
            infoRows.Clear();
            infoTemplate.gameObject.SetActive(false);
            addInfo("대상 회사", doc.TargetCompany?.definition.companyName ?? "—");
            if (resolved != null)
            {
                addInfo("처리 결과", resolved.approved ? "승인" : "거절");
                addInfo("처리한 턴", resolved.resolvedTurn.ToString());
                return;
            }
            foreach (string line in doc.BodyLines)
            {
                if (line == doc.TargetCompany?.definition.companyName) continue;
                int colon = line.IndexOf(':');
                addInfo(colon < 0 ? "내용" : line.Substring(0, colon),
                    colon < 0 ? line : line.Substring(colon + 1).Trim());
            }
        }

        private void addInfo(string label, string value)
        {
            var row = Instantiate(infoTemplate, infoRoot, false);
            row.gameObject.SetActive(true);
            row.Bind(label, value);
            infoRows.Add(row.gameObject);
        }

        private void decide(bool approved)
        {
            if (!readOnly && document != null) Decided?.Invoke(document, approved);
        }
    }
}
