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
        [SerializeField] private Text arrivalDate;
        [SerializeField] private Text approveLabel;
        [SerializeField] private Text rejectLabel;
        [SerializeField] private GameObject doneButton;
        [SerializeField] private Text doneLabel;
        [SerializeField] private Transform infoRoot;
        [SerializeField] private DocumentInfoRow infoTemplate;
        [SerializeField] private Transform fieldsRoot;
        [SerializeField] private RectTransform pageControlsRoot;
        [SerializeField] private DocumentFieldView fieldTemplate;
        [SerializeField] private DocumentChoiceFieldView choiceFieldTemplate;
        [SerializeField] private Text errorText;
        [SerializeField] private Button approve;
        [SerializeField] private Button reject;
        private readonly List<GameObject> fields = new List<GameObject>();
        private readonly List<GameObject> infoRows = new List<GameObject>();
        private readonly List<DocumentInfo> deferredNotices = new List<DocumentInfo>();
        private IDocument document;
        private ResolvedDocument resolvedDocument;
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
        public void Show(IDocument doc, ResolvedDocument resolved = null, int arrival = 0, int arrivalTurn = 0)
        {
            if (document != doc) currentPage = 0;
            document = doc;
            resolvedDocument = resolved;
            readOnly = resolved != null;
            IApprovalDocument approval = doc as IApprovalDocument;
            documentRoot.SetActive(true);
            emptyMessage.SetActive(false);
            errorText.gameObject.SetActive(false);
            title.text = doc.Title;
            documentNumber.text = "NO. " + numberFor(doc, arrival);
            if (arrivalDate != null)
            {
                arrivalDate.gameObject.SetActive(arrivalTurn > 0);
                arrivalDate.text = arrivalTurn > 0 ? GameCalendar.Format(arrivalTurn) : "";
            }
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
            if (doc.BodyId != "generic") Debug.LogWarning($"알 수 없는 문서 본문 {doc.BodyId}: 범용 본문을 사용합니다.");
            configurePages(doc);
            refreshInfo(doc, resolved);
            refreshFields(doc, resolved);
            bool isLastPage = currentPage == pageCount - 1;
            doneButton.SetActive(readOnly && isLastPage);
            approve.gameObject.SetActive(!readOnly && isLastPage);
            reject.gameObject.SetActive(!readOnly && approval != null && isLastPage);
            refreshPagedApprovalState();
            Canvas.ForceUpdateCanvases();
            var informationScroll = infoRoot.GetComponentInParent<ScrollRect>();
            if (informationScroll != null) informationScroll.verticalNormalizedPosition = 1f;
        }

        // Only used with a temporary tutorial document, never registered in the real inbox.
        public void ShowTutorialPreview()
        {
            readOnly = true;
            title.text = document.Title;
            documentNumber.text = "NO. 1";
            approve.gameObject.SetActive(true);
            reject.gameObject.SetActive(document is IApprovalDocument);
            doneButton.SetActive(false);
            if (arrivalDate != null)
            {
                arrivalDate.gameObject.SetActive(true);
                arrivalDate.text = GameCalendar.Format(clock.CurrentTurn);
            }
            foreach (var input in fieldsRoot.GetComponentsInChildren<Selectable>()) input.interactable = false;
        }

        private void refreshInfo(IDocument doc, ResolvedDocument resolved)
        {
            foreach (var row in infoRows) { row.SetActive(false); Destroy(row); }
            infoRows.Clear();
            infoTemplate.gameObject.SetActive(false);
            IDocumentPages pages = doc as IDocumentPages;
            var rows = pages != null ? pages.GetInfoRows(currentPage) : doc.InfoRows;
            var noticeRows = new List<DocumentInfo>();
            for (int i = 0; i < rows.Count; i++)
            {
                DocumentInfo info = rows[i];
                if (info.isNotice)
                {
                    if (resolved == null) noticeRows.Add(info);
                    continue;
                }
                addInfo(info);
                if (doc is EmployeeRenewalDocument renewal && renewal.IsStatComparisonRow(info))
                {
                    documentNumber.text = $"NO. {renewal.DocumentNumber}";
                    if (infoRows.Count > 0) infoRows[infoRows.Count - 1].GetComponent<DocumentInfoRow>().BindEmployeeRenewalHover(renewal);
                }
            }
            if (doc is IApprovalDocument approval && !(doc is StartupBusinessPlanDocument && noticeRows.Count > 0))
                foreach (var line in approval.BodyLines)
                    if (!string.IsNullOrWhiteSpace(line) && !rows.Any(row => row.value == line || row.label + ": " + row.value == line)) addInfo(new DocumentInfo("", line));
            foreach (DocumentInfo notice in noticeRows)
            {
                if (doc is StartupBusinessPlanDocument) deferredNotices.Add(notice);
                else addInfo(notice);
            }
            if (pages != null && pages.PageCount > 1)
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
                addInfo(new DocumentInfo("처리 결과", resolved.approved ? "승인" : "거절"));
                addInfo(new DocumentInfo("처리일", GameCalendar.Format(resolved.resolvedTurn)));
            }
            fieldsRoot.SetAsLastSibling();
        }

        private void configurePages(IDocument doc)
        {
            IDocumentPages pages = doc as IDocumentPages;
            pageCount = pages != null ? Math.Max(1, pages.PageCount) : 1;
            if (pageCount > 1 && pagination == null) createPageControls();
            if (pagination == null) return;
            pagination.SetActive(pageCount > 1);
            refreshPageControls();
        }

        private void createPageControls()
        {
            if (pageControlsRoot == null) return;
            pagination = pageControlsRoot.gameObject;
            previousPage = createPageButton("Previous page", "‹", 1039f, -1);
            nextPage = createPageButton("Next page", "›", 1470f, 1);
            pageLabel = Instantiate(sender, pageControlsRoot, false);
            pageLabel.name = "Page number";
            pageLabel.alignment = TextAnchor.MiddleCenter;
            pageLabel.fontSize = 24;
            pageLabel.fontStyle = FontStyle.Bold;
            pageLabel.color = new Color32(246, 246, 246, 255);
            pageLabel.raycastTarget = false;
            pageLabel.gameObject.SetActive(true);
            placePageControl(pageLabel.rectTransform, 1185f, -935f, 200f, 40f);
        }

        private UnityEngine.UI.Button createPageButton(string name, string glyph, float x, int delta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(pageControlsRoot, false);
            var rect = (RectTransform)go.transform;
            placePageControl(rect, x, -920f, 60f, 60f);
            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => changePage(delta));
            GameObject chevron = createPageChevron(go.transform, delta > 0);
            if (delta < 0) previousPageChevron = chevron.GetComponent<CanvasGroup>();
            else nextPageChevron = chevron.GetComponent<CanvasGroup>();
            return button;
        }

        private static GameObject createPageChevron(Transform parent, bool pointsLeft)
        {
            var root = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(28f, 48f);
            createChevronStroke(rect, "Upper shadow outer", new Vector2(0f, 9f), pointsLeft ? 46f : -46f, 18f, new Color(0f, 0f, 0f, .06f), -5f);
            createChevronStroke(rect, "Upper shadow middle", new Vector2(0f, 9f), pointsLeft ? 46f : -46f, 12f, new Color(0f, 0f, 0f, .08f), -5f);
            createChevronStroke(rect, "Upper shadow inner", new Vector2(0f, 9f), pointsLeft ? 46f : -46f, 8f, new Color(0f, 0f, 0f, .12f), -5f);
            createChevronStroke(rect, "Lower shadow outer", new Vector2(0f, -9f), pointsLeft ? -46f : 46f, 18f, new Color(0f, 0f, 0f, .06f), -5f);
            createChevronStroke(rect, "Lower shadow middle", new Vector2(0f, -9f), pointsLeft ? -46f : 46f, 12f, new Color(0f, 0f, 0f, .08f), -5f);
            createChevronStroke(rect, "Lower shadow inner", new Vector2(0f, -9f), pointsLeft ? -46f : 46f, 8f, new Color(0f, 0f, 0f, .12f), -5f);
            createChevronStroke(rect, "Upper stroke", new Vector2(0f, 9f), pointsLeft ? 46f : -46f, 6f, new Color32(246, 246, 246, 255), 0f);
            createChevronStroke(rect, "Lower stroke", new Vector2(0f, -9f), pointsLeft ? -46f : 46f, 6f, new Color32(246, 246, 246, 255), 0f);
            return root;
        }

        private static void createChevronStroke(RectTransform parent, string name, Vector2 position, float angle, float width, Color color, float shadowY)
        {
            var stroke = new GameObject(name, typeof(RectTransform), typeof(Image));
            stroke.transform.SetParent(parent, false);
            var rect = (RectTransform)stroke.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, 28f);
            rect.anchoredPosition = position + new Vector2(0f, shadowY);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = stroke.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void placePageControl(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void changePage(int delta)
        {
            currentPage += delta;
            refreshInfo(document, resolvedDocument);
            refreshFields(document, resolvedDocument);
            bool isLastPage = currentPage == pageCount - 1;
            doneButton.SetActive(readOnly && isLastPage);
            approve.gameObject.SetActive(!readOnly && isLastPage);
            reject.gameObject.SetActive(!readOnly && document is IApprovalDocument && isLastPage);
            refreshPagedApprovalState();
            refreshPageControls();
        }

        private void refreshPageControls()
        {
            if (pageLabel == null) return;
            pageLabel.text = $"{currentPage + 1}/{pageCount}";
            previousPage.interactable = currentPage > 0;
            nextPage.interactable = currentPage < pageCount - 1;
            previousPageChevron.alpha = currentPage > 0 ? 1f : .35f;
            nextPageChevron.alpha = currentPage < pageCount - 1 ? 1f : .35f;
        }

        private CanvasGroup previousPageChevron;
        private CanvasGroup nextPageChevron;

        private void addInfo(DocumentInfo info)
        {
            var row = Instantiate(infoTemplate, infoRoot, false);
            row.gameObject.SetActive(true);
            row.Bind(info);
            infoRows.Add(row.gameObject);
        }

        private void refreshPagedApprovalState()
        {
            if (!(document is IDocumentPages) || !(document is IApprovalDocument approval)) return;
            approve.interactable = !readOnly && currentPage == pageCount - 1 && approval.CanApprove(out _);
        }

        private void refreshFields(IDocument doc, ResolvedDocument resolved)
        {
            foreach (var field in fields) { field.SetActive(false); Destroy(field); }
            fields.Clear();
            fieldTemplate.gameObject.SetActive(false);
            choiceFieldTemplate.gameObject.SetActive(false);
            if (!(doc is IApprovalDocument approval)) return;

            IDocumentPages pages = doc as IDocumentPages;
            IReadOnlyList<DocumentField> numericFields = pages != null ? pages.GetFields(currentPage) : approval.Fields;
            IReadOnlyList<DocumentChoiceField> choiceFields = pages != null ? pages.GetChoiceFields(currentPage) : approval.ChoiceFields;
            foreach (DocumentField field in numericFields)
            {
                float value = field.value;
                if (resolved != null)
                    foreach (NameValue saved in resolved.fields)
                        if (saved.name == field.name) { value = saved.value; break; }
                var view = Instantiate(fieldTemplate, fieldsRoot, false);
                view.gameObject.SetActive(true);
                view.Bind(approval, field, readOnly, value, () =>
                {
                    refreshInfo(doc, resolvedDocument);
                    refreshPagedApprovalState();
                });
                fields.Add(view.gameObject);
            }
            foreach (DocumentChoiceField field in choiceFields)
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
                    refreshInfo(doc, resolvedDocument);
                    refreshPagedApprovalState();
                });
                fields.Add(view.gameObject);
            }
            foreach (DocumentInfo notice in deferredNotices)
            {
                var noticeRow = Instantiate(infoTemplate, fieldsRoot, false);
                noticeRow.gameObject.SetActive(true);
                noticeRow.Bind(notice);
                fields.Add(noticeRow.gameObject);
            }
            deferredNotices.Clear();
            fieldsRoot.SetAsLastSibling();
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
