using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    public class EmployeeTabView : MonoBehaviour
    {
        public enum SortColumn
        {
            None,
            Name,
            Grade,
            Analysis,
            Network,
            Eye,
            Legwork,
            Management,
            Salary,
            Diligence,
            Loyalty,
            Ambition,
            Potential
        }

        [Header("Service References")]
        [SerializeField] private EmployeeService employeeService;
        [SerializeField] private GameTurnClock gameTurnClock;
        [SerializeField] private ActionPointService actionPointService;

        [Header("Top Employee Table Section")]
        [SerializeField] private TextMeshProUGUI employeeCountText;
        [SerializeField] private Transform employeeListContent;
        [SerializeField] private EmployeeRowView employeeRowPrefab;
        [SerializeField] private TextMeshProUGUI emptyEmployeeText;

        [Header("Employee Table Sort Buttons (12 Columns)")]
        [SerializeField] private Button sortNameButton;
        [SerializeField] private Button sortGradeButton;
        [SerializeField] private Button sortAnalysisButton;
        [SerializeField] private Button sortNetworkButton;
        [SerializeField] private Button sortEyeButton;
        [SerializeField] private Button sortLegworkButton;
        [SerializeField] private Button sortManagementButton;
        [SerializeField] private Button sortSalaryButton;
        [SerializeField] private Button sortDiligenceButton;
        [SerializeField] private Button sortLoyaltyButton;
        [SerializeField] private Button sortAmbitionButton;
        [SerializeField] private Button sortPotentialButton;

        [Header("Bottom Recruitment Section")]
        [SerializeField] private Button recruitButton;
        [SerializeField] private TextMeshProUGUI recruitButtonText;
        [SerializeField] private TextMeshProUGUI applicantArrivalText;
        [SerializeField] private Button closeApplicantsButton;
        [SerializeField] private Transform applicantListContent;
        [SerializeField] private ApplicantRowView applicantRowPrefab;
        [SerializeField] private TextMeshProUGUI emptyApplicantText;

        [Header("Detail Popup")]
        [SerializeField] private EmployeeDetailView detailView;

        [Header("Optional Test Scene Debug Bar")]
        [SerializeField] private TextMeshProUGUI debugStatusText;
        [SerializeField] private Button debugAdvanceTurnButton;

        private readonly List<EmployeeRowView> spawnedEmployeeRows = new List<EmployeeRowView>();
        private readonly List<ApplicantRowView> spawnedApplicantRows = new List<ApplicantRowView>();

        private SortColumn currentSortColumn = SortColumn.None;
        private bool sortAscending = true;
        private int fallbackTurn;
        private string lastActionMessage = "";

        private void Awake()
        {
            if (employeeService == null)
            {
                employeeService = FindFirstObjectByType<EmployeeService>();
            }

            if (gameTurnClock == null)
            {
                gameTurnClock = FindFirstObjectByType<GameTurnClock>();
            }

            if (actionPointService == null)
            {
                actionPointService = FindFirstObjectByType<ActionPointService>();
            }

            AutoRecoverReferences();
            BindButtons();

            if (detailView != null)
            {
                detailView.Close();
            }
        }

        private void AutoRecoverReferences()
        {
            if (employeeListContent == null)
            {
                Transform t = transform.Find("EmployeeTableSection/EmployeeScrollView/Viewport/Content");
                if (t != null) employeeListContent = t;
            }

            if (applicantListContent == null)
            {
                Transform t = transform.Find("RecruitmentSection/ApplicantScrollView/Viewport/Content");
                if (t != null) applicantListContent = t;
            }

            if (applicantArrivalText == null)
            {
                Transform t = transform.Find("RecruitmentSection/ApplicantArrivalText");
                if (t != null) applicantArrivalText = t.GetComponent<TextMeshProUGUI>();
            }

            if (closeApplicantsButton == null)
            {
                Transform t = transform.Find("RecruitmentSection/CloseApplicantsButton");
                if (t != null) closeApplicantsButton = t.GetComponent<Button>();
            }

            if (emptyEmployeeText == null)
            {
                Transform t = transform.Find("EmployeeTableSection/EmployeeScrollView/Viewport/EmptyEmployeeText");
                if (t != null) emptyEmployeeText = t.GetComponent<TextMeshProUGUI>();
            }

            if (emptyApplicantText == null)
            {
                Transform t = transform.Find("RecruitmentSection/ApplicantScrollView/Viewport/EmptyApplicantText");
                if (t != null) emptyApplicantText = t.GetComponent<TextMeshProUGUI>();
            }

            if (detailView == null)
            {
                Transform t = transform.Find("EmployeeDetailPopup");
                if (t != null) detailView = t.GetComponent<EmployeeDetailView>();
            }

#if UNITY_EDITOR
            if (employeeRowPrefab == null)
            {
                employeeRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<EmployeeRowView>("Assets/Employees/Prefabs/EmployeeRowPrefab.prefab");
            }

            if (applicantRowPrefab == null)
            {
                applicantRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<ApplicantRowView>("Assets/Employees/Prefabs/ApplicantRowPrefab.prefab");
            }
#endif
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void BindButtons()
        {
            if (recruitButton != null)
            {
                recruitButton.onClick.RemoveAllListeners();
                recruitButton.onClick.AddListener(HandleRecruitClicked);
            }

            if (closeApplicantsButton != null)
            {
                closeApplicantsButton.onClick.RemoveAllListeners();
                closeApplicantsButton.onClick.AddListener(HandleCloseApplicantsClicked);
            }

            if (debugAdvanceTurnButton != null)
            {
                debugAdvanceTurnButton.onClick.RemoveAllListeners();
                debugAdvanceTurnButton.onClick.AddListener(HandleDebugAdvanceTurn);
            }

            BindSortButton(sortNameButton, SortColumn.Name);
            BindSortButton(sortGradeButton, SortColumn.Grade);
            BindSortButton(sortAnalysisButton, SortColumn.Analysis);
            BindSortButton(sortNetworkButton, SortColumn.Network);
            BindSortButton(sortEyeButton, SortColumn.Eye);
            BindSortButton(sortLegworkButton, SortColumn.Legwork);
            BindSortButton(sortManagementButton, SortColumn.Management);
            BindSortButton(sortSalaryButton, SortColumn.Salary);
            BindSortButton(sortDiligenceButton, SortColumn.Diligence);
            BindSortButton(sortLoyaltyButton, SortColumn.Loyalty);
            BindSortButton(sortAmbitionButton, SortColumn.Ambition);
            BindSortButton(sortPotentialButton, SortColumn.Potential);
        }

        private void BindSortButton(Button btn, SortColumn column)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ToggleSort(column));
        }

        private void SubscribeEvents()
        {
            if (employeeService != null)
            {
                employeeService.OnApplicantsArrived += HandleApplicantsArrived;
                employeeService.OnStatRevealed += HandleStatRevealed;
                employeeService.OnGradeChanged += HandleGradeChanged;
                employeeService.OnEmployeeLeft += HandleEmployeeLeft;
                employeeService.OnContractExpired += HandleContractExpired;
            }

            if (gameTurnClock != null)
            {
                gameTurnClock.TurnAdvanced += HandleClockTurnAdvanced;
            }
        }

        private void UnsubscribeEvents()
        {
            if (employeeService != null)
            {
                employeeService.OnApplicantsArrived -= HandleApplicantsArrived;
                employeeService.OnStatRevealed -= HandleStatRevealed;
                employeeService.OnGradeChanged -= HandleGradeChanged;
                employeeService.OnEmployeeLeft -= HandleEmployeeLeft;
                employeeService.OnContractExpired -= HandleContractExpired;
            }

            if (gameTurnClock != null)
            {
                gameTurnClock.TurnAdvanced -= HandleClockTurnAdvanced;
            }
        }

        public void RefreshAll()
        {
            RefreshHeaderAndRecruitState();
            RefreshEmployeeTable();
            RefreshApplicantTable();
            RefreshDebugBar();
        }

        private void RefreshHeaderAndRecruitState()
        {
            int count = employeeService != null ? employeeService.Employees.Count : 0;
            int cap = employeeService != null ? employeeService.Capacity : 5;

            if (employeeCountText != null)
            {
                employeeCountText.text = $"직원 {count} / 상한 {cap}";
            }

            bool isRecruiting = employeeService != null && employeeService.IsRecruiting;
            int apCost = (employeeService != null && employeeService.Config != null)
                ? employeeService.Config.recruitApCost
                : 5;

            if (recruitButton != null)
            {
                recruitButton.interactable = !isRecruiting;
            }

            if (recruitButtonText != null)
            {
                recruitButtonText.text = isRecruiting
                    ? "모집 중입니다"
                    : $"직원 모집 (AP {apCost})";
            }
        }

        private void RefreshEmployeeTable()
        {
            for (int i = 0; i < spawnedEmployeeRows.Count; i++)
            {
                if (spawnedEmployeeRows[i] != null)
                {
                    Destroy(spawnedEmployeeRows[i].gameObject);
                }
            }
            spawnedEmployeeRows.Clear();

            if (employeeService == null || employeeListContent == null)
            {
                return;
            }

            IEnumerable<Employee> ordered = GetSortedEmployees(employeeService.Employees);
            int rendered = 0;

            if (employeeRowPrefab != null)
            {
                foreach (Employee emp in ordered)
                {
                    EmployeeRowView row = Instantiate(employeeRowPrefab, employeeListContent);
                    row.gameObject.SetActive(true);
                    row.transform.localScale = Vector3.one;
                    row.Bind(emp, employeeService, HandleEmployeeRowClicked);
                    spawnedEmployeeRows.Add(row);
                    rendered++;
                }
            }

            if (emptyEmployeeText != null)
            {
                emptyEmployeeText.gameObject.SetActive(rendered == 0);
            }

            if (employeeListContent is RectTransform contentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }

        private void RefreshApplicantTable()
        {
            for (int i = 0; i < spawnedApplicantRows.Count; i++)
            {
                if (spawnedApplicantRows[i] != null)
                {
                    Destroy(spawnedApplicantRows[i].gameObject);
                }
            }
            spawnedApplicantRows.Clear();

            if (employeeService == null)
            {
                return;
            }

            IReadOnlyList<Employee> list = employeeService.Applicants;
            bool hasApplicants = list != null && list.Count > 0;
            bool canHireMore = employeeService.Employees.Count < employeeService.Capacity;

            if (applicantArrivalText != null)
            {
                applicantArrivalText.gameObject.SetActive(hasApplicants);
                if (hasApplicants)
                {
                    applicantArrivalText.text = $"{list.Count}명이 지원했습니다";
                }
            }

            if (closeApplicantsButton != null)
            {
                closeApplicantsButton.gameObject.SetActive(hasApplicants);
            }

            if (hasApplicants && applicantListContent != null && applicantRowPrefab != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    ApplicantRowView row = Instantiate(applicantRowPrefab, applicantListContent);
                    row.gameObject.SetActive(true);
                    row.transform.localScale = Vector3.one;
                    row.Bind(list[i], canHireMore, HandleHireApplicantClicked);
                    spawnedApplicantRows.Add(row);
                }
            }

            if (emptyApplicantText != null)
            {
                emptyApplicantText.gameObject.SetActive(!hasApplicants);
                if (!hasApplicants)
                {
                    emptyApplicantText.text = employeeService.IsRecruiting
                        ? "모집이 진행 중입니다. 턴을 진행하면 지원자가 도착합니다."
                        : "현재 지원자가 없습니다.";
                }
            }

            if (applicantListContent is RectTransform contentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }

        private void ToggleSort(SortColumn column)
        {
            if (currentSortColumn == column)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                currentSortColumn = column;
                sortAscending = column == SortColumn.Name;
            }

            RefreshEmployeeTable();
        }

        private IEnumerable<Employee> GetSortedEmployees(IReadOnlyList<Employee> source)
        {
            if (currentSortColumn == SortColumn.None || source == null)
            {
                return source ?? Array.Empty<Employee>();
            }

            Func<Employee, object> keySelector = emp =>
            {
                switch (currentSortColumn)
                {
                    case SortColumn.Name: return emp.Name;
                    case SortColumn.Grade: return (int)emp.Grade;
                    case SortColumn.Analysis: return emp.Analysis.Current;
                    case SortColumn.Network: return emp.Network.Current;
                    case SortColumn.Eye: return emp.Eye.Current;
                    case SortColumn.Legwork: return emp.Legwork.Current;
                    case SortColumn.Management: return emp.Management.Current;
                    case SortColumn.Salary: return emp.MonthlySalary;
                    case SortColumn.Diligence: return GetSortableHidden(emp, HiddenStatType.Diligence);
                    case SortColumn.Loyalty: return GetSortableHidden(emp, HiddenStatType.Loyalty);
                    case SortColumn.Ambition: return GetSortableHidden(emp, HiddenStatType.Ambition);
                    case SortColumn.Potential: return GetSortableHidden(emp, HiddenStatType.Potential);
                    default: return 0;
                }
            };

            return sortAscending
                ? source.OrderBy(keySelector)
                : source.OrderByDescending(keySelector);
        }

        private int GetSortableHidden(Employee emp, HiddenStatType type)
        {
            if (emp == null || employeeService == null) return -1;
            return employeeService.IsRevealed(emp, type) ? employeeService.GetHiddenStat(emp, type) : -1;
        }

        private void HandleRecruitClicked()
        {
            if (employeeService == null)
            {
                return;
            }

            if (employeeService.StartRecruit())
            {
                lastActionMessage = "모집 시작됨";
                RefreshAll();
            }
            else
            {
                lastActionMessage = "모집 실패 (AP 부족 또는 이미 모집 중)";
                RefreshDebugBar();
            }
        }

        private void HandleHireApplicantClicked(Employee applicant)
        {
            if (employeeService == null || applicant == null)
            {
                return;
            }

            if (employeeService.Hire(applicant))
            {
                lastActionMessage = $"{applicant.Name} 고용 완료 (총 {employeeService.Employees.Count}명)";
                RefreshAll();
            }
            else
            {
                lastActionMessage = $"고용 실패 (현재 {employeeService.Employees.Count}/{employeeService.Capacity})";
                RefreshDebugBar();
            }
        }

        private void HandleCloseApplicantsClicked()
        {
            if (employeeService == null)
            {
                return;
            }

            employeeService.CloseApplicants();
            lastActionMessage = "지원자 목록 닫음";
            RefreshAll();
        }

        private void HandleEmployeeRowClicked(Employee emp)
        {
            if (detailView != null && emp != null)
            {
                detailView.Open(emp, employeeService, GetCurrentTurn, RefreshAll);
            }
        }

        private void HandleApplicantsArrived()
        {
            int arrivedCount = employeeService != null ? employeeService.Applicants.Count : 0;
            lastActionMessage = $"지원자 {arrivedCount}명 도착";
            RefreshAll();
        }

        private void HandleStatRevealed(Employee emp, HiddenStatType statType)
        {
            for (int i = 0; i < spawnedEmployeeRows.Count; i++)
            {
                if (spawnedEmployeeRows[i] != null && spawnedEmployeeRows[i].BoundEmployee == emp)
                {
                    spawnedEmployeeRows[i].Refresh();
                }
            }

            if (detailView != null)
            {
                detailView.RefreshIfShowing(emp);
            }
        }

        private void HandleGradeChanged(Employee emp)
        {
            RefreshEmployeeTable();
            if (detailView != null)
            {
                detailView.RefreshIfShowing(emp);
            }
        }

        private void HandleEmployeeLeft(Employee emp)
        {
            if (detailView != null)
            {
                detailView.RefreshIfShowing(emp);
            }
            RefreshAll();
        }

        private void HandleContractExpired(Employee emp)
        {
            RefreshAll();
        }

        private void HandleClockTurnAdvanced(int newTurn)
        {
            fallbackTurn = newTurn;
            RefreshAll();
            if (detailView != null)
            {
                detailView.Refresh();
            }
        }

        private void HandleDebugAdvanceTurn()
        {
            if (gameTurnClock != null)
            {
                string[] candidateNames = { "AdvanceTurn", "NextTurn", "Advance", "Step", "Tick" };
                MethodInfo[] methods = typeof(GameTurnClock).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (string targetName in candidateNames)
                {
                    MethodInfo match = methods.FirstOrDefault(m =>
                        m.Name == targetName &&
                        m.GetParameters().All(p => p.IsOptional));

                    if (match != null)
                    {
                        ParameterInfo[] parameters = match.GetParameters();
                        object[] args = parameters.Length == 0
                            ? null
                            : parameters.Select(p => p.DefaultValue).ToArray();
                        match.Invoke(gameTurnClock, args);
                        RefreshAll();
                        if (detailView != null) detailView.Refresh();
                        return;
                    }
                }
            }

            fallbackTurn++;
            if (employeeService != null)
            {
                MethodInfo handleTurn = typeof(EmployeeService).GetMethod("HandleTurnAdvanced", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                handleTurn?.Invoke(employeeService, new object[] { fallbackTurn });
            }
            RefreshAll();
            if (detailView != null)
            {
                detailView.Refresh();
            }
        }

        private void RefreshDebugBar()
        {
            if (debugStatusText == null)
            {
                return;
            }

            int turn = GetCurrentTurn();
            string apInfo = ReadActionPointText();
            string statusSuffix = string.IsNullOrEmpty(lastActionMessage) ? "" : $"  |  {lastActionMessage}";
            debugStatusText.text = $"[테스트 바] 현재 턴: {turn}  |  {apInfo}{statusSuffix}";
        }

        private int GetCurrentTurn()
        {
            if (gameTurnClock != null)
            {
                PropertyInfo prop = typeof(GameTurnClock).GetProperty("CurrentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(int))
                {
                    return (int)prop.GetValue(gameTurnClock);
                }

                FieldInfo field = typeof(GameTurnClock).GetField("currentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? typeof(GameTurnClock).GetField("turn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null && field.FieldType == typeof(int))
                {
                    return (int)field.GetValue(gameTurnClock);
                }
            }

            return fallbackTurn;
        }

        private string ReadActionPointText()
        {
            if (actionPointService == null)
            {
                return "AP: 미연결";
            }

            PropertyInfo curProp = typeof(ActionPointService).GetProperty("CurrentAP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(ActionPointService).GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo maxProp = typeof(ActionPointService).GetProperty("MaxAP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(ActionPointService).GetProperty("Max", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (curProp != null && curProp.PropertyType == typeof(int))
            {
                int cur = (int)curProp.GetValue(actionPointService);
                int max = (maxProp != null && maxProp.PropertyType == typeof(int)) ? (int)maxProp.GetValue(actionPointService) : cur;
                return $"AP: {cur} / {max}";
            }

            return "AP: 연결됨";
        }

        public static string FormatGrade(CareerGrade grade)
        {
            switch (grade.ToString())
            {
                case "Intern": return "인턴";
                case "Junior":
                case "Staff": return "사원";
                case "Senior":
                case "AssistantManager": return "대리";
                case "Manager": return "과장";
                case "Director": return "부장";
                case "Executive": return "임원";
                default: return grade.ToString();
            }
        }
    }
}