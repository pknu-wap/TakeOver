using System.Collections.Generic;
using TakeOver.CompanyRules;
using TakeOver.Documents;
using TakeOver.Employees;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.Operations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TakeOver.Startups
{
    public sealed class StartupContractService : MonoBehaviour, ITurnBlocker
    {
        [SerializeField] private DocumentDetailView contractPrefab;
        private readonly Queue<(Company company, int day)> requests = new Queue<(Company, int)>();
        private Companies companies;
        private Player player;
        private Marketdata marketData;
        private GameTurnClock clock;
        private EmployeeService employees;
        private JobService jobs;
        private EventSystemController events;
        private EventNegotiationView negotiationTemplate;
        private EventNegotiationView negotiation;
        private DocumentDetailView detail;
        private StartupContractDocument contract;
        private GameObject previousSelection;

        private void Start()
        {
            companies = FindFirstObjectByType<Companies>();
            player = FindFirstObjectByType<Player>();
            marketData = FindFirstObjectByType<Marketdata>();
            clock = FindFirstObjectByType<GameTurnClock>();
            employees = FindFirstObjectByType<EmployeeService>();
            jobs = FindFirstObjectByType<JobService>();
            events = FindFirstObjectByType<EventSystemController>();
            negotiationTemplate = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            clock.RegisterBlocker(this);
        }

        public bool IsBlocking(out string reason)
        {
            reason = "진행 중인 투자 계약을 먼저 처리해 주세요";
            return contract != null;
        }

        // 발굴 보고서의 투자 검토 선택에서 호출할 연결 함수
        public bool requestContract(Company startup)
        {
            bool registered = false;
            foreach (Company company in companies.StartupList)
                if (company == startup) { registered = true; break; }
            if (!registered || startup.state.getStat(CompanyStat.PLAYER_SHARES) > 0f) return false;
            var system = FindFirstObjectByType<StartupSystem>();
            if (!system.tryGetRecord(startup.definition.companyID, out var record) || record.pendingContractDay > 0) return false;
            foreach (var request in requests)
                if (request.company == startup) return false;
            if (contract != null && contract.TargetCompany == startup) return false;
            requests.Enqueue((startup, clock.CurrentTurn + 1));
            record.pendingContractDay = clock.CurrentTurn + 1;
            return true;
        }

        private void Update()
        {
            if (contract == null && requests.Count == 0 && companies != null)
                foreach (var candidate in companies.StartupList)
                    if (FindFirstObjectByType<StartupSystem>().tryGetRecord(candidate.definition.companyID, out var record) && record.pendingContractDay > 0)
                        requests.Enqueue((candidate, record.pendingContractDay));
            if (contract != null || requests.Count == 0 || clock.CurrentTurn < requests.Peek().day) return;
            if (!events.IsClosed || TakeOver.Story.StoryDirector.IsRunning) return;
            Company company = requests.Dequeue().company;
            if (company == null || !containsStartup(company) || company.state.getStat(CompanyStat.PLAYER_SHARES) > 0f) return;

            contract = new StartupContractDocument(company, player, companies, clock.CurrentTurn, employees, jobs);
            previousSelection = EventSystem.current.currentSelectedGameObject;
            negotiation = Instantiate(negotiationTemplate, negotiationTemplate.transform.parent, false);
            negotiation.name = "Startup contract negotiation";
            negotiation.transform.SetAsLastSibling();
            var canvas = negotiation.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 600;
            negotiation.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            negotiation.ShowDialogue(new DialoguePresentation
            {
                speaker = contract.Author,
                text = "투자 조건을 확인해 주세요. 지분율을 정하시면 계약을 진행하겠습니다.",
                showPlayer = false
            }, showContract, null, showContract, () => true);
            negotiation.SetDialogueInput(true);
            negotiation.ContinueButton.Select();
        }

        private bool containsStartup(Company company)
        {
            foreach (Company startup in companies.StartupList)
                if (startup == company) return true;
            return false;
        }

        private void showContract()
        {
            negotiation.SetDialogueInput(false);
            negotiation.ContinueButton.gameObject.SetActive(false);
            negotiation.SkipButton.gameObject.SetActive(false);
            if (detail != null) return;
            detail = Instantiate(contractPrefab, negotiation.transform.parent, false);
            detail.name = "Startup investment contract";
            var operations = FindFirstObjectByType<OperationsScreen>(FindObjectsInactive.Include);
            bool operationsActive = operations.gameObject.activeSelf;
            operations.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var source = operations.GetComponentInChildren<DocumentDetailView>(true);
            var sourceRect = (RectTransform)source.transform;
            var rect = (RectTransform)detail.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sourceRect.rect.size;
            Vector3 sourceScale = sourceRect.lossyScale;
            Vector3 targetScale = rect.parent.lossyScale;
            rect.localScale = new Vector3(sourceScale.x / targetScale.x, sourceScale.y / targetScale.y, 1f);
            operations.gameObject.SetActive(operationsActive);
            detail.gameObject.SetActive(true);
            detail.Decided += decide;
            var overlay = detail.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 601;
            detail.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            detail.Show(contract, arrivalTurn: clock.CurrentTurn);
            negotiation.transform.Find("DialogueBand").gameObject.SetActive(false);
            var backdrop = negotiation.GetComponent<Canvas>();
            backdrop.overrideSorting = true;
            backdrop.sortingOrder = 600;
            detail.GetComponentInChildren<UnityEngine.UI.Button>().Select();
        }

        private void decide(IDocument document, bool approved)
        {
            if (approved)
            {
                if (!contract.CanApprove(out _)) return;
                Employee staff = contract.SelectedStaff;
                if (staff != null && (jobs == null || !jobs.AssignSupport(staff, contract.TargetCompany)))
                {
                    Debug.LogWarning($"[StartupContract] {staff.Name} 배치에 실패해 계약을 확정하지 않았습니다.", this);
                    return;
                }
                var result = contract.OnApprove();
                new ChangeApplier(player).Apply(result, contract.TargetCompany);
                var startupSystem = FindFirstObjectByType<StartupSystem>();
                if (startupSystem != null)
                {
                    startupSystem.markContracted(contract.TargetCompany, clock.CurrentTurn);
                    if (startupSystem.tryGetRecord(contract.TargetCompany.definition.companyID, out var investmentRecord))
                        investmentRecord.cumulativeInvestment -= result.playerCash;
                }
                contract.TargetCompany.calculator.recalculate(contract.TargetCompany, marketData);
                var startupTab = FindFirstObjectByType<StartupTabView>();
                if (startupTab != null) startupTab.refreshAfterInvestment();
            }
            if (FindFirstObjectByType<StartupSystem>().tryGetRecord(contract.TargetCompany.definition.companyID, out var record)) record.pendingContractDay = 0;
            close();
            TakeOver.SaveSystem.GameSessionCoordinator.Active?.SaveSettledState();
        }

        private void close()
        {
            if (detail != null)
            {
                detail.gameObject.SetActive(false);
                Destroy(detail.gameObject);
            }
            if (negotiation != null)
            {
                negotiation.gameObject.SetActive(false);
                Destroy(negotiation.gameObject);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            }
            detail = null;
            negotiation = null;
            contract = null;
            previousSelection = null;
        }

        private void OnDestroy()
        {
            if (clock != null) clock.UnregisterBlocker(this);
            close();
        }

        [ContextMenu("검증용: 첫 미투자 스타트업 계약 요청")]
        private void requestFirstStartup()
        {
            foreach (Company startup in companies.StartupList)
                if (requestContract(startup)) break;
        }

        [ContextMenu("검증용: 첫 미투자 스타트업 계약 요청", true)]
        private bool canRequestFirstStartup() => Application.isPlaying;
    }
}
