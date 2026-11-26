using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TakeOver.Employees
{
    public sealed class AppraisalCompanyPicker : MonoBehaviour
    {
        [SerializeField] private CompanyRow rowPrefab;
        [SerializeField] private GameObject modal;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_Text heading;
        [SerializeField] private Button confirm, cancel;
        private readonly List<Company> selected = new List<Company>();
        private readonly Dictionary<Company, EmployeeCompanySelectionView> rows = new Dictionary<Company, EmployeeCompanySelectionView>();
        private Employee employee;
        private Action assigned;
        private EmployeeDetailView suspendedDetail;
        private void Awake()
        {
            confirm.onClick.AddListener(commit);
            cancel.onClick.AddListener(close);
            modal.SetActive(false);
        }
        public void open(Employee employee, Action assigned)
        {
            this.employee = employee;
            this.assigned = assigned;
            suspendedDetail = GetComponentInChildren<EmployeeDetailView>();
            if (suspendedDetail != null) suspendedDetail.gameObject.SetActive(false);
            selected.Clear();
            rows.Clear();
            foreach (Transform child in content) Destroy(child.gameObject);
            foreach (Company company in FindFirstObjectByType<Companies>().CompanyList)
            {
                CompanyRow row = Instantiate(rowPrefab,content,false);
                row.gameObject.SetActive(true);
                row.setup(company,toggle);
                rows.Add(company,row.GetComponent<EmployeeCompanySelectionView>());
            }
            modal.SetActive(true);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition=1;
            refresh();
        }
        private void toggle(Company company)
        {
            if (!selected.Remove(company))
            {
                if (selected.Count == MarketAppraisal.getCompanyLimit(employee.Legwork.Current)) return;
                selected.Add(company);
            }
            refresh();
        }
        private void refresh()
        {
            heading.text = $"조사할 회사 선택 {selected.Count}/{MarketAppraisal.getCompanyLimit(employee.Legwork.Current)}";
            foreach (var pair in rows) pair.Value.SetSelected(selected.Contains(pair.Key));
            confirm.interactable = selected.Count > 0;
        }
        private void commit()
        {
            if (!FindFirstObjectByType<JobService>().Assign(employee,JobType.Appraisal,selected)) return;
            assigned?.Invoke();
            close();
        }
        private void close()
        {
            modal.SetActive(false);
            if (suspendedDetail != null) suspendedDetail.gameObject.SetActive(true);
            suspendedDetail = null;
        }
    }
}
