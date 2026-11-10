using System.Collections;
using System.Collections.Generic;
using TakeOver.NPC;
using TakeOver.Events;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Story
{
    /// <summary>이벤트 대화 UI 인스턴스를 공용 렌더러로 사용한다. 실제 이벤트에는 안내 UI를 표시하지 않는다.</summary>
    public sealed class StoryView : MonoBehaviour
    {
        private StorySequence sequence;
        private StoryDirector director;
        private RectTransform root;
        private GameObject stage, objectives;
        private Image fade;
        private EventNegotiationView sharedDialogue;
        private GraphicRaycaster raycaster;
        private bool nativeEvent, informationOnly;
        private string informationInstruction = "", informationExplanation = "";
        private Text objective, detail;
        private Button confirm, reopen, skip;
        private CanvasGroup stageGroup;
        private readonly List<Image> masks = new List<Image>();
        private Image outline;
        private bool gated;
        public Text Dialogue => informationOnly ? detail : sharedDialogue.CurrentDialogue;
        public Text Speaker => sharedDialogue.CurrentSpeaker;
        public Image PlayerImage => sharedDialogue.PlayerImage;
        public Image NpcImage => sharedDialogue.NpcImage;
        public EventNegotiationView SharedDialogue => sharedDialogue;
        public bool HasMoreDialoguePages => !informationOnly && sharedDialogue.HasMorePages;
        public Button ContinueButton => informationOnly ? confirm : sharedDialogue.ContinueButton;
        public Button SkipButton => stage.activeSelf && !informationOnly ? sharedDialogue.SkipButton : skip;
        public Text InformationBody => detail;
        public RectTransform InformationRect => (RectTransform)objectives.transform;
        public bool HasEventOverlay => nativeEvent && GetComponentsInChildren<Graphic>().Length > 0;
        public float FadeAlpha => fade.color.a;

        public void Build(StorySequence data, StoryDirector owner)
        {
            sequence = data; director = owner;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
            var full = Image("Letterbox", transform, Color.black);
            Stretch(full.rectTransform);
            root = Rect("Story safe frame", transform, new Rect(0, 0, 1280, 720));
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f,.5f);
            root.anchoredPosition = Vector2.zero;
            var template = FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            if (template == null) throw new System.InvalidOperationException("공용 이벤트 대화 UI가 없습니다.");
            var nativeScaler = template.GetComponentInParent<CanvasScaler>();
            if (nativeScaler != null)
            {
                scaler.uiScaleMode = nativeScaler.uiScaleMode;
                scaler.referenceResolution = nativeScaler.referenceResolution;
                scaler.screenMatchMode = nativeScaler.screenMatchMode;
                scaler.matchWidthOrHeight = nativeScaler.matchWidthOrHeight;
            }
            stage = Instantiate(template.gameObject, transform, false);
            stage.name = "Shared dialogue stage";
            sharedDialogue = stage.GetComponent<EventNegotiationView>();
            stageGroup = stage.AddComponent<CanvasGroup>();
            root.SetAsLastSibling();
            for (int i=0; i<4; i++) masks.Add(Image("Input mask "+i, root, new Color(.015f,.025f,.05f,.58f)));
            outline = Image("Target frame", root, Color.clear); outline.raycastTarget = false;
            for (int i=0;i<4;i++)
            {
                var edge=Image("Border "+i,outline.transform,new Color(.92f,.76f,.4f)); edge.raycastTarget=false;
                var rect=edge.rectTransform;
                rect.anchorMin=i==1?new Vector2(1,0):i==3?new Vector2(0,1):Vector2.zero;
                rect.anchorMax=i==0?new Vector2(0,1):i==1?Vector2.one:i==2?new Vector2(1,0):Vector2.one;
                rect.pivot=new Vector2(.5f,.5f); rect.offsetMin=new Vector2(-1,-1); rect.offsetMax=new Vector2(1,1);
            }
            objectives = template.CreateInformationPanel(root, out objective, out detail);
            confirm = template.CreateActionButton(objectives.transform,"Confirm profile","확인",()=>director.Continue());
            reopen = template.CreateActionButton(objectives.transform,"Reopen profile","인물 정보",()=>director.OpenProfile());
            skip = template.CreateActionButton(root,"Skip to choice","스킵",()=>director.SkipToNextChoice());
            Place(skip.GetComponent<RectTransform>(),new Rect(24,24,164,42));
            EventNegotiationView.ApplySkipButtonStyle(skip);
            fade = Image("Fade curtain", transform, Color.black); Stretch(fade.rectTransform); fade.raycastTarget = true;
            objectives.SetActive(false); SetMasks(false); stage.SetActive(false);
        }

        public void Show(StoryBeat beat, NpcStateRegistry registry)
        {
            informationOnly = beat.goal == StoryGoal.None && beat.speaker == StorySpeaker.System;
            nativeEvent = beat.goal == StoryGoal.OpenEvent || beat.goal == StoryGoal.CompleteEvent;
            raycaster.enabled = !nativeEvent;
            if (nativeEvent)
            {
                HideContent();
                fade.gameObject.SetActive(false);
                return;
            }
            Place(skip.GetComponent<RectTransform>(),new Rect(24,24,164,42));
            gated = beat.goal != StoryGoal.None;
            stage.SetActive(!gated); objectives.SetActive(false); skip.gameObject.SetActive(gated || informationOnly);
            transform.GetChild(0).gameObject.SetActive(!gated);
            SetMasks(gated);
            if (informationOnly)
            {
                sharedDialogue.ShowBackdrop(beat.background,beat.backgroundTint);
                ShowInformation(beat.tutorialTitle ?? "안내",director.Resolve(beat.text),new Rect(280,190,720,320),true,false);
                confirm.name = "Continue";
                confirm.interactable = true;
            }
            else if (!gated) RenderLine(beat, director.Resolve(beat.text), beat.speaker, registry, false, true);
        }

        public void ShowExchange(StoryBeat beat, string line, bool player, NpcStateRegistry registry)
        {
            RenderLine(beat, line, player ? StorySpeaker.Player : StorySpeaker.Npc, registry, !player, false);
            sharedDialogue.SetDialogueInput(true);
        }

        private void RenderLine(StoryBeat beat, string line, StorySpeaker speaking, NpcStateRegistry registry, bool response, bool includeChoices)
        {
            bool narration = speaking == StorySpeaker.Narration || speaking == StorySpeaker.System;
            bool player = speaking == StorySpeaker.Player;
            NpcStateRegistry.NpcSeed profile = null;
            if (!string.IsNullOrEmpty(beat.npcId)) registry.TryGetProfile(beat.npcId, out profile);
            var titles = new List<string>();
            if (includeChoices) foreach (var choice in beat.choices) titles.Add(choice.title);
            sharedDialogue.ShowDialogue(new DialoguePresentation
            {
                background = beat.background, backgroundTint = beat.backgroundTint,
                speaker = narration ? "" : player ? "주인공" : speaking == StorySpeaker.UnregisteredVoice ? beat.voiceLabel :
                    profile != null ? profile.profile.affiliation+" · "+profile.profile.role+"  "+profile.profile.displayName : "",
                text = line, player = player, narration = narration, response = response,
                showPlayer = beat.showPlayer,
                npcStanding = profile?.profile.standingIllustration ?? (profile != null ? NpcInfoPanel.FallbackStanding : null),
                npcDimmed = profile?.profile.dimmedStandingIllustration,
                choices = titles
            }, director.Continue, director.Choose, director.SkipToNextChoice, () => director.CanSkipToChoice);
        }

        public void ShowObjective(string text, bool confirmation, bool profile, bool eventGoal)
        {
            if(nativeEvent) return;
            var beat = director.Current;
            string title = string.IsNullOrEmpty(beat.tutorialTitle) ? "안내" : beat.tutorialTitle;
            informationInstruction = director.Resolve(text);
            informationExplanation = director.Resolve(beat.tutorialText ?? "");
            ShowInformation(title, informationInstruction+"\n\n"+informationExplanation,
                profile ? new Rect(24,100,312,536) : new Rect(24,160,312,520),confirmation,profile);
            confirm.name="Confirm profile";
            confirm.interactable=false;
        }

        private void ShowInformation(string title,string body,Rect area,bool confirmation,bool profile)
        {
            objectives.SetActive(true);
            Place((RectTransform)objectives.transform,area);
            objective.text=title;
            detail.text=body;
            var bodyRect=detail.rectTransform;
            bodyRect.anchorMin=new Vector2(0,1);bodyRect.anchorMax=Vector2.one;bodyRect.pivot=new Vector2(0,1);
            bodyRect.anchoredPosition=new Vector2(18,-66);bodyRect.sizeDelta=new Vector2(-36,area.height-(profile?174:126));
            confirm.gameObject.SetActive(confirmation);
            reopen.gameObject.SetActive(profile);
            Place(confirm.GetComponent<RectTransform>(),new Rect(18,16,area.width-36,38));
            Place(reopen.GetComponent<RectTransform>(),new Rect(18,62,area.width-36,38));
        }
        public void ObjectiveDetail(string text)
        {
            if(!nativeEvent) detail.text=informationInstruction+"\n\n"+informationExplanation+"\n\n"+text;
        }
        public void SetGoalReady(bool ready)=>confirm.interactable=ready;
        public void HideContent()
        {
            stage.SetActive(false); objectives.SetActive(false); SetMasks(false);
            skip.gameObject.SetActive(false);
            transform.GetChild(0).gameObject.SetActive(false);
        }
        public IEnumerator FadeDialogue(float alpha, float seconds)
        {
            float start = stageGroup.alpha;
            for(float elapsed=0;elapsed<seconds;elapsed+=Time.unscaledDeltaTime)
            { stageGroup.alpha=Mathf.Lerp(start,alpha,elapsed/seconds); yield return null; }
            stageGroup.alpha=alpha;
        }
        public void SetInput(bool enabled)
        {
            stageGroup.interactable = enabled;
            sharedDialogue.SetDialogueInput(enabled && stage.activeSelf && !informationOnly);
            if(informationOnly)confirm.interactable=enabled;
            skip.interactable = enabled && director.CanSkipToChoice;
            fade.raycastTarget = !enabled && !nativeEvent;
            raycaster.enabled = !nativeEvent;
        }
        public void SetSkipAvailable(bool available)
        {
            if (stage.activeSelf && !informationOnly) sharedDialogue.SetDialogueInput(available);
            else skip.interactable = available && director.CanSkipToChoice;
        }
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            if (nativeEvent && alpha == 0)
            {
                fade.color = Color.clear; fade.gameObject.SetActive(false);
                yield break;
            }
            fade.gameObject.SetActive(true);
            raycaster.enabled = true;
            fade.raycastTarget = true;
            float start = fade.color.a;
            for (float elapsed=0; elapsed<seconds; elapsed+=Time.unscaledDeltaTime)
            { fade.color = new Color(0,0,0,Mathf.Lerp(start,alpha,Mathf.SmoothStep(0,1,elapsed/seconds))); yield return null; }
            fade.color = new Color(0,0,0,alpha);
        }

        public void SpotlightCompany()
        {
            foreach (var node in FindObjectsByType<CompanyMapNode>(FindObjectsSortMode.None))
                if (node.company?.definition.companyID == StoryDirector.CompanyId) { Spotlight((RectTransform)node.transform); return; }
            Spotlight(null);
        }
        public void SpotlightDayEnd()
        {
            var day = FindFirstObjectByType<DaySkipController>();
            Spotlight(day != null && day.DaySkipButton != null ? day.DaySkipButton.GetComponent<RectTransform>() : null);
        }
        public void Spotlight(RectTransform target)
        {
            if (!gated || nativeEvent) return;
            if (target == null) { Place(masks[0].rectTransform,new Rect(0,0,1280,720)); for(int i=1;i<4;i++) masks[i].gameObject.SetActive(false); outline.gameObject.SetActive(false); return; }
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var targetCanvas = target.GetComponentInParent<Canvas>();
            Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera,corners[0]), null, out var lower);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera,corners[2]), null, out var upper);
            float left=Mathf.Clamp(lower.x+640-6,0,1280), right=Mathf.Clamp(upper.x+640+6,0,1280);
            float bottom=Mathf.Clamp(lower.y+360-6,0,720), top=Mathf.Clamp(upper.y+360+6,0,720);
            Rect[] rects={new Rect(0,0,left,720),new Rect(right,0,1280-right,720),new Rect(left,0,right-left,bottom),new Rect(left,top,right-left,720-top)};
            for(int i=0;i<4;i++) { masks[i].gameObject.SetActive(true); Place(masks[i].rectTransform,rects[i]); }
            outline.gameObject.SetActive(true); Place(outline.rectTransform,new Rect(left,bottom,right-left,top-bottom));
        }

        public void ShowError(string text)
        {
            ShowInformation("안내",text,new Rect(280,190,720,320),false,false);
            fade.color=Color.clear;fade.raycastTarget=false;
        }
        private void SetMasks(bool visible) { foreach(var mask in masks)mask.gameObject.SetActive(visible);outline.gameObject.SetActive(false); }
        private static Image Image(string name, Transform parent, Color color)
        { var obj=new GameObject(name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false); var result=obj.GetComponent<Image>(); result.color=color; return result; }
        private static RectTransform Rect(string name, Transform parent, Rect rect)
        { var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false); var result=(RectTransform)obj.transform; Place(result,rect); return result; }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Place(RectTransform target, Rect rect)
        { target.anchorMin=target.anchorMax=target.pivot=Vector2.zero; target.anchoredPosition=rect.position; target.sizeDelta=rect.size; }
    }
}
