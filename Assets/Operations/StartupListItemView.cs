using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class StartupListItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text companyName;
        [SerializeField] private TMP_Text industry;
        [SerializeField] private Image logo;
        [SerializeField] private GameObject selectionFrame;
        [SerializeField] private Button button;
        private Company company;

        public void Bind(Company value, string name, string industryName, Sprite logoSprite, bool selected, Action<Company> onSelected)
        {
            company = value;
            companyName.text = name ?? "";
            industry.text = industryName ?? "";
            logo.sprite = logoSprite;
            logo.enabled = logoSprite != null;
            selectionFrame.SetActive(selected);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (onSelected != null) onSelected(company);
            });
        }
    }
}