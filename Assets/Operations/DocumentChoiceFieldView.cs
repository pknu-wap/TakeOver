using System;
using System.Collections.Generic;
using TakeOver.Documents;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentChoiceFieldView : MonoBehaviour
    {
        private const string emptyLabel = "—";
        [SerializeField] private DocumentButton valueButton;
        [SerializeField] private GameObject optionsRoot;
        [SerializeField] private DocumentButton optionTemplate;
        [SerializeField] private Text fieldLabel;
        private readonly List<GameObject> options = new List<GameObject>();

        public void Bind(IApprovalDocument document, DocumentChoiceField field, bool readOnly, int? value, Action changed)
        {
            clearOptions();
            optionsRoot.SetActive(false);
            optionTemplate.gameObject.SetActive(false);
            fieldLabel.text = field.label;
            if (readOnly)
            {
                valueButton.Bind(labelFor(field, value), null, false);
                return;
            }
            valueButton.Bind(valueText(field, value), () => toggle(document, field, changed));
        }

        private void toggle(IApprovalDocument document, DocumentChoiceField field, Action changed)
        {
            if (optionsRoot.activeSelf)
            {
                optionsRoot.SetActive(false);
                return;
            }
            clearOptions();
            // 열 때마다 최신 목록을 새로 읽는다.
            foreach (DocumentChoice choice in field.GetChoices())
            {
                DocumentChoice picked = choice;
                var button = Instantiate(optionTemplate, optionTemplate.transform.parent, false);
                button.gameObject.SetActive(true);
                button.Bind(picked.label, () =>
                {
                    document.SetChoice(field.name, picked.key);
                    valueButton.Bind(valueText(field, picked.key), () => toggle(document, field, changed));
                    optionsRoot.SetActive(false);
                    changed();
                });
                options.Add(button.gameObject);
            }
            optionsRoot.SetActive(true);
        }

        private static string valueText(DocumentChoiceField field, int? value)
        {
            return (value.HasValue ? labelFor(field, value) : "선택") + " ▾";
        }

        private static string labelFor(DocumentChoiceField field, int? value)
        {
            if (!value.HasValue) return emptyLabel;
            foreach (DocumentChoice choice in field.GetChoices())
                if (choice.key == value.Value) return choice.label;
            return emptyLabel;
        }

        private void clearOptions()
        {
            foreach (var option in options) { option.SetActive(false); Destroy(option); }
            options.Clear();
        }
    }
}
