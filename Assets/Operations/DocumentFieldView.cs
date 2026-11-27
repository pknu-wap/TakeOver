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
        private DocumentDropdownMenu menu;
        private Action openMenu;
        private string selectedText;

        private void Awake()
        {
            menu = optionsRoot.GetComponent<DocumentDropdownMenu>();
            if (menu == null) menu = optionsRoot.AddComponent<DocumentDropdownMenu>();
        }

        public void Bind(IApprovalDocument document, DocumentField field, bool readOnly, float value, Action changed)
        {
            if (field.options == null || field.options.Count == 0)
            {
                Debug.LogWarning($"문서 입력 칸 {field.name}: 선택지 없는 입력은 지원하지 않습니다.");
                gameObject.SetActive(false);
                return;
            }
            menu.Close();
            optionTemplate.gameObject.SetActive(false);
            fieldLabel.text = field.label;
            configureLayout();
            string initialText = !readOnly && field.initialSelected ? field.initialOptionLabel : formatValue(value, field.format);
            var choices = new List<DocumentDropdownOption>();
            if (!readOnly && field.initialOptionLabel != null)
            {
                choices.Add(new DocumentDropdownOption
                {
                    label = field.initialOptionLabel,
                    selected = () =>
                    {
                        document.SetField(field.name, field.initialValue);
                        field.initialSelected = true;
                        bindValue(field.initialOptionLabel, true);
                        menu.Close();
                        changed();
                    }
                });
            }
            if (!readOnly) foreach (float choice in field.options)
            {
                float selectedChoice = choice;
                choices.Add(new DocumentDropdownOption
                {
                    label = formatValue(selectedChoice, field.format),
                    selected = () =>
                    {
                        document.SetField(field.name, selectedChoice);
                        field.initialSelected = false;
                        bindValue(formatValue(selectedChoice, field.format), true);
                        menu.Close();
                        changed();
                    }
                });
            }
            openMenu = readOnly ? null : (Action)(() => menu.Show(valueButton, optionTemplate, selectedText, choices,
                () => menu.Close()));
            bindValue(initialText, !readOnly);
        }

        private void bindValue(string text, bool interactable)
        {
            selectedText = text;
            valueButton.Bind(text, interactable ? openMenu : null, interactable);
            valueButton.SetDropdownStyle(true, interactable ? new Color32(96, 96, 96, 255) : new Color32(186, 186, 186, 255), interactable);
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

        private static string formatValue(float value, DocumentValueFormat format)
        {
            switch (format)
            {
                case DocumentValueFormat.Number: return value.ToString("N0");
                case DocumentValueFormat.Money: return MoneyText.Format(value);
                case DocumentValueFormat.Stake: return StakeText.Format(value * 100d);
                default: return value.ToString("P0");
            }
        }
    }
}
