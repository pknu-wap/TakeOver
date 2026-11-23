using System;
using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject documentRoot;
        [SerializeField] private GameObject emptyMessage;
        [SerializeField] private Text title;
        [SerializeField] private Text sender;
        [SerializeField] private Text deadline;
        [SerializeField] private Text documentNumber;
        [SerializeField] private Text approveLabel;
        [SerializeField] private Text rejectLabel;
        [SerializeField] private GameObject doneButton;
        [SerializeField] private Text doneLabel;
        [SerializeField] private Transform infoRoot;
        [SerializeField] private DocumentInfoRow infoTemplate;
        [SerializeField] private Transform fieldsRoot;
        [SerializeField] private DocumentFieldView fieldTemplate;
        [SerializeField] private DocumentChoiceFieldView choiceFieldTemplate;
        [SerializeField] private Text errorText;
        [SerializeField] private Button approve;
        [SerializeField] private Button reject;
        private readonly List<GameObject> fields = new List<GameObject>();
        private readonly List<GameObject> infoRows = new List<GameObject>();
        private IDocument document;
        private bool readOnly;
        private GameTurnClock clock;
        private GameObject pagination;
        private UnityEngine.UI.Button previousPage;
        private UnityEngine.UI.Button nextPage;
        private UnityEngine.UI.Text pageLabel;
        private int currentPage;
        private int pageCount;
        public event Action<IDocument, bool> Decided;

        private void Awake()
        {
            clock = FindFirstObjectByType<GameTurnClock>();
            approve.onClick.AddListener(() => decide(true));
            reject.onClick.AddListener(() => decide(false));
        }

        public void Clear()
        {
            document = null;
            if (pagination != null) pagination.SetActive(false);
            documentRoot.SetActive(false);
            emptyMessage.SetActive(true);
        }

        // readOnly = 이미 처리한(읽은) 문서. 읽기 전용 문서(approval == null)는 기한·D-day·거절·입력 칸이 없다.
        public void Show(IDocument doc, ResolvedDocument resolved = null, int arrival = 0)
        {
            if (document != doc) currentPage = 0;
            document = doc;
            readOnly = resolved != null;
            IApprovalDocument approval = doc as IApprovalDocument;
            documentRoot.SetActive(true);
            emptyMessage.SetActive(false);
            errorText.gameObject.SetActive(false);
            title.text = doc.Title;
            documentNumber.text = "NO. " + numberFor(doc, arrival);
            sender.gameObject.SetActive(!string.IsNullOrEmpty(doc.Author));
            sender.text = $"<color=#2C507A>작성자</color> {doc.Author}";
            if (approval != null)
            {
                int remaining = approval.DeadlineTurn - clock.CurrentTurn;
                deadline.text = readOnly ? "완료" : remaining < 0 ? "기한 지남" : remaining == 0 ? "오늘 마감" : $"D-{remaining}";
            }
            else deadline.text = "";
            deadline.color = new Color(.88f, .08f, .11f);
            if (approval != null)
            {
                approveLabel.text = string.IsNullOrEmpty(approval.ApproveLabel) ? "승인" : approval.ApproveLabel;
                rejectLabel.text = string.IsNullOrEmpty(approval.RejectLabel) ? "거절" : approval.RejectLabel;
                doneLabel.text = string.IsNullOrEmpty(approval.DoneLabel)
                    ? resolved?.approved == true ? "승인 완료" : "거절 완료" : approval.DoneLabel;
            }
            else
            {
                approveLabel.text = "확인";
                doneLabel.text = "확인 완료";
            }
            doneButton.SetActive(readOnly);
            if (doc.BodyId != "generic") Debug.LogWarning($"알 수 없는 문서 본문 {doc.BodyId}: 범용 본문을 사용합니다.");
            refreshInfo(doc, resolved);
            configurePages(doc);
            foreach (var field in fields) { field.SetActive(false); Destroy(field); }
            fields.Clear();
            fieldTemplate.gameObject.SetActive(false);
            choiceFieldTemplate.gameObject.SetActive(false);
            if (approval != null)
            {
                foreach (DocumentField field in approval.Fields)
                {
                    float value = field.value;
                    if (resolved != null)
                        foreach (NameValue saved in resolved.fields)
                            if (saved.name == field.name) { value = saved.value; break; }
                    var view = Instantiate(fieldTemplate, fieldsRoot, false);
                    view.gameObject.SetActive(true);
                    view.Bind(approval, field, readOnly, value, () => refreshInfo(doc, resolved));
                    fields.Add(view.gameObject);
                }
                foreach (DocumentChoiceField field in approval.ChoiceFields)
                {
                    int? value = field.value;
                    if (resolved != null)
                    {
                        value = null;
                        foreach (NameValue saved in resolved.fields)
                            if (saved.name == field.name) { value = Mathf.RoundToInt(saved.value); break; }
                    }
                    var view = Instantiate(choiceFieldTemplate, fieldsRoot, false);
                    view.gameObject.SetActive(true);
                    view.Bind(approval, field, readOnly, value, () =>
                    {
                        errorText.gameObject.SetActive(false);
                        refreshInfo(doc, resolved);
                    });
                    fields.Add(view.gameObject);
                }
            }
            approve.gameObject.SetActive(!readOnly);
            reject.gameObject.SetActive(!readOnly && approval != null);
        }

        // Only called on the tutorial's disposable clone, after Show.
        public void ShowTutorialPreview()
        {
            readOnly = true;
            title.text = document.Title + " · 설명용 미리보기";
            deadline.text = "미리보기";
            deadline.color = sender.color;
            documentNumber.text = "설명용 예시";
            approve.gameObject.SetActive(false);
            reject.gameObject.SetActive(false);
            doneButton.SetActive(false);
            foreach (var input in fieldsRoot.GetComponentsInChildren<Selectable>()) input.interactable = false;
        }

        private void refreshInfo(IDocument doc, ResolvedDocument resolved)
        {
            foreach (var row in infoRows) { row.SetActive(false); Destroy(row); }
            infoRows.Clear();
            infoTemplate.gameObject.SetActive(false);
            var rows = doc.InfoRows;
            int rowsPerPage = doc is SnapshotDocument snapshot ? snapshot.RowsPerPage : 0;
            int start = rowsPerPage > 0 ? currentPage * rowsPerPage : 0;
            int end = rowsPerPage > 0 ? Math.Min(start + rowsPerPage, rows.Count) : rows.Count;
            for (int i = start; i < end; i++) addInfo(rows[i].label, rows[i].value);
            if (doc is IApprovalDocument approval)
                foreach (var line in approval.BodyLines)
                    if (!string.IsNullOrWhiteSpace(line) && !rows.Any(row => row.value == line || row.label + ": " + row.value == line)) addInfo("", line);
            if (rowsPerPage > 0)
            {
                Canvas.ForceUpdateCanvases();
                var scroll = infoRoot.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                if (scroll != null)
                {
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1f;
                }
            }
            if (resolved != null && !resolved.isReadOnly)
            {
                addInfo("처리 결과", resolved.approved ? "승인" : "거절");
                addInfo("처리한 턴", resolved.resolvedTurn.ToString());
            }
        }

        private void configurePages(IDocument doc)
        {
            int rowsPerPage = doc is SnapshotDocument snapshot ? snapshot.RowsPerPage : 0;
            pageCount = rowsPerPage > 0 ? Math.Max(1, (doc.InfoRows.Count + rowsPerPage - 1) / rowsPerPage) : 1;
            if (pageCount > 1 && pagination == null) createPageControls();
            if (pagination == null) return;
            pagination.SetActive(pageCount > 1);
            refreshPageControls();
        }

        private void createPageControls()
        {
            pagination = new GameObject("Document pages", typeof(RectTransform));
            pagination.transform.SetParent(fieldsRoot, false);
            pagination.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48f;
            previousPage = createPageButton("Previous page", "이전", 0f, -1);
            nextPage = createPageButton("Next page", "다음", 310f, 1);
            pageLabel = Instantiate(sender, pagination.transform, false);
            pageLabel.name = "Page number";
            pageLabel.alignment = TextAnchor.MiddleCenter;
            pageLabel.fontSize = 20;
            pageLabel.raycastTarget = false;
            pageLabel.gameObject.SetActive(true);
            placePageControl(pageLabel.rectTransform, 166f, 108f);
        }

        private UnityEngine.UI.Button createPageButton(string name, string label, float x, int delta)
        {
            var button = Instantiate(approve, pagination.transform, false);
            button.name = name;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => changePage(delta));
            button.GetComponentInChildren<UnityEngine.UI.Text>().text = label;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Automatic };
            placePageControl((RectTransform)button.transform, x, 132f);
            return button;
        }

        private static void placePageControl(RectTransform rect, float x, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 48f);
        }

        private void changePage(int delta)
        {
            currentPage += delta;
            refreshInfo(document, null);
            refreshPageControls();
        }

        private void refreshPageControls()
        {
            pageLabel.text = $"{currentPage + 1} / {pageCount}";
            previousPage.interactable = currentPage > 0;
            nextPage.interactable = currentPage < pageCount - 1;
        }

        private void addInfo(string label, string value)
        {
            var row = Instantiate(infoTemplate, infoRoot, false);
            row.gameObject.SetActive(true);
            row.Bind(label, value);
            infoRows.Add(row.gameObject);
        }

        private void decide(bool approved)
        {
            if (readOnly || document == null) return;
            if (approved && document is IApprovalDocument approval && !approval.CanApprove(out string reason))
            {
                errorText.text = reason;
                errorText.gameObject.SetActive(true);
                return;
            }
            Decided?.Invoke(document, approved);
        }

        private static string numberFor(IDocument doc, int arrival)
        {
            string key = doc is IApprovalDocument approval
                ? doc.TypeId + "|" + approval.TargetCompany?.definition.companyID + "|"
                    + approval.DeadlineTurn.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : doc.TypeId + "|" + arrival.ToString(System.Globalization.CultureInfo.InvariantCulture);
            uint hash = 2166136261;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes(key))
                hash = unchecked((hash ^ value) * 16777619);
            string digits = (hash % 100000000).ToString("D8");
            return digits.Substring(0, 4) + "-" + digits.Substring(4);
        }
    }
}
