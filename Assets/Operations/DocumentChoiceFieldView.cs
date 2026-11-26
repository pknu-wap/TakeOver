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
        private DocumentDropdownMenu menu;

        private void Awake()
        {
            menu = optionsRoot.GetComponent<DocumentDropdownMenu>();
            if (menu == null) menu = optionsRoot.AddComponent<DocumentDropdownMenu>();
        }

        public void Bind(IApprovalDocument document, DocumentChoiceField field, bool readOnly, int? value, Action changed)
        {
            menu.Close();
            optionTemplate.gameObject.SetActive(false);
            fieldLabel.text = field.label;
            configureLayout();
            if (readOnly)
            {
                valueButton.Bind(labelFor(field, value), null, false);
                valueButton.SetDropdownStyle(true, new Color32(186, 186, 186, 255), false);
                return;
            }
            valueButton.Bind(valueText(field, value), () => toggle(document, field, changed));
            valueButton.SetDropdownStyle(true, new Color32(96, 96, 96, 255), true);
        }

        private void toggle(IApprovalDocument document, DocumentChoiceField field, Action changed)
        {
            if (optionsRoot.activeSelf)
            {
                menu.Close();
                return;
            }
            var options = new List<DocumentDropdownOption>();
            foreach (DocumentChoice choice in field.GetChoices())
            {
                DocumentChoice picked = choice;
                options.Add(new DocumentDropdownOption
                {
                    label = picked.label,
                    enabled = picked.enabled,
                    selected = () =>
                    {
                        document.SetChoice(field.name, picked.key);
                        valueButton.Bind(valueText(field, picked.key), () => toggle(document, field, changed));
                        valueButton.SetDropdownStyle(true, new Color32(96, 96, 96, 255), true);
                        menu.Close();
                        changed();
                    }
                });
            }
            menu.Show(valueButton, optionTemplate, valueText(field, field.value), options, () => menu.Close());
        }

        private static string valueText(DocumentChoiceField field, int? value)
        {
            return value.HasValue ? labelFor(field, value) : "선택";
        }

        private static string labelFor(DocumentChoiceField field, int? value)
        {
            if (!value.HasValue) return emptyLabel;
            foreach (DocumentChoice choice in field.GetChoices())
                if (choice.key == value.Value) return choice.label;
            return emptyLabel;
        }

        private void configureLayout()
        {
            RectTransform row = (RectTransform)transform;
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 446f);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 36f);
            RectTransform label = fieldLabel.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
            label.pivot = new Vector2(0f, 1f);
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(148f, 36f);
            RectTransform value = (RectTransform)valueButton.transform;
            value.anchorMin = value.anchorMax = new Vector2(0f, 1f);
            value.pivot = new Vector2(0f, 1f);
            value.anchoredPosition = new Vector2(148f, -2f);
            value.sizeDelta = new Vector2(142f, 32f);
        }
    }
}
