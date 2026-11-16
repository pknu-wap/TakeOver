using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Employees
{
    public class EmployeeDetailView : MonoBehaviour
    {
        private const float StatBarMax = 20f;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private Button closeButton;

        [Header("Contract & Salary")]
        [SerializeField] private TextMeshProUGUI salaryText;
        [SerializeField] private TextMeshProUGUI remainingContractText;

        [Header("Ability Stats (0~20 Bar, No Max Displayed)")]
        [SerializeField] private Image analysisFill;
        [SerializeField] private TextMeshProUGUI analysisValueText;
        [SerializeField] private Image networkFill;
        [SerializeField] private TextMeshProUGUI networkValueText;
        [SerializeField] private Image eyeFill;
        [SerializeField] private TextMeshProUGUI eyeValueText;
        [SerializeField] private Image legworkFill;
        [SerializeField] private TextMeshProUGUI legworkValueText;
        [SerializeField] private Image managementFill;
        [SerializeField] private TextMeshProUGUI managementValueText;

        [Header("Hidden Stats")]
        [SerializeField] private Image diligenceFill;
        [SerializeField] private TextMeshProUGUI diligenceValueText;
        [SerializeField] private Image loyaltyFill;
        [SerializeField] private TextMeshProUGUI loyaltyValueText;
        [SerializeField] private Image ambitionFill;
        [SerializeField] private TextMeshProUGUI ambitionValueText;
        [SerializeField] private TextMeshProUGUI potentialValueText;

        [Header("Dismiss Footer")]
        [SerializeField] private TextMeshProUGUI severanceText;
        [SerializeField] private Button dismissButton;

        private Employee currentEmployee;
        private EmployeeService employeeService;
        private Func<int> currentTurnProvider;
        private Action onDismissed;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveAllListeners();
                dismissButton.onClick.AddListener(HandleDismiss);
            }
        }

        public void Open(Employee emp, EmployeeService service, Func<int> turnProvider, Action onEmployeeDismissed)
        {
            currentEmployee = emp;
            employeeService = service;
            currentTurnProvider = turnProvider;
            onDismissed = onEmployeeDismissed;

            if (currentEmployee == null)
            {
                Close();
                return;
            }

            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            currentEmployee = null;
            gameObject.SetActive(false);
        }

        public void RefreshIfShowing(Employee target)
        {
            if (!gameObject.activeSelf || currentEmployee == null)
            {
                return;
            }

            if (target == null || target == currentEmployee)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (currentEmployee == null)
            {
                return;
            }

            if (nameText != null) nameText.text = currentEmployee.Name;
            if (gradeText != null) gradeText.text = EmployeeTabView.FormatGrade(currentEmployee.Grade);
            if (salaryText != null) salaryText.text = $"월급: {currentEmployee.MonthlySalary:N0}만원";

            int nowTurn = currentTurnProvider != null ? currentTurnProvider() : 0;
            int remainingDays = Mathf.Max(0, currentEmployee.ContractEndTurn - nowTurn);
            if (remainingContractText != null)
            {
                remainingContractText.text = $"남은 계약 일수: {remainingDays}일";
            }

            SetAbilityStat(analysisFill, analysisValueText, currentEmployee.Analysis.Current);
            SetAbilityStat(networkFill, networkValueText, currentEmployee.Network.Current);
            SetAbilityStat(eyeFill, eyeValueText, currentEmployee.Eye.Current);
            SetAbilityStat(legworkFill, legworkValueText, currentEmployee.Legwork.Current);
            SetAbilityStat(managementFill, managementValueText, currentEmployee.Management.Current);

            SetHiddenBarStat(diligenceFill, diligenceValueText, HiddenStatType.Diligence);
            SetHiddenBarStat(loyaltyFill, loyaltyValueText, HiddenStatType.Loyalty);
            SetHiddenBarStat(ambitionFill, ambitionValueText, HiddenStatType.Ambition);

            if (potentialValueText != null)
            {
                bool potRevealed = IsStatRevealed(HiddenStatType.Potential);
                potentialValueText.text = potRevealed
                    ? GetHiddenStatValue(HiddenStatType.Potential).ToString()
                    : "?";
            }

            int severance = employeeService != null ? employeeService.GetSeveranceAmount(currentEmployee) : 0;
            if (severanceText != null)
            {
                severanceText.text = $"해고 보상금: {severance:N0}만원";
            }
        }

        private void SetAbilityStat(Image fill, TextMeshProUGUI label, int value)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(value / StatBarMax);
            }

            if (label != null)
            {
                label.text = value.ToString();
            }
        }

        private void SetHiddenBarStat(Image fill, TextMeshProUGUI label, HiddenStatType type)
        {
            bool revealed = IsStatRevealed(type);
            if (revealed)
            {
                int val = GetHiddenStatValue(type);
                if (fill != null) fill.fillAmount = Mathf.Clamp01(val / StatBarMax);
                if (label != null) label.text = val.ToString();
            }
            else
            {
                if (fill != null) fill.fillAmount = 0f;
                if (label != null) label.text = "?";
            }
        }

        private bool IsStatRevealed(HiddenStatType type)
        {
            if (currentEmployee == null) return false;
            return employeeService != null
                ? employeeService.IsRevealed(currentEmployee, type)
                : currentEmployee.IsHiddenRevealed(type);
        }

        private int GetHiddenStatValue(HiddenStatType type)
        {
            if (currentEmployee == null) return 0;
            return employeeService != null
                ? employeeService.GetHiddenStat(currentEmployee, type)
                : currentEmployee.GetHiddenStat(type);
        }

        private void HandleDismiss()
        {
            if (currentEmployee == null || employeeService == null)
            {
                return;
            }

            Employee target = currentEmployee;
            employeeService.Dismiss(target);
            Close();
            onDismissed?.Invoke();
        }
    }
}