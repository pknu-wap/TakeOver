using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TakeOver.Employees
{
    public class EmployeeComparisonView : MonoBehaviour
    {
        [SerializeField] private GameObject modal, ourColumn, emptyOurStaff, ourNavigation;
        [SerializeField] private TextMeshProUGUI numberText, applicantName, applicantGrade, ourName, ourGrade, ourCount;
        [SerializeField] private Image applicantFace, ourFace;
        [SerializeField] private AbilityRadarGraphic applicantRadar, ourRadar;
        [SerializeField] private Button closeButton, hireButton, previousButton, nextButton;
        [SerializeField] private Sprite hireNormal, hireDisabled;
        private EmployeeTabView tab;
        private EmployeeService service;
        private Employee applicant;
        private int employeeIndex;
        private void Awake()
        {
            tab = GetComponent<EmployeeTabView>();
            service = FindFirstObjectByType<EmployeeService>();
            tab.ApplicantSelected += Open;
            if (service != null) service.Changed += Refresh;
            closeButton.onClick.AddListener(Close);
            hireButton.onClick.AddListener(Hire);
            previousButton.onClick.AddListener(() => { employeeIndex--; Refresh(); });
            nextButton.onClick.AddListener(() => { employeeIndex++; Refresh(); });
            modal.SetActive(false);
        }
        private void OnDestroy()
        {
            if (tab != null) tab.ApplicantSelected -= Open;
            if (service != null) service.Changed -= Refresh;
        }
        public void Open(Employee target)
        {
            applicant = target;
            employeeIndex = 0;
            modal.SetActive(true);
            Refresh();
        }
        public void Close() { modal.SetActive(false); applicant = null; }
        private static int[] Values(Employee e) => new[] { e.Analysis.Current, e.Network.Current, e.Eye.Current, e.Legwork.Current, e.Management.Current };
        public void Refresh()
        {
            if (applicant == null || !modal.activeSelf || service == null) return;
            numberText.text = applicant.ProfileNumber;
            applicantName.text = applicant.Name;
            applicantGrade.text = EmployeeTabView.FormatGrade(applicant.Grade);
            PlaceGrade(applicantName, applicantGrade);
            applicantFace.sprite = EmployeeRowDesign.Portrait(applicant.Appearance, true);
            int count = service.Employees.Count;
            ourColumn.SetActive(count > 0);
            ourNavigation.SetActive(count > 0);
            emptyOurStaff.SetActive(count == 0);
            int[] opponent = null;
            if (count > 0)
            {
                employeeIndex = Mathf.Clamp(employeeIndex,0,count-1);
                Employee employee = service.Employees[employeeIndex];
                ourCount.text = $"우리 직원 {employeeIndex + 1}/{count}";
                ourName.text = employee.Name;
                ourGrade.text = EmployeeTabView.FormatGrade(employee.Grade);
                PlaceGrade(ourName, ourGrade);
                ourFace.sprite = EmployeeRowDesign.Portrait(employee.Appearance, true);
                opponent = Values(employee);
                ourRadar.SetValues(opponent, Values(applicant));
                previousButton.interactable = employeeIndex > 0;
                nextButton.interactable = employeeIndex < count-1;
            }
            applicantRadar.SetValues(Values(applicant),opponent);
            bool canHire = count < service.Capacity;
            hireButton.interactable = canHire;
            hireButton.image.sprite = canHire ? hireNormal : hireDisabled;
        }
        private static void PlaceGrade(TextMeshProUGUI name, TextMeshProUGUI grade)
        {
            var pos = grade.rectTransform.anchoredPosition;
            pos.x = name.rectTransform.anchoredPosition.x + name.GetPreferredValues(name.text).x + 16;
            grade.rectTransform.anchoredPosition = pos;
        }
        private void Hire()
        {
            if (applicant != null && service.Hire(applicant)) { Close(); tab.RefreshAll(); }
        }
    }
}
