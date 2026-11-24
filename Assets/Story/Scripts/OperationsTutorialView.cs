using System.Collections;
using TakeOver.Documents;
using TakeOver.Employees;
using TakeOver.Operations;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Story
{
    // A separate copy of the authored operations UI. Never moves or restyles the live screen.
    public sealed class OperationsTutorialView : MonoBehaviour
    {
        public OperationsScreen Screen { get; private set; }
        public RectTransform Content { get; private set; }
        public bool Ready { get; private set; }
        private GameObject backdrop;

        public void Initialize(StoryGoal goal, IDocument document)
        {
            var prefab = Resources.Load<GameObject>("Story/OperationsTutorial");
            var copy = Instantiate(prefab, transform, false);
            copy.name = "Tutorial operations copy";
            Screen = copy.GetComponent<OperationsScreen>();
            Screen.ShowTutorialPage(goal == StoryGoal.ViewEmployees, goal == StoryGoal.ViewStartups);
            Content = (RectTransform)copy.transform;
            var designSize = Content.rect.size;
            Content.anchorMin = Content.anchorMax = Content.pivot = new Vector2(.5f, .5f);
            Content.sizeDelta = designSize;
            Content.anchoredPosition = Vector2.zero;
            Content.localRotation = Quaternion.identity;
            var area = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(area.x / designSize.x, area.y / designSize.y);
            Content.localScale = Vector3.one * scale;
            // Copy the original office image behind the whole lesson, so unrelated live panels cannot peek out.
            var office = copy.transform.Find("Office background");
            backdrop = new GameObject("Tutorial office backdrop", typeof(RectTransform), typeof(RectMask2D));
            backdrop.transform.SetParent(transform.parent, false);
            backdrop.transform.SetAsFirstSibling();
            var clip = (RectTransform)backdrop.transform;
            clip.anchorMin = Vector2.zero; clip.anchorMax = Vector2.one;
            clip.offsetMin = Vector2.zero; clip.offsetMax = new Vector2(0, -80);
            var background = (RectTransform)Instantiate(office.gameObject, clip, false).transform;
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero; background.offsetMax = new Vector2(0, 80);
            background.GetComponent<Image>().raycastTarget = false;
            copy.SetActive(true);
            Screen.GetComponentInChildren<EmployeeTabView>(true)?.SetTutorialPreview(goal == StoryGoal.ViewEmployees);
            Screen.GetComponentInChildren<StartupTabView>(true)?.SetTutorialPreview(goal == StoryGoal.ViewStartups);
            if (document != null) Screen.GetComponentInChildren<DocumentTabView>(true).SetTutorialPreview(document);
            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            StartCoroutine(FinishLayout(group));
        }

        private void OnDestroy() { if (backdrop != null) Destroy(backdrop); }

        private IEnumerator FinishLayout(CanvasGroup group)
        {
            // Let Start populate the real row/detail components, then expose the completed copy.
            yield return null; yield return null;
            foreach (var control in GetComponentsInChildren<Selectable>())
            {
                var colors = control.colors;
                colors.disabledColor = colors.normalColor;
                control.colors = colors;
                control.interactable = false;
                control.targetGraphic?.CrossFadeColor(colors.normalColor, 0, true, true);
            }
            Canvas.ForceUpdateCanvases();
            group.alpha = 1;
            Ready = true;
        }
    }
}
