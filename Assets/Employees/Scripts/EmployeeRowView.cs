using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Employees
{
    public class EmployeeRowView : MonoBehaviour
    {
        [Header("Row Button")]
        [SerializeField] private Button rowButton;

        [Header("Basic & Ability Columns")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI analysisText;
        [SerializeField] private TextMeshProUGUI networkText;
        [SerializeField] private TextMeshProUGUI eyeText;
        [SerializeField] private TextMeshProUGUI legworkText;
        [SerializeField] private TextMeshProUGUI managementText;
        [SerializeField] private TextMeshProUGUI salaryText;

        [Header("Hidden Stat Columns (4 Separate Cells)")]
        [SerializeField] private TextMeshProUGUI diligenceText;
        [SerializeField] private TextMeshProUGUI loyaltyText;
        [SerializeField] private TextMeshProUGUI ambitionText;
        [SerializeField] private TextMeshProUGUI potentialText;

        private Employee boundEmployee;
        private EmployeeService boundService;
        private Action<Employee> onClicked;

        public Employee BoundEmployee => boundEmployee;

        public void Bind(Employee emp, EmployeeService service, Action<Employee> onRowClicked)
        {
            boundEmployee = emp;
            boundService = service;
            onClicked = onRowClicked;

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(HandleClick);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (boundEmployee == null)
            {
                return;
            }

            if (nameText != null) nameText.text = boundEmployee.Name;
            if (gradeText != null) gradeText.text = EmployeeTabView.FormatGrade(boundEmployee.Grade);
            if (analysisText != null) analysisText.text = boundEmployee.Analysis.Current.ToString();
            if (networkText != null) networkText.text = boundEmployee.Network.Current.ToString();
            if (eyeText != null) eyeText.text = boundEmployee.Eye.Current.ToString();
            if (legworkText != null) legworkText.text = boundEmployee.Legwork.Current.ToString();
            if (managementText != null) managementText.text = boundEmployee.Management.Current.ToString();
            if (salaryText != null) salaryText.text = $"{boundEmployee.MonthlySalary:N0}만";

            SetHiddenCell(diligenceText, HiddenStatType.Diligence);
            SetHiddenCell(loyaltyText, HiddenStatType.Loyalty);
            SetHiddenCell(ambitionText, HiddenStatType.Ambition);
            SetHiddenCell(potentialText, HiddenStatType.Potential);
        }

        private void SetHiddenCell(TextMeshProUGUI label, HiddenStatType statType)
        {
            if (label == null || boundEmployee == null)
            {
                return;
            }

            bool revealed = boundService != null
                ? boundService.IsRevealed(boundEmployee, statType)
                : boundEmployee.IsHiddenRevealed(statType);

            if (revealed)
            {
                int val = boundService != null
                    ? boundService.GetHiddenStat(boundEmployee, statType)
                    : boundEmployee.GetHiddenStat(statType);
                label.text = val.ToString();
                label.color = new Color32(0xF3, 0xD1, 0x76, 0xFF);
            }
            else
            {
                label.text = "?";
                label.color = new Color32(0xBA, 0xBA, 0xBA, 0xFF);
            }
        }

        private void HandleClick()
        {
            if (boundEmployee != null)
            {
                onClicked?.Invoke(boundEmployee);
            }
        }
    }
}