using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Employees
{
    public class EmployeeDetailView : MonoBehaviour
    {
        private const float StatBarMax = 20f;
        [SerializeField] private AbilityRadarGraphic abilityRadar;
        [SerializeField] private TextMeshProUGUI profileNumber;
        [SerializeField] private Sprite jobSelectedSprite, jobUnselectedSprite;

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

        [Header("Job (임시 디자인)")]
        [SerializeField] private TextMeshProUGUI jobNameText;
        [SerializeField] private TextMeshProUGUI jobRemainingText;
        [SerializeField] private Button appraisalButton;
        [SerializeField] private Button discoveryButton;
        [SerializeField] private Button clearJobButton;
        [SerializeField] private AppraisalCompanyPicker appraisalPicker;

        private Employee currentEmployee;
        private JobService jobService;
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

            BindJobButton(appraisalButton, JobType.Appraisal);
            BindJobButton(discoveryButton, JobType.Discovery);
            BindJobButton(clearJobButton, JobType.None);
        }

        private void BindJobButton(Button button, JobType job)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (currentEmployee == null || jobService == null)
                {
                    return;
                }

                if (job == JobType.Appraisal && appraisalPicker != null)
                {
                    appraisalPicker.open(currentEmployee, () => { Refresh(); onDismissed?.Invoke(); });
                    return;
                }

                jobService.Assign(currentEmployee, job);
                Refresh();
                onDismissed?.Invoke();
            });
        }

        public void Open(Employee emp, EmployeeService service, Func<int> turnProvider, Action onEmployeeDismissed)
        {
            currentEmployee = emp;
            employeeService = service;
            currentTurnProvider = turnProvider;
            onDismissed = onEmployeeDismissed;
            if (jobService == null)
            {
                jobService = FindFirstObjectByType<JobService>();
            }

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
            if (salaryText != null) salaryText.text = $"{currentEmployee.MonthlySalary:N0}만원";
            if (profileNumber != null) profileNumber.text = currentEmployee.ProfileNumber;
            if (abilityRadar != null) abilityRadar.SetValues(new[] { currentEmployee.Analysis.Current, currentEmployee.Network.Current, currentEmployee.Eye.Current, currentEmployee.Legwork.Current, currentEmployee.Management.Current });
            if (nameText != null && gradeText != null)
            {
                var p = gradeText.rectTransform.anchoredPosition;
                p.x = nameText.rectTransform.anchoredPosition.x + nameText.GetPreferredValues(nameText.text).x + 16;
                gradeText.rectTransform.anchoredPosition = p;
            }

            int nowTurn = currentTurnProvider != null ? currentTurnProvider() : 0;
            int remainingDays = Mathf.Max(0, currentEmployee.ContractEndTurn - nowTurn);
            if (remainingContractText != null)
            {
                remainingContractText.text = $"{remainingDays}일";
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
                potentialValueText.text = TryGetHiddenStatValue(HiddenStatType.Potential, out int potVal)
                    ? potVal.ToString()
                    : "?";
            }

            RefreshJob();

            int severance = employeeService != null ? employeeService.GetSeveranceAmount(currentEmployee) : 0;
            if (severanceText != null)
            {
                severanceText.text = $"해고 보상금 {severance:N0}만원";
            }
        }

        private void RefreshJob()
        {
            JobType job = jobService != null ? jobService.GetJob(currentEmployee) : JobType.None;
            if (jobNameText != null)
            {
                jobNameText.text = job == JobType.None ? "진행 중인 직무 없음" : "진행 중 : " + FormatJob(job) + (job == JobType.Support ? "" : $" D-{jobService.GetRemainingDays(currentEmployee)}");
            }

            StyleJobButton(appraisalButton, job == JobType.Appraisal);
            StyleJobButton(discoveryButton, job == JobType.Discovery);
            StyleJobButton(clearJobButton, false);
            if (jobRemainingText != null)
            {
                jobRemainingText.text = job == JobType.None ? "" : job == JobType.Support
                    ? jobService.SupportTargetName(currentEmployee) + " 지원 중" : $"D-{jobService.GetRemainingDays(currentEmployee)}";
            }
        }

        private void StyleJobButton(Button button, bool selected)
        {
            if (button == null || jobSelectedSprite == null) return;
            button.image.sprite = selected ? jobSelectedSprite : jobUnselectedSprite;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = selected ? new Color32(246,246,246,255) : new Color32(36,72,126,255);
        }

        private static string FormatJob(JobType job)
        {
            switch (job)
            {
                case JobType.Appraisal: return "시세 예측";
                case JobType.Discovery: return "스타트업 발굴";
                case JobType.Support: return "지원 인원 배치";
                default: return "직무 없음";
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
            if (TryGetHiddenStatValue(type, out int val))
            {
                if (fill != null) fill.fillAmount = Mathf.Clamp01(val / StatBarMax);
                if (label != null) label.text = val.ToString();
            }
            else
            {
                if (fill != null) fill.fillAmount = 0f;
                if (label != null) label.text = "?";
            }
        }

        private bool TryGetHiddenStatValue(HiddenStatType type, out int value)
        {
            value = 0;
            if (currentEmployee == null)
            {
                return false;
            }

            return employeeService != null
                ? employeeService.TryGetHiddenStat(currentEmployee, type, out value)
                : currentEmployee.TryGetHiddenStat(type, out value);
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
