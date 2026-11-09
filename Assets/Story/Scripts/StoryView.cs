using System.Collections;
using System.Collections.Generic;
using TakeOver.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Story
{
    /// <summary>기존 이벤트와 같은 좌측 NPC·우측 주인공 구도. 독백은 화자 없이 중앙 정렬한다.</summary>
    public sealed class StoryView : MonoBehaviour
    {
        private StorySequence sequence;
        private StoryDirector director;
        private RectTransform root;
        private GameObject stage, objectives;
        private Image background, playerImage, npcImage, fade;
        private Text speaker, dialogue, chapter, objective, detail;
        private Button next, confirm, reopen, skip;
        private CanvasGroup stageGroup;
        private readonly List<Button> choices = new List<Button>();
        private readonly List<Image> masks = new List<Image>();
        private Image outline;
        private bool gated;
        public Text Dialogue => dialogue;
        public Text Speaker => speaker;
        public Image PlayerImage => playerImage;
        public Image NpcImage => npcImage;
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
            gameObject.AddComponent<GraphicRaycaster>();
            var full = Image("Letterbox", transform, Color.black);
            Stretch(full.rectTransform);
            root = Rect("Story safe frame", transform, new Rect(0, 0, 1280, 720));
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f,.5f);
            root.anchoredPosition = Vector2.zero;
            stage = Rect("Dialogue stage", root, new Rect(0, 0, 1280, 720)).gameObject;
            stageGroup = stage.AddComponent<CanvasGroup>();
            background = Image("Background", stage.transform, Color.white); Stretch(background.rectTransform);
            var wash = Image("Cinematic wash", stage.transform, new Color(.02f,.035f,.07f,.16f)); Stretch(wash.rectTransform);
            playerImage = Image("Player standing", stage.transform, Color.white);
            Place(playerImage.rectTransform, new Rect(812, 102, 420, 580)); playerImage.preserveAspect = true;
            npcImage = Image("NPC standing", stage.transform, Color.white);
            Place(npcImage.rectTransform, new Rect(48, 102, 420, 580)); npcImage.preserveAspect = true;
            var top = Image("Chapter strip", stage.transform, new Color(.025f,.04f,.07f,.88f));
            Place(top.rectTransform, new Rect(0, 656, 1280, 64));
            chapter = Label("Chapter", top.transform, new Rect(48, 8, 1184, 48), 20, new Color(.88f,.76f,.5f));
            var panel = Image("Dialogue panel", stage.transform, new Color(.035f,.055f,.095f,.97f));
            Place(panel.rectTransform, new Rect(40, 28, 1200, 206));
            var advance=panel.gameObject.AddComponent<Button>();
            advance.transition=Selectable.Transition.None;
            advance.onClick.AddListener(()=>director.Continue());
            var rule = Image("Gold rule", panel.transform, new Color(.72f,.59f,.34f));
            Place(rule.rectTransform, new Rect(0,203,1200,3));
            speaker = Label("Speaker", panel.transform, new Rect(32, 148, 1136, 36), 21, new Color(.93f,.8f,.53f));
            dialogue = Label("Dialogue", panel.transform, new Rect(32, 44, 1136, 108), 23, new Color(.94f,.95f,.98f));
            dialogue.lineSpacing = 1.3f;
            next = MakeButton("Continue", panel.transform, new Rect(1008, 8, 164, 32), "계속  ▸", () => director.Continue(), 17);
            for (int i=0; i<3; i++)
            {
                int choice = i;
                choices.Add(MakeButton("Choice"+i, stage.transform, new Rect(616, 248+(2-i)*63, 608, 54), "", () => director.Choose(choice), 20));
            }
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
            objectives = Rect("Tutorial objective", root, new Rect(24, 552, 600, 144)).gameObject;
            var objectiveBg = Image("Objective backing", objectives.transform, new Color(.025f,.045f,.085f,.97f)); Stretch(objectiveBg.rectTransform);
            objective = Label("Objective", objectives.transform, new Rect(20, 91, 560, 43), 20, new Color(.95f,.82f,.54f));
            detail = Label("Hint", objectives.transform, new Rect(20, 12, 560, 74), 17, Color.white);
            confirm = MakeButton("Confirm profile", objectives.transform, new Rect(348,8,232,40), "확인했습니다  ▸", () => director.Continue(), 18);
            reopen = MakeButton("Reopen profile", objectives.transform, new Rect(20,8,210,40), "인물 정보 다시 열기", () => director.OpenProfile(), 17);
            skip = MakeButton("Skip to choice", root, new Rect(56,248,164,42), "스킵  ▸▸", () => director.SkipToNextChoice(), 17);
            fade = Image("Fade curtain", transform, Color.black); Stretch(fade.rectTransform); fade.raycastTarget = true;
            objectives.SetActive(false); SetMasks(false); stage.SetActive(false);
        }

        public void Show(StoryBeat beat, NpcStateRegistry registry)
        {
            skip.gameObject.SetActive(true);
            bool profileGoal = beat.goal == StoryGoal.ViewRepresentative || beat.goal == StoryGoal.ViewMemory;
            Place(skip.GetComponent<RectTransform>(), profileGoal ? new Rect(24,248,100,42) : new Rect(56,248,164,42));
            gated = beat.goal != StoryGoal.None;
            stage.SetActive(!gated); objectives.SetActive(false);
            // Letterbox도 실제 조작 단계에는 클릭을 가로채지 않는다.
            transform.GetChild(0).gameObject.SetActive(!gated);
            SetMasks(gated);
            if (gated) return;
            background.sprite = beat.background; background.color = beat.backgroundTint;
            chapter.text = beat.sceneTitle;
            RenderLine(beat, director.Resolve(beat.text), beat.speaker, registry);
            for (int i=0; i<choices.Count; i++)
            {
                choices[i].gameObject.SetActive(i < beat.choices.Count);
                if (i < beat.choices.Count) choices[i].GetComponentInChildren<Text>().text = beat.choices[i].title;
            }
            next.gameObject.SetActive(beat.choices.Count == 0);
        }

        public void ShowExchange(StoryBeat beat, string line, bool player, NpcStateRegistry registry)
        {
            foreach (var button in choices) button.gameObject.SetActive(false);
            next.gameObject.SetActive(true);
            RenderLine(beat, line, player ? StorySpeaker.Player : StorySpeaker.Npc, registry);
        }

        private void RenderLine(StoryBeat beat, string line, StorySpeaker speaking, NpcStateRegistry registry)
        {
            bool narration = speaking == StorySpeaker.Narration;
            bool player = speaking == StorySpeaker.Player;
            NpcStateRegistry.NpcSeed profile = null;
            if (!string.IsNullOrEmpty(beat.npcId)) registry.TryGetProfile(beat.npcId, out profile);
            speaker.text = narration ? "" : player ? "주인공" : speaking == StorySpeaker.UnregisteredVoice ? beat.voiceLabel :
                profile != null ? profile.profile.affiliation+" · "+profile.profile.role+"  "+profile.profile.displayName : "";
            speaker.gameObject.SetActive(!narration);
            speaker.alignment = player ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            dialogue.alignment = narration ? TextAnchor.MiddleCenter : player ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            dialogue.text = line;
            Place(dialogue.rectTransform, narration ? new Rect(56, 46, 1088, 130) : new Rect(32, 44, 1136, 108));
            playerImage.sprite = !player && sequence.playerDimmed != null ? sequence.playerDimmed : sequence.playerStanding;
            playerImage.color = player ? Color.white : new Color(.68f,.68f,.7f);
            playerImage.gameObject.SetActive(!narration && beat.showPlayer && playerImage.sprite != null);
            npcImage.sprite = player && profile?.profile.dimmedStandingIllustration != null ?
                profile.profile.dimmedStandingIllustration : profile?.profile.standingIllustration;
            if (profile != null && npcImage.sprite == null) npcImage.sprite = NpcInfoPanel.FallbackStanding;
            npcImage.color = player ? new Color(.6f,.6f,.64f) : Color.white;
            npcImage.gameObject.SetActive(!narration && npcImage.sprite != null);
        }

        public void ShowObjective(string text, bool profile)
        {
            objectives.SetActive(true);
            // 프로필 상단/탭을 가리지 않도록 하단 안내띠로 배치한다.
            Place((RectTransform)objectives.transform, profile ? new Rect(24, 0, 1232, 124) : new Rect(24,552,600,144));
            Place(objective.rectTransform, profile ? new Rect(20,80,1192,38) : new Rect(20,91,560,43));
            objective.text = text;
            detail.gameObject.SetActive(true);
            Place(detail.rectTransform,profile ? new Rect(20,40,1192,40) : new Rect(20,12,560,74));
            detail.text = profile ? "프로필과 관계를 살펴본 뒤 확인 버튼을 누르세요." : "금색으로 강조된 화면을 조작하세요.";
            confirm.gameObject.SetActive(profile); reopen.gameObject.SetActive(profile);
            if (profile) { Place(confirm.GetComponent<RectTransform>(),new Rect(980,4,232,36)); confirm.interactable = false; }
        }
        public void ObjectiveDetail(string text) => detail.text = text;
        public void SetGoalReady(bool ready) => confirm.interactable = ready;
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
            foreach (var button in choices) button.interactable = enabled;
            next.interactable = enabled;
            skip.interactable = enabled && director.CanSkipToChoice;
            fade.raycastTarget = !enabled;
        }
        public void SetSkipAvailable(bool available) => skip.interactable = available && director.CanSkipToChoice;
        public IEnumerator FadeTo(float alpha, float seconds)
        {
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
            if (!gated) return;
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

        public void ShowError(string text) { stage.SetActive(true); dialogue.text=text; speaker.gameObject.SetActive(false); next.gameObject.SetActive(false); fade.color=Color.clear; }
        private void SetMasks(bool visible) { foreach(var mask in masks) mask.gameObject.SetActive(visible); outline.gameObject.SetActive(false); }
        private Text Label(string name, Transform parent, Rect rect, int size, Color color)
        {
            var obj=Rect(name,parent,rect); var text=obj.gameObject.AddComponent<Text>();
            text.font=sequence.font; text.fontSize=size; text.color=color; text.raycastTarget=false;
            text.supportRichText=false; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            text.alignment=TextAnchor.MiddleLeft; return text;
        }
        private Button MakeButton(string name, Transform parent, Rect rect, string title, UnityEngine.Events.UnityAction clicked, int size)
        {
            var image=Image(name,parent,new Color(.10f,.15f,.23f,.98f)); Place(image.rectTransform,rect);
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(1,.9f,.65f); colors.pressedColor=new Color(.7f,.7f,.75f); button.colors=colors;
            var label=Label("Label",button.transform,new Rect(12,0,rect.width-24,rect.height),size,Color.white); label.alignment=TextAnchor.MiddleCenter;
            label.text=title;
            button.onClick.AddListener(clicked); return button;
        }
        private static Image Image(string name, Transform parent, Color color)
        { var obj=new GameObject(name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false); var result=obj.GetComponent<Image>(); result.color=color; return result; }
        private static RectTransform Rect(string name, Transform parent, Rect rect)
        { var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false); var result=(RectTransform)obj.transform; Place(result,rect); return result; }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Place(RectTransform target, Rect rect)
        { target.anchorMin=target.anchorMax=target.pivot=Vector2.zero; target.anchoredPosition=rect.position; target.sizeDelta=rect.size; }
    }
}
