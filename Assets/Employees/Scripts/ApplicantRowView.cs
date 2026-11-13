using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Employees
{
    public class ApplicantRowView : MonoBehaviour
    {
        [Header("Basic & Ability Columns")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI analysisText;
        [SerializeField] private TextMeshProUGUI networkText;
        [SerializeField] private TextMeshProUGUI eyeText;
        [SerializeField] private TextMeshProUGUI legworkText;
        [SerializeField] private TextMeshProUGUI managementText;
        [SerializeField] private TextMeshProUGUI salaryText;

        [Header("Hidden Stat Columns (All '?' for Applicants)")]
        [SerializeField] private TextMeshProUGUI diligenceText;
        [SerializeField] private TextMeshProUGUI loyaltyText;
        [SerializeField] private TextMeshProUGUI ambitionText;
        [SerializeField] private TextMeshProUGUI potentialText;

        [Header("Hire Action")]
        [SerializeField] private Button hireButton;
        [SerializeField] private TextMeshProUGUI hireButtonText;

        private Employee boundApplicant;
        private Action<Employee> onHireClicked;

        public void Bind(Employee applicant, bool canHire, Action<Employee> onHire)
        {
            boundApplicant = applicant;
            onHireClicked = onHire;

            if (applicant == null)
            {
                return;
            }

            if (nameText != null) nameText.text = applicant.Name;
            if (gradeText != null) gradeText.text = EmployeeTabView.FormatGrade(applicant.Grade);
            if (analysisText != null) analysisText.text = applicant.Analysis.Current.ToString();
            if (networkText != null) networkText.text = applicant.Network.Current.ToString();
            if (eyeText != null) eyeText.text = applicant.Eye.Current.ToString();
            if (legworkText != null) legworkText.text = applicant.Legwork.Current.ToString();
            if (managementText != null) managementText.text = applicant.Management.Current.ToString();
            if (salaryText != null) salaryText.text = $"{applicant.MonthlySalary:N0}만";

            if (diligenceText != null) diligenceText.text = "?";
            if (loyaltyText != null) loyaltyText.text = "?";
            if (ambitionText != null) ambitionText.text = "?";
            if (potentialText != null) potentialText.text = "?";

            if (hireButton != null)
            {
                hireButton.interactable = canHire;
                hireButton.onClick.RemoveAllListeners();
                hireButton.onClick.AddListener(HandleHireClick);
            }

            if (hireButtonText != null)
            {
                hireButtonText.text = canHire ? "고용" : "정원초과";
            }
        }

        private void HandleHireClick()
        {
            if (boundApplicant != null)
            {
                onHireClicked?.Invoke(boundApplicant);
            }
        }
    }
}