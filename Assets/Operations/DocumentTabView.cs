using System.Collections.Generic;
using System.Linq;
using TakeOver.Documents;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class DocumentTabView : MonoBehaviour
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private DocumentCard rowTemplate;
        [SerializeField] private GameObject emptyMessage;
        [SerializeField] private DocumentDetailView detail;
        [SerializeField] private Button[] filterButtons;
        [SerializeField] private Text[] filterLabels;
        [SerializeField] private Image[] filterUnderlines;
        private readonly List<DocumentCard> cards = new List<DocumentCard>();
        private readonly List<IDocument> cardDocuments = new List<IDocument>();
        private DocumentInbox inbox;
        private GameTurnClock clock;
        private IDocument selected;
        private int filter;

        private void Awake()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int index = i;
                filterButtons[i].onClick.AddListener(() => { filter = index; refresh(); });
            }
        }

        private void Start()
        {
            var system = FindFirstObjectByType<DocumentSystem>();
            inbox = system != null ? system.Inbox : null;
            clock = FindFirstObjectByType<GameTurnClock>();
            if (inbox != null) inbox.Changed += refresh;
            clock.TurnAdvanced += onTurn;
            detail.Decided += decide;
            refresh();
        }

        private void OnEnable() { if (clock != null) refresh(); }
        private void OnDestroy()
        {
            if (inbox != null) inbox.Changed -= refresh;
            if (clock != null) clock.TurnAdvanced -= onTurn;
            detail.Decided -= decide;
        }

        private void onTurn(int turn) => refresh();
        private void decide(IDocument doc, bool approved)
        {
            if (doc is IApprovalDocument approval) inbox.Resolve(approval, approved);
            else inbox.MarkRead(doc);
        }

        private void refresh()
        {
            foreach (var card in cards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear();
            cardDocuments.Clear();
            rowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < filterButtons.Length; i++)
            {
                filterLabels[i].color = i == filter ? Color.white : new Color(.72f, .73f, .76f);
                filterUnderlines[i].gameObject.SetActive(i == filter);
            }
            if (inbox != null)
            {
                IEnumerable<IDocument> documents = filter == 1 ? inbox.Open
                    : filter == 2 ? inbox.History.Select(entry => entry.document)
                    : inbox.Open.Concat(inbox.History.Select(entry => entry.document)).OrderBy(inbox.GetArrival);
                foreach (IDocument doc in documents)
                {
                    var resolved = inbox.History.FirstOrDefault(entry => entry.document == doc);
                    IApprovalDocument approval = doc as IApprovalDocument;
                    string due;
                    bool overdue = false;
                    if (approval != null)
                    {
                        int remaining = approval.DeadlineTurn - clock.CurrentTurn;
                        due = resolved != null ? "완료" : remaining < 0 ? "기한 지남"
                            : remaining == 0 ? "오늘 마감" : $"D-{remaining}";
                        overdue = resolved == null && remaining <= 0;
                    }
                    else due = resolved != null ? "완료" : "새 보고";
                    var card = Instantiate(rowTemplate, listRoot, false);
                    card.gameObject.SetActive(true);
                    string heading = doc is DividendPolicyDocument
                        ? $"{doc.Title} · {approval.TargetCompany.definition.companyName}" : doc.Title;
                    card.Bind(heading, string.IsNullOrEmpty(doc.Author)
                        ? approval?.TargetCompany?.definition.companyName ?? "대상 없음" : doc.Author,
                        due, overdue, () => select(doc, resolved), resolved != null);
                    cards.Add(card);
                    cardDocuments.Add(doc);
                }
            }
            emptyMessage.SetActive(cards.Count == 0);
            if (selected == null || !cardDocuments.Contains(selected))
                selected = cardDocuments.Count > 0 ? cardDocuments[0] : null;
            if (selected == null) { detail.Clear(); return; }
            select(selected, inbox.History.FirstOrDefault(entry => entry.document == selected));
        }

        private void select(IDocument doc, ResolvedDocument resolved)
        {
            selected = doc;
            for (int i = 0; i < cards.Count; i++) cards[i].SetSelected(cardDocuments[i] == doc);
            detail.Show(doc, resolved, inbox.GetArrival(doc));
        }
    }
}
