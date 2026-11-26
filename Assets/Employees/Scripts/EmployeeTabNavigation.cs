using UnityEngine;
using UnityEngine.UI;
namespace TakeOver.Employees
{
    public class EmployeeTabNavigation : MonoBehaviour
    {
        [SerializeField] private RectTransform designFrame;
        [SerializeField] private GameObject employeePage, applicantPage, newBadge;
        [SerializeField] private Button employeeButton, applicantButton;
        [SerializeField] private Sprite normalSprite, selectedSprite;
        private EmployeeService service;
        private bool applicantsSelected, unseen;
        private void Awake()
        {
            service = FindFirstObjectByType<EmployeeService>();
            if (service != null) { service.OnApplicantsArrived += OnArrived; unseen = service.Applicants.Count > 0; }
            employeeButton.onClick.AddListener(() => SelectPage(false));
            applicantButton.onClick.AddListener(() => SelectPage(true));
            SelectPage(false);
        }
        private void OnEnable()
        {
            if (applicantsSelected) unseen = false;
            RefreshSelection();
        }
        private void OnDestroy() { if (service != null) service.OnApplicantsArrived -= OnArrived; }
        private void OnArrived() { unseen = !applicantsSelected || !gameObject.activeInHierarchy; RefreshSelection(); }
        public void SelectPage(bool applicants)
        {
            applicantsSelected = applicants;
            if (applicants) unseen = false;
            RefreshSelection();
        }
        private void RefreshSelection()
        {
            employeePage.SetActive(!applicantsSelected);
            applicantPage.SetActive(applicantsSelected);
            employeeButton.image.sprite = applicantsSelected ? normalSprite : selectedSprite;
            applicantButton.image.sprite = applicantsSelected ? selectedSprite : normalSprite;
            newBadge.SetActive(unseen);
        }
        private void LateUpdate()
        {
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            float scale = Mathf.Min(canvasRect.rect.width / 1920f, canvasRect.rect.height / 1080f);
            designFrame.position = canvasRect.TransformPoint(canvasRect.rect.center);
            Vector3 parentScale = designFrame.parent.lossyScale;
            designFrame.localScale = new Vector3(canvasRect.lossyScale.x / parentScale.x * scale,
                canvasRect.lossyScale.y / parentScale.y * scale, 1);
        }
    }
}
