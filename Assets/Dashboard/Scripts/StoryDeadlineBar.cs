using System.Linq;
using TakeOver.Events;
using TakeOver.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Dashboard
{
    /// <summary>상단바의 실제 배경과 글꼴을 복제한 필수 스토리 목표 표시.</summary>
    public sealed class StoryDeadlineBar : MonoBehaviour
    {
        private EventSystemController controller;
        private TopBarNavigationController navigation;
        private RectTransform divider;
        private bool showingMission;
        private int displayedDays = int.MinValue;
        private string displayedRequirement;
        public RectTransform Bar { get; private set; }
        public TMP_Text Requirement { get; private set; }
        public TMP_Text Countdown { get; private set; }
        private void Start()
        {
            controller = FindFirstObjectByType<EventSystemController>();
            navigation = GetComponent<TopBarNavigationController>();
            var source = transform.Find("TopBar_Background").GetComponent<Image>();
            var fontSource = GetComponentsInChildren<TMP_Text>(true).First(text => text.font != null && text.text == "홈");
            var background = Instantiate(source, transform, false);
            background.name = "Mandatory story deadline"; background.raycastTarget = false;
            Bar = background.rectTransform;
            Bar.anchorMin = Bar.anchorMax = new Vector2(0, 1); Bar.pivot = new Vector2(0, 1);
            Bar.anchoredPosition = new Vector2(60, -264); Bar.sizeDelta = new Vector2(880, 64);
            Requirement = CreateLabel(fontSource, "Story entry requirement", new Vector2(0, 0), new Vector2(7f / 11f, 1));
            Requirement.enableAutoSizing = true; Requirement.fontSizeMin = 16; Requirement.fontSizeMax = 22;
            Requirement.alignment = TextAlignmentOptions.MidlineLeft;
            Countdown = CreateLabel(fontSource, "Story days remaining", new Vector2(7f / 11f, 0), Vector2.one);
            Countdown.alignment = TextAlignmentOptions.Center;
            var rule = new GameObject("Story deadline divider", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(Bar, false);
            var rect = (RectTransform)rule.transform;
            divider = rect;
            rect.anchorMin = new Vector2(7f / 11f, .2f); rect.anchorMax = new Vector2(7f / 11f, .8f);
            rect.sizeDelta = new Vector2(1.5f, 0); rect.anchoredPosition = Vector2.zero;
            rule.GetComponent<Image>().color = fontSource.color;
            rule.GetComponent<Image>().raycastTarget = false;
            Refresh();
        }
        private TMP_Text CreateLabel(TMP_Text source, string label, Vector2 min, Vector2 max)
        {
            var text = Instantiate(source, Bar, false); text.name = label;
            var rect = text.rectTransform;
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(22, 0); rect.offsetMax = new Vector2(-22, 0);
            text.enableAutoSizing = false; text.fontSize = 22; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
        private void LateUpdate() { Refresh(); }
        private void Refresh()
        {
            if (Bar == null || controller == null) return;
            DashboardTab tab = navigation.CurrentTab;
            bool visible = tab != DashboardTab.Npcs && tab != DashboardTab.Log
                && !StoryDirector.IsRunning && controller.MandatoryState != MandatoryStoryState.Failed;
            if (Bar.gameObject.activeSelf != visible) Bar.gameObject.SetActive(visible);
            if (!visible) return;
            bool hasMission = controller.HasActiveMission;
            string requirement = hasMission ? controller.MissionRequirement : "지금은 투자를 고려할 회사가 없다";
            if (displayedRequirement == null || showingMission != hasMission)
            {
                showingMission = hasMission;
                Requirement.alignment = hasMission ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                Requirement.rectTransform.anchorMax = hasMission ? new Vector2(7f / 11f, 1) : Vector2.one;
                Countdown.gameObject.SetActive(hasMission);
                divider.gameObject.SetActive(hasMission);
            }
            if (displayedRequirement != requirement)
            {
                displayedRequirement = requirement;
                Bar.sizeDelta = new Vector2(880, hasMission && requirement.Contains("\n") ? 80 : 64);
                Requirement.text = requirement;
            }
            int days = hasMission ? controller.MandatoryDaysRemaining : -1;
            if (days == displayedDays) return;
            displayedDays = days;
            Countdown.text = hasMission ? "앞으로 " + days + "일" : string.Empty;
        }
    }
}
