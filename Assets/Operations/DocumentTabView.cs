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
        private readonly List<IApprovalDocument> cardDocuments = new List<IApprovalDocument>();
        private DocumentInbox inbox;
        private GameTurnClock clock;
        private IApprovalDocument selected;
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
        private void decide(IApprovalDocument doc, bool approved) => inbox.Resolve(doc, approved);

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
                IEnumerable<IApprovalDocument> documents = filter == 1 ? inbox.Open
                    : filter == 2 ? inbox.History.Select(entry => entry.document)
                    : inbox.Open.Concat(inbox.History.Select(entry => entry.document)).OrderBy(inbox.GetArrival);
                foreach (IApprovalDocument doc in documents)
                {
                    var resolved = inbox.History.FirstOrDefault(entry => entry.document == doc);
                    int remaining = doc.DeadlineTurn - clock.CurrentTurn;
                    string due = resolved != null ? "완료" : remaining < 0 ? "기한 지남"
                        : remaining == 0 ? "오늘 마감" : $"D-{remaining}";
                    var card = Instantiate(rowTemplate, listRoot, false);
                    card.gameObject.SetActive(true);
                    string heading = doc is DividendPolicyDocument
                        ? $"{doc.Title} · {doc.TargetCompany.definition.companyName}" : doc.Title;
                    card.Bind(heading, string.IsNullOrEmpty(doc.Author)
                        ? doc.TargetCompany?.definition.companyName ?? "대상 없음" : doc.Author,
                        due, resolved == null && remaining <= 0, () => select(doc, resolved), resolved != null);
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

        private void select(IApprovalDocument doc, ResolvedDocument resolved)
        {
            selected = doc;
            for (int i = 0; i < cards.Count; i++) cards[i].SetSelected(cardDocuments[i] == doc);
            detail.Show(doc, resolved);
        }
    }
}
