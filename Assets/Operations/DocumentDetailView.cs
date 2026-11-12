using System;
using System.Collections.Generic;
using TakeOver.Documents;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentDetailView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private GameObject genericBody;
        [SerializeField] private Transform fieldsRoot;
        [SerializeField] private DocumentFieldView fieldTemplate;
        [SerializeField] private Button approve;
        [SerializeField] private Button reject;
        [SerializeField] private Button back;
        private readonly List<GameObject> fields = new List<GameObject>();
        private IApprovalDocument document;
        private bool readOnly;
        public event Action<IApprovalDocument, bool> Decided;
        public event Action Closed;

        private void Awake()
        {
            approve.onClick.AddListener(() => decide(true));
            reject.onClick.AddListener(() => decide(false));
            back.onClick.AddListener(() => Closed?.Invoke());
        }

        public void Show(IApprovalDocument doc, ResolvedDocument resolved = null)
        {
            document = doc;
            readOnly = resolved != null;
            gameObject.SetActive(true);
            title.text = doc.Title;
            if (doc.BodyId != "generic") Debug.LogWarning($"알 수 없는 문서 본문 {doc.BodyId}: 범용 본문을 사용합니다.");
            genericBody.SetActive(true);
            body.text = readOnly
                ? $"{doc.TargetCompany?.definition.companyName}\n{(resolved.approved ? "승인" : "거절")} · 처리한 턴 {resolved.resolvedTurn}\n처리 당시 입력값"
                : string.Join("\n", doc.BodyLines);
            foreach (var field in fields) Destroy(field);
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
                view.Bind(doc, field, readOnly, value, () => body.text = string.Join("\n", doc.BodyLines));
                fields.Add(view.gameObject);
            }
            approve.gameObject.SetActive(!readOnly);
            reject.gameObject.SetActive(!readOnly);
        }

        private void decide(bool approved)
        {
            if (!readOnly && document != null) Decided?.Invoke(document, approved);
        }
    }
}
