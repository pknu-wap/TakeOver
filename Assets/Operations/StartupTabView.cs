using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TakeOver.Employees;
using TakeOver.NPC;
using TakeOver.Startups;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class StartupTabView : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private TMP_Text emptyTitle;
        [SerializeField] private TMP_Text emptyDescription;

        [Header("목록")]
        [SerializeField] private ScrollRect listScroll;
        [SerializeField] private Scrollbar listScrollbar;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private StartupListItemView itemPrefab;
        [SerializeField] private GameObject scrollThumb;

        [Header("상세")]
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text supportEmployees;
        [SerializeField] private TMP_Text[] investmentValues = new TMP_Text[4];
        [SerializeField] private TMP_Text[] financeValues = new TMP_Text[4];
        [SerializeField] private TMP_Text coreValue;
        [SerializeField] private TMP_Text description;
        [SerializeField] private StartupLineGraph graph;
        [SerializeField] private TMP_Text[] graphTickLabels = new TMP_Text[5];
        [SerializeField] private RectTransform graphLabelRoot;
        [SerializeField] private TMP_Text graphLabelTemplate;
        [SerializeField] private GameObject graphEmptyRoot;

        private Companies companies;
        private StartupSystem startupSystem;
        private JobService jobs;
        private GameTurnClock turnClock;
        private bool built;
        private bool tutorialPreview;
        private Company selected;
        private readonly List<StartupListItemView> items = new List<StartupListItemView>();
        private int shownItemCount = -1;
        private readonly List<TMP_Text> graphLabels = new List<TMP_Text>();

        public bool IsTutorialPreview => tutorialPreview;

        private static readonly Color UpColor = new Color32(201, 33, 33, 255);
        private static readonly Color DownColor = new Color32(58, 127, 231, 255);
        private static readonly Color ValueColor = new Color32(96, 96, 96, 255);

        public void SetTutorialPreview(bool enabled)
        {
            tutorialPreview = enabled;
            refresh();
        }

        private void Start()
        {
            companies = FindFirstObjectByType<Companies>();
            startupSystem = FindFirstObjectByType<StartupSystem>();
            jobs = FindFirstObjectByType<JobService>();
            turnClock = FindFirstObjectByType<GameTurnClock>();
            if (turnClock != null) turnClock.TurnAdvanced += handleTurnAdvanced;
            if (listScrollbar != null && listScroll != null)
            {
                listScrollbar.onValueChanged.AddListener(value => listScroll.verticalNormalizedPosition = value);
                listScroll.onValueChanged.AddListener(value => listScrollbar.SetValueWithoutNotify(value.y));
                listScrollbar.SetValueWithoutNotify(1f);
            }
            built = true;
            refresh();
        }

        private void OnEnable()
        {
            if (built) refresh();
        }

        private void OnDestroy()
        {
            if (turnClock != null) turnClock.TurnAdvanced -= handleTurnAdvanced;
        }

        private void handleTurnAdvanced(int newTurn)
        {
            if (isActiveAndEnabled) StartCoroutine(refreshNextFrame());
        }

        private IEnumerator refreshNextFrame()
        {
            yield return null;
            refresh();
        }

        public void refreshAfterInvestment() => refresh();

        private void refresh()
        {
            if (!built) return;

            IReadOnlyList<Company> startups = companies != null
                ? companies.StartupList.Where(company => company.state.getBaseStat(CompanyStat.PLAYER_SHARES) > 0f).ToList()
                : null;
            bool empty = startups == null || startups.Count == 0;
            bool showPreview = empty && tutorialPreview;

            contentRoot.SetActive(!empty || showPreview);
            emptyRoot.SetActive(empty && !showPreview);
            if (empty && !showPreview)
            {
                emptyTitle.text = "계약한 스타트업이 없습니다";
                emptyDescription.text = "직원 탭에서 직원에게 스타트업 발굴 직무를 지정하세요";
                selected = null;
                setItems(0);
                scrollThumb.SetActive(false);
                return;
            }

            if (showPreview)
            {
                setItems(1);
                items[0].Bind(null, "새봄테크", "정보기술", null, true, selectPreview);
                detailTitle.text = "새봄테크 <color=#BABABA>(정보기술)</color>";
                supportEmployees.text = "파견 직원 : 없음";
                investmentValues[0].text = "20%";
                investmentValues[1].text = "200,000만원";
                investmentValues[2].text = "150,000만원";
                investmentValues[3].text = "+33.3%";
                colorSigned(investmentValues[3], 1f);
                financeValues[0].text = "250,000만원";
                financeValues[1].text = "120,000만원";
                financeValues[2].text = "+15,000만원";
                financeValues[3].text = "+4.0%";
                colorSigned(financeValues[2], 1f);
                colorSigned(financeValues[3], 1f);
                coreValue.text = "핵심 가치 : 정보의 빠른 연결";
                description.text = "여러 산업의 데이터를 모아 빠르게 필요한 정보를 제공하는 기술 스타트업입니다.";
                setGraph(new[] { 60000f, 72000f, 68000f, 81000f, 90000f });
                return;
            }

            if (selected == null || !contains(startups, selected)) selected = startups[0];
            setItems(startups.Count);
            for (int i = 0; i < startups.Count; i++)
            {
                Company company = startups[i];
                items[i].Bind(company, company.definition.companyName, company.definition.industry,
                    company.definition.logo, company == selected, select);
            }
            refreshDetail(selected);
        }

        private void select(Company company)
        {
            selected = company;
            refresh();
        }

        private void selectPreview(Company company)
        {
            selected = null;
        }

        private void setItems(int count)
        {
            bool countChanged = shownItemCount != count;
            while (items.Count < count)
            {
                StartupListItemView item = Instantiate(itemPrefab, listContent);
                items.Add(item);
            }

            float rowStep = itemPrefab.GetComponent<RectTransform>().rect.height > 0f
                ? itemPrefab.GetComponent<RectTransform>().rect.height
                : 58f;
            float step = rowStep * (96f / 71.73f);
            float gap = step - rowStep;
            float topOffset = rowStep * (30f / 71.73f);
            for (int i = 0; i < items.Count; i++)
            {
                StartupListItemView item = items[i];
                item.gameObject.SetActive(i < count);
                if (i >= count) continue;
                RectTransform rect = item.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(8f, -(topOffset + i * step));
            }

            if (listContent != null)
            {
                float viewportHeight = listScroll != null && listScroll.viewport != null ? listScroll.viewport.rect.height : 0f;
                listContent.sizeDelta = new Vector2(listContent.sizeDelta.x, Mathf.Max(viewportHeight, topOffset + count * rowStep + Mathf.Max(0, count - 1) * gap));
            }
            if (scrollThumb != null) scrollThumb.SetActive(count > 0);
            if (countChanged && listScroll != null && listScrollbar != null)
            {
                listScroll.verticalNormalizedPosition = 1f;
                listScrollbar.SetValueWithoutNotify(1f);
            }
            shownItemCount = count;
        }

        private void refreshDetail(Company startup)
        {
            detailTitle.text = $"{startup.definition.companyName} <color=#BABABA>({startup.definition.industry})</color>";
            string names = jobs != null ? jobs.SupportEmployeeNamesFor(startup) : "";
            supportEmployees.text = $"파견 직원 : {(string.IsNullOrEmpty(names) ? "없음" : names)}";

            float stake = CompanyUIUtil.getDerived(startup, CompanyDerivedStat.STAKE);
            float equity = CompanyUIUtil.getDerived(startup, CompanyDerivedStat.FUNDAMENTAL_VALUE) * stake / 100f;
            float investment = 0f;
            if (startupSystem != null && startupSystem.tryGetRecord(startup.definition.companyID, out StartupRecord record))
                investment = record.cumulativeInvestment;

            investmentValues[0].text = $"{stake:0.#}%";
            investmentValues[1].text = $"{equity:N0}만원";
            investmentValues[2].text = $"{investment:N0}만원";
            if (investment > 0f)
            {
                float rate = (equity - investment) / investment;
                investmentValues[3].text = signedPercent(rate);
                colorSigned(investmentValues[3], rate);
            }
            else
            {
                investmentValues[3].text = "-";
                investmentValues[3].color = ValueColor;
            }

            float cash = CompanyUIUtil.getStat(startup, CompanyStat.CASH);
            float revenue = CompanyUIUtil.getStat(startup, CompanyStat.REVENUE);
            float profit = CompanyUIUtil.getDerived(startup, CompanyDerivedStat.NET_PROFIT);
            float growth = CompanyUIUtil.getDerived(startup, CompanyDerivedStat.REVENUE_GROWTH);
            financeValues[0].text = $"{cash:N0}만원";
            financeValues[1].text = $"{revenue:N0}만원";
            financeValues[2].text = signedAmount(profit) + "만원";
            financeValues[3].text = signedPercent(growth);
            colorSigned(financeValues[2], profit);
            colorSigned(financeValues[3], growth);

            coreValue.text = string.IsNullOrEmpty(startup.definition.coreValue)
                ? "" : $"핵심 가치 : {startup.definition.coreValue}";
            description.text = startup.definition.description ?? "";

            IReadOnlyList<float> history = startupSystem != null ? startupSystem.getRevenueHistory(startup) : null;
            setGraph(history);
        }

        private void setGraph(IReadOnlyList<float> values)
        {
            graph.setValues(values);
            bool hasGraph = values != null && values.Count >= 2;
            graphEmptyRoot.SetActive(!hasGraph);
            graphLabelTemplate.gameObject.SetActive(false);
            while (graphLabels.Count < (hasGraph ? values.Count : 0))
            {
                TMP_Text label = Instantiate(graphLabelTemplate, graphLabelRoot);
                label.gameObject.SetActive(true);
                graphLabels.Add(label);
            }

            for (int i = 0; i < graphLabels.Count; i++)
            {
                bool visible = hasGraph && i < values.Count;
                graphLabels[i].gameObject.SetActive(visible);
                if (!visible) continue;
                TMP_Text label = graphLabels[i];
                RectTransform rect = label.rectTransform;
                float x = (float)i / Mathf.Max(1, values.Count - 1);
                rect.anchorMin = new Vector2(x, 1f);
                rect.anchorMax = new Vector2(x, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                label.text = i == values.Count - 1 ? "이번 주" : $"{values.Count - 1 - i}주 전";
            }

            IReadOnlyList<float> ticks = graph.TickValues;
            for (int i = 0; i < graphTickLabels.Length; i++)
                graphTickLabels[i].text = hasGraph && i < ticks.Count ? $"{ticks[ticks.Count - 1 - i]:N0}" : "";
        }

        private static string signedPercent(float value)
        {
            string formatted = (Mathf.Abs(value) * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            return value > 0f ? "+" + formatted : value < 0f ? "-" + formatted : "0.0%";
        }

        private static string signedAmount(float value)
        {
            return value > 0f ? "+" + value.ToString("N0") : value < 0f ? value.ToString("N0") : "0";
        }

        private static void colorSigned(TMP_Text text, float value)
        {
            text.color = value > 0f ? UpColor : value < 0f ? DownColor : ValueColor;
        }

        private static bool contains(IReadOnlyList<Company> list, Company target)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == target) return true;
            return false;
        }
    }
}