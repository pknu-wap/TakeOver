using System.Collections;
using System.Collections.Generic;
using TakeOver.NPC;
using TakeOver.Startups;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class StartupTabView : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, .81f, .3f);
        private static readonly Color Muted = new Color(.65f, .75f, .88f);
        private static readonly Color Ink = new Color(.93f, .95f, .98f);
        private static readonly Color PanelColor = new Color(.04f, .11f, .22f);
        private static readonly Color CardColor = new Color(.07f, .17f, .33f);
        private static readonly Color LineColor = new Color(.16f, .29f, .5f);

        private const float ListWidth = 260f;
        private const float Gap = 16f;
        private const float Pad = 16f;
        private const float ItemHeight = 64f;
        private const float MetricTop = 58f;
        private const float MetricHeight = 64f;
        private const float MetricGap = 8f;

        private static readonly string[] MetricLabels = { "기업가치", "보유 자금", "매출", "직원 수" };

        [Tooltip("글꼴을 빌려 오고 빈 상태 문구를 적을 글자. 비워 두면 이 오브젝트 아래의 첫 Text('준비 중')를 쓴다.")]
        [FormerlySerializedAs("listText")]
        [SerializeField] private Text templateText;
        [SerializeField] private string emptyMessage = "스타트업이 없습니다";

        private Companies companies;
        private StartupSystem startupSystem;
        private GameTurnClock turnClock;
        private bool built;
        private Company selected;

        private RectTransform contentRoot;
        private RectTransform listContent;
        private readonly List<ListItem> items = new List<ListItem>();

        private Text titleText;
        private Text stakeText;
        private readonly Text[] metricValues = new Text[MetricLabels.Length];
        private StartupLineGraph graph;
        private Text graphStartLabel;
        private Text graphEmptyLabel;

        private sealed class ListItem
        {
            public GameObject root;
            public Text nameText;
            public Text industryText;
            public Text stakeText;
            public Outline outline;
            public Company company;
        }

        private void Start()
        {
            if (templateText == null) templateText = GetComponentInChildren<Text>(true);
            if (templateText == null)
            {
                Debug.LogError("StartupTabView: 글꼴을 빌려 올 Text를 찾을 수 없습니다.", this);
                return;
            }

            companies = FindFirstObjectByType<Companies>();
            startupSystem = FindFirstObjectByType<StartupSystem>();
            turnClock = FindFirstObjectByType<GameTurnClock>();
            if (turnClock != null) turnClock.TurnAdvanced += handleTurnAdvanced;

            build();
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

            IReadOnlyList<Company> startups = companies != null ? companies.StartupList : null;
            bool empty = startups == null || startups.Count == 0;
            contentRoot.gameObject.SetActive(!empty);
            templateText.gameObject.SetActive(empty);
            if (empty)
            {
                templateText.text = emptyMessage;
                selected = null;
                return;
            }

            if (selected == null || !contains(startups, selected)) selected = startups[0];

            refreshList(startups);
            refreshDetail(selected);
        }

        private void select(Company company)
        {
            selected = company;
            refresh();
        }

        private void refreshList(IReadOnlyList<Company> startups)
        {
            while (items.Count < startups.Count) items.Add(createItem());

            for (int i = 0; i < items.Count; i++)
            {
                ListItem item = items[i];
                bool visible = i < startups.Count;
                item.root.SetActive(visible);
                if (!visible) continue;

                Company startup = startups[i];
                item.company = startup;
                item.nameText.text = startup.definition.companyName;
                item.industryText.text = startup.definition.industry;
                item.stakeText.text = $"{CompanyUIUtil.getDerived(startup, CompanyDerivedStat.STAKE):0.#}%";
                item.outline.enabled = startup == selected;
            }
        }

        private void refreshDetail(Company startup)
        {
            titleText.text = $"{startup.definition.companyName} · {startup.definition.industry}";
            stakeText.text = $"플레이어 지분 {CompanyUIUtil.getDerived(startup, CompanyDerivedStat.STAKE):0.#}%";

            metricValues[0].text = $"{CompanyUIUtil.getDerived(startup, CompanyDerivedStat.FUNDAMENTAL_VALUE):N0}";
            metricValues[1].text = $"{CompanyUIUtil.getStat(startup, CompanyStat.CASH):N0}";
            metricValues[2].text = $"{CompanyUIUtil.getStat(startup, CompanyStat.REVENUE):N0}";
            metricValues[3].text = $"{CompanyUIUtil.getStat(startup, CompanyStat.EMPLOYEE_COUNT):N0}명";

            IReadOnlyList<float> history = startupSystem != null ? startupSystem.getRevenueHistory(startup) : null;
            graph.setValues(history);
            bool hasGraph = graph.pointCount >= 2;
            graphEmptyLabel.gameObject.SetActive(!hasGraph);
            graphStartLabel.text = hasGraph ? $"{graph.pointCount - 1}주 전" : "";
        }

        private static bool contains(IReadOnlyList<Company> list, Company target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return true;
            }
            return false;
        }

        private void build()
        {
            contentRoot = makeRect("Startup content", transform);
            stretch(contentRoot, 0f, 0f, 0f, 0f);

            Image listPanel = makeImage("Startup list", contentRoot, PanelColor);
            RectTransform listRect = listPanel.rectTransform;
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(0f, 1f);
            listRect.pivot = new Vector2(0f, 0.5f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = new Vector2(ListWidth, 0f);
            buildList(listRect);

            Image detailPanel = makeImage("Startup detail", contentRoot, PanelColor);
            stretch(detailPanel.rectTransform, ListWidth + Gap, 0f, 0f, 0f);
            buildDetail(detailPanel.rectTransform);
        }

        private void buildList(RectTransform panel)
        {
            RectTransform viewport = makeRect("Viewport", panel);
            stretch(viewport, 8f, 8f, 8f, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            listContent = makeRect("Content", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.offsetMin = Vector2.zero;
            listContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport;
            scroll.content = listContent;
        }

        private ListItem createItem()
        {
            Image background = makeImage("Startup item", listContent, CardColor);
            LayoutElement size = background.gameObject.AddComponent<LayoutElement>();
            size.minHeight = ItemHeight;
            size.preferredHeight = ItemHeight;

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = false;

            ListItem item = new ListItem { root = background.gameObject, outline = outline };

            item.nameText = makeText("Name", background.transform, 18, Ink, TextAnchor.UpperLeft, true);
            topRow(item.nameText.rectTransform, 10f, 24f, 12f, 12f);

            item.industryText = makeText("Industry", background.transform, 14, Muted, TextAnchor.LowerLeft);
            stretch(item.industryText.rectTransform, 12f, 10f, 12f, 34f);

            item.stakeText = makeText("Stake", background.transform, 14, Muted, TextAnchor.LowerRight);
            stretch(item.stakeText.rectTransform, 12f, 10f, 12f, 34f);

            button.onClick.AddListener(() => select(item.company));
            return item;
        }

        private void buildDetail(RectTransform panel)
        {
            titleText = makeText("Title", panel, 20, Ink, TextAnchor.MiddleLeft, true);
            topRow(titleText.rectTransform, Pad, 30f, Pad, Pad);
            stakeText = makeText("Stake", panel, 15, Muted, TextAnchor.MiddleRight);
            topRow(stakeText.rectTransform, Pad, 30f, Pad, Pad);

            for (int i = 0; i < MetricLabels.Length; i++)
            {
                int line = i / 2;
                int column = i % 2;
                float top = MetricTop + line * (MetricHeight + MetricGap);

                Image box = makeImage(MetricLabels[i], panel, CardColor);
                RectTransform rect = box.rectTransform;
                rect.anchorMin = new Vector2(column * 0.5f, 1f);
                rect.anchorMax = new Vector2((column + 1) * 0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(column == 0 ? Pad : 4f, -(top + MetricHeight));
                rect.offsetMax = new Vector2(column == 0 ? -4f : -Pad, -top);

                Text label = makeText("Label", box.transform, 14, Muted, TextAnchor.UpperLeft);
                topRow(label.rectTransform, 8f, 20f, 12f, 12f);
                label.text = MetricLabels[i];

                metricValues[i] = makeText("Value", box.transform, 22, Ink, TextAnchor.UpperLeft, true);
                topRow(metricValues[i].rectTransform, 30f, 28f, 12f, 12f);
            }

            int metricLines = (MetricLabels.Length + 1) / 2;
            float chartTop = MetricTop + metricLines * (MetricHeight + MetricGap) + 8f;
            RectTransform chart = makeRect("Revenue chart", panel);
            stretch(chart, Pad, Pad, Pad, chartTop);

            Text chartTitle = makeText("Title", chart, 16, Muted, TextAnchor.UpperLeft);
            topRow(chartTitle.rectTransform, 0f, 22f, 0f, 0f);
            chartTitle.text = "매출";

            Image baseline = makeImage("Baseline", chart, LineColor);
            baseline.rectTransform.anchorMin = new Vector2(0f, 0f);
            baseline.rectTransform.anchorMax = new Vector2(1f, 0f);
            baseline.rectTransform.offsetMin = new Vector2(0f, 28f);
            baseline.rectTransform.offsetMax = new Vector2(0f, 29f);

            RectTransform graphRect = makeRect("Line", chart);
            stretch(graphRect, 0f, 28f, 0f, 30f);
            graph = graphRect.gameObject.AddComponent<StartupLineGraph>();
            graph.color = Gold;
            graph.raycastTarget = false;

            graphStartLabel = makeText("Start", chart, 14, Muted, TextAnchor.LowerLeft);
            stretch(graphStartLabel.rectTransform, 0f, 0f, 0f, 0f);
            Text endLabel = makeText("End", chart, 14, Muted, TextAnchor.LowerRight);
            stretch(endLabel.rectTransform, 0f, 0f, 0f, 0f);
            endLabel.text = "이번 주";

            graphEmptyLabel = makeText("Empty", chart, 15, Muted, TextAnchor.MiddleCenter);
            stretch(graphEmptyLabel.rectTransform, 0f, 0f, 0f, 0f);
            graphEmptyLabel.text = "다음 실적 갱신 뒤부터 매출 그래프가 그려집니다";
        }

        private static RectTransform makeRect(string objectName, Transform parent)
        {
            GameObject created = new GameObject(objectName, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private static Image makeImage(string objectName, Transform parent, Color color)
        {
            Image image = makeRect(objectName, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text makeText(string objectName, Transform parent, int size, Color color,
            TextAnchor alignment, bool bold = false)
        {
            Text text = makeRect(objectName, parent).gameObject.AddComponent<Text>();
            text.font = templateText.font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void topRow(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
