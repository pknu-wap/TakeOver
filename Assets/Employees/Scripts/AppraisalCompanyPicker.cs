using System;
using System.Collections.Generic;
using TakeOver.Events;
using UnityEngine;

namespace TakeOver.Employees
{
    public sealed class AppraisalCompanyPicker : MonoBehaviour
    {
        [SerializeField] private CompanyRow rowPrefab;
        private readonly List<Company> selected = new List<Company>();
        private readonly Dictionary<Company, UnityEngine.UI.Image> backgrounds = new Dictionary<Company, UnityEngine.UI.Image>();
        private GameObject panel;
        private GameObject backdrop;
        private UnityEngine.UI.Text heading;
        private UnityEngine.UI.Text explanation;
        private UnityEngine.UI.Button confirm;
        private Employee employee;
        private Action assigned;

        public void open(Employee employee, Action assigned)
        {
            this.employee = employee;
            this.assigned = assigned;
            selected.Clear();
            backgrounds.Clear();
            if (backdrop != null) Destroy(backdrop);

            var source = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            Canvas canvas = source.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform backdropRect = createRect("Appraisal selection overlay", canvas.transform);
            stretch(backdropRect);
            backdrop = backdropRect.gameObject;
            backdrop.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, .45f);
            var overlay = backdrop.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 500;
            backdrop.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            panel = source.CreateInformationPanel(backdrop.transform, out heading, out explanation);
            panel.name = "Appraisal company selection";
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(920f, 600f);
            var question = (RectTransform)panel.transform.Find("Content/QuestionBlock");
            question.anchorMin = Vector2.zero;
            question.anchorMax = Vector2.one;
            question.offsetMin = question.offsetMax = Vector2.zero;
            question.Find("Underline").gameObject.SetActive(false);
            heading.fontSize = 26;
            explanation.fontSize = 20;
            explanation.rectTransform.sizeDelta = new Vector2(explanation.rectTransform.sizeDelta.x, 100f);
            explanation.text = "회사를 선택하고 확인하면 같은 회사들을 반복 조사합니다.\n분석력이 높을수록 살피는 지표가 늘어납니다. 보고서는 주가 상승을 보장하지 않습니다.\n로고 · 회사 · 업종 · 보유 지분 · 주가 · 부채비율";

            RectTransform scroll = createRect("Company list", panel.transform);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(28f, 96f);
            scroll.offsetMax = new Vector2(-28f, -154f);
            var scrollView = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollView.horizontal = false;
            scrollView.scrollSensitivity = 30f;
            RectTransform viewport = createRect("Viewport", scroll);
            stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewportImage.color = new Color(0f, 0f, 0f, .05f);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            RectTransform content = createRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scrollView.viewport = viewport;
            scrollView.content = content;

            Companies companies = FindFirstObjectByType<Companies>();
            foreach (Company company in companies.CompanyList)
            {
                CompanyRow row = Instantiate(rowPrefab, content, false);
                row.gameObject.SetActive(true);
                row.setup(company, toggle);
                var size = row.GetComponent<UnityEngine.UI.LayoutElement>();
                if (size == null) size = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                size.minHeight = size.preferredHeight = 68f;
                var background = row.GetComponent<UnityEngine.UI.Image>();
                if (background == null) background = row.gameObject.AddComponent<UnityEngine.UI.Image>();
                backgrounds.Add(company, background);
            }
            confirm = source.CreateActionButton(panel.transform, "Confirm", "확인", commit);
            placeButton((RectTransform)confirm.transform, 150f);
            var cancel = source.CreateActionButton(panel.transform, "Cancel", "취소", close);
            placeButton((RectTransform)cancel.transform, -150f);
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
            heading.text = $"조사할 회사 선택 · {selected.Count}/{MarketAppraisal.getCompanyLimit(employee.Legwork.Current)}";
            foreach (var pair in backgrounds)
                pair.Value.color = selected.Contains(pair.Key) ? new Color(.55f, .43f, .12f, .85f) : new Color(.04f, .1f, .2f, .9f);
            confirm.interactable = selected.Count > 0;
        }

        private void commit()
        {
            var jobs = FindFirstObjectByType<JobService>();
            if (!jobs.Assign(employee, JobType.Appraisal, selected)) return;
            assigned();
            close();
        }

        private void close()
        {
            backdrop.SetActive(false);
            Destroy(backdrop);
            backdrop = null;
            panel = null;
        }

        private void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop);
        }

        private static RectTransform createRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void placeButton(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, 44f);
            rect.sizeDelta = new Vector2(220f, 52f);
        }
    }
}
