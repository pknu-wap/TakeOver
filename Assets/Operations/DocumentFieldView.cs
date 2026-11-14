using System;
using System.Collections.Generic;
using TakeOver.Documents;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentFieldView : MonoBehaviour
    {
        [SerializeField] private DocumentButton valueButton;
        [SerializeField] private GameObject optionsRoot;
        [SerializeField] private DocumentButton optionTemplate;
        [SerializeField] private Text fieldLabel;
        private readonly List<GameObject> options = new List<GameObject>();

        public void Bind(IApprovalDocument document, DocumentField field, bool readOnly, float value, Action changed)
        {
            if (field.options == null || field.options.Count == 0)
            {
                Debug.LogWarning($"문서 입력 칸 {field.name}: 선택지 없는 입력은 지원하지 않습니다.");
                gameObject.SetActive(false);
                return;
            }
            foreach (var option in options) Destroy(option);
            options.Clear();
            optionsRoot.SetActive(false);
            optionTemplate.gameObject.SetActive(false);
            fieldLabel.text = field.label;
            valueButton.Bind($"{value:P0}{(readOnly ? "" : " ▾")}", () => optionsRoot.SetActive(!optionsRoot.activeSelf), !readOnly);
            if (readOnly) return;
            foreach (float choice in field.options)
            {
                var button = Instantiate(optionTemplate, optionTemplate.transform.parent, false);
                button.gameObject.SetActive(true);
                button.Bind(choice.ToString("P0"), () =>
                {
                    document.SetField(field.name, choice);
                    valueButton.Bind($"{choice:P0} ▾", () => optionsRoot.SetActive(!optionsRoot.activeSelf));
                    optionsRoot.SetActive(false);
                    changed();
                });
                options.Add(button.gameObject);
            }
        }
    }
}
