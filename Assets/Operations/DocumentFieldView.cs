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

        private void Awake()
        {
            var canvas = optionsRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = optionsRoot.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
            if (optionsRoot.GetComponent<GraphicRaycaster>() == null) optionsRoot.AddComponent<GraphicRaycaster>();
        }

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
            string initialText = !readOnly && field.initialSelected ? field.initialOptionLabel : formatValue(value, field.format);
            valueButton.Bind($"{initialText}{(readOnly ? "" : " ▾")}", () => optionsRoot.SetActive(!optionsRoot.activeSelf), !readOnly);
            if (readOnly) return;
            if (field.initialOptionLabel != null)
            {
                var initial = Instantiate(optionTemplate, optionTemplate.transform.parent, false);
                initial.gameObject.SetActive(true);
                initial.Bind(field.initialOptionLabel, () =>
                {
                    document.SetField(field.name, field.initialValue);
                    field.initialSelected = true;
                    valueButton.Bind(field.initialOptionLabel + " ▾", () => optionsRoot.SetActive(!optionsRoot.activeSelf));
                    optionsRoot.SetActive(false);
                    changed();
                });
                options.Add(initial.gameObject);
            }
            foreach (float choice in field.options)
            {
                var button = Instantiate(optionTemplate, optionTemplate.transform.parent, false);
                button.gameObject.SetActive(true);
                button.Bind(formatValue(choice, field.format), () =>
                {
                    document.SetField(field.name, choice);
                    field.initialSelected = false;
                    valueButton.Bind($"{formatValue(choice, field.format)} ▾", () => optionsRoot.SetActive(!optionsRoot.activeSelf));
                    optionsRoot.SetActive(false);
                    changed();
                });
                options.Add(button.gameObject);
            }
        }

        private static string formatValue(float value, DocumentValueFormat format)
        {
            switch (format)
            {
                case DocumentValueFormat.Number: return value.ToString("N0");
                case DocumentValueFormat.Money: return value.ToString("N0") + "원";
                default: return value.ToString("P0");
            }
        }
    }
}
