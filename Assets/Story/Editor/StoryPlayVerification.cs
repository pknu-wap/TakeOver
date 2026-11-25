using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.MainMenu;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class StoryPlayVerification
    {
        private const string Key = "TakeOver.Story.Verify";
        public static bool DashboardOnly => SessionState.GetBool(Key + ".DashboardOnly", false);
        public static bool FocusedSpeakerRoles => SessionState.GetBool(Key + ".FocusedSpeakerRoles", false);
        public static void RunFocusedSpeakerRoles()
        {
            SessionState.SetBool(Key + ".FocusedSpeakerRoles", true);
            Run();
        }
        public static void RunFocusedEventsOnly()
        {
            SessionState.SetBool(Key + ".FocusedSpeakerRoles", true);
            SessionState.SetBool(Key + ".DashboardOnly", true);
            Run();
        }
        public static bool SkipRegression => SessionState.GetBool("TakeOver.StorySkip.Normal", false);
        public static string Output => Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("TAKEOVER_QA_OUTPUT") ?? "../QA_NarrativeRevision", SkipRegression ? "Normal" : "."));
        public static void RunSkipRegression()
        {
            SessionState.SetBool("TakeOver.StorySkip.Normal", true);
            Run();
        }
        public static void RunDashboard()
        {
            SessionState.SetBool(Key + ".DashboardOnly", true);
            Run();
        }
        static StoryPlayVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("TakeOver/Story/Verify New Game in Play Mode")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"play-results.txt"),"RUNNING\n");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(DashboardOnly ? "Assets/Scenes/Game.unity" : "Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var game = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            game.position=new Rect(40,40,1280,780); game.Show(); game.Focus();
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                var obj=new GameObject("Story integration verification");
                Object.DontDestroyOnLoad(obj); obj.AddComponent<StoryPlayProbe>();
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                bool failed=File.ReadAllText(Path.Combine(Output,"play-results.txt")).Contains("FAIL");
                SessionState.SetBool("TakeOver.StorySkip.Normal",false);
                SessionState.SetBool(Key + ".DashboardOnly", false);
                SessionState.SetBool(Key + ".FocusedSpeakerRoles", false);
                if(Application.isBatchMode || Environment.GetEnvironmentVariable("TAKEOVER_QA_EXIT") == "1") EditorApplication.Exit(failed?1:0);
            }
        }
    }

    public sealed class StoryPlayProbe : MonoBehaviour
    {
        private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        private string Results => Path.Combine(StoryPlayVerification.Output,"play-results.txt");
        private readonly HashSet<string> captures=new HashSet<string>();
        private IEnumerator test;
        private bool ending;
        private float deadline;
        private FixedGameViewSize remoteResolution;
        private void Start()
        {
            if (Environment.GetEnvironmentVariable("TAKEOVER_REMOTE_INTEGRATION_QA") == "1")
                remoteResolution = new FixedGameViewSize(1280, 720);
            Application.logMessageReceived+=OnLog;
            deadline=Time.realtimeSinceStartup+600;
            test=Run();
        }
        private void Update()
        {
            if(ending) return;
            if(Time.realtimeSinceStartup>deadline) { Fail(new Exception("Story verification timed out")); return; }
            try
            {
                if(!test.MoveNext()) { ending=true; EditorApplication.isPlaying=false; }
            }
            catch(Exception error) { Fail(error); }
        }
        // Editor update drives the outer test; yielded IEnumerator operations are awaited by a coroutine bridge.
        private bool waiting;
        private IEnumerator Wait(IEnumerator routine) { waiting=true; yield return routine; waiting=false; }
        private IEnumerator Await(IEnumerator routine)
        {
            StartCoroutine(Wait(routine)); while(waiting) yield return null;
        }
        private void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
                File.AppendAllText(Results,"FAIL runtime: "+message+"\n"+stack+"\n");
        }
        private void OnDestroy() { remoteResolution?.Dispose(); Application.logMessageReceived-=OnLog; }
        private void Fail(Exception error)
        {
            File.AppendAllText(Results,"FAIL: "+error+"\n"); ending=true; EditorApplication.isPlaying=false;
        }
        private void Check(bool value,string text)
        {
            if(!value) throw new InvalidOperationException(text);
            File.AppendAllText(Results,"PASS: "+text+"\n");
        }
        private IEnumerator Capture(string name)
        {
            if ((Application.isBatchMode && Environment.GetEnvironmentVariable("TAKEOVER_QA_CAPTURE") != "1") || !captures.Add(name)) yield break;
            if (Application.isBatchMode) yield return null;
            else yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(StoryPlayVerification.Output,name+".png"));
            yield return null;
        }
        private static Button Named(Transform root,string name) => root.GetComponentsInChildren<Button>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);
        internal static void Click(Button button)
        {
            if(!button.gameObject.activeInHierarchy || !button.interactable) throw new Exception("Button not available: "+button.name);
            var canvas=button.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var rect=button.GetComponent<RectTransform>();
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0 || !(hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)))
                throw new Exception("Button occluded: "+button.name+" by "+(hits.Count>0?hits[0].gameObject.transform.parent?.name+"/"+hits[0].gameObject.name:"nothing"));
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private static Button Persistent(Transform root,string method)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
                for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
                    if(button.onClick.GetPersistentMethodName(i)==method) return button;
            throw new Exception("No button for "+method);
        }

        private IEnumerator VerifyDialogueLayout()
        {
            var overlay = new GameObject("Dialogue layout verification", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var viewport = new GameObject("Dialogue QA viewport", typeof(RectTransform));
            var frame = (RectTransform)viewport.transform;
            frame.SetParent(overlay.transform, false);
            var template = AssetDatabase.LoadAssetAtPath<EventNegotiationView>("Assets/EventSystem/Prefabs/EventNegotiation.prefab");
            var dialogue = Instantiate(template, frame, false);
            try
            {
                foreach (var size in new[] { new Vector2(1280, 720), new Vector2(800, 900), new Vector2(1600, 560) })
                {
                    frame.sizeDelta = size;
                    int advances = 0;
                    dialogue.ShowDialogue(new DialoguePresentation { narration = true, text = "대화 진행 확인" },
                        () => advances++, null, null, null);
                    dialogue.SetDialogueInput(true);
                    yield return null;
                    CheckButtonInside(frame, dialogue.ContinueButton, size);
                    Click(dialogue.ContinueButton);
                    Check(advances == 1, "Continue advances once in dialogue viewport " + size);

                    int choice = -1;
                    dialogue.ShowDialogue(new DialoguePresentation { text = "선택 확인", choices = new[] { "첫 번째", "두 번째" } },
                        null, index => choice = index, null, null);
                    Check(!dialogue.ContinueButton.gameObject.activeSelf, "Choices take over after the final page " + size);
                    dialogue.SetDialogueInput(true);
                    yield return null;
                    var answer = Named(dialogue.transform, "Choice1");
                    CheckButtonInside(frame, answer, size);
                    Click(answer);
                    Check(choice == 1, "Choice reaches its callback in dialogue viewport " + size);
                }
            }
            finally
            {
                overlay.SetActive(false);
                Destroy(overlay);
            }
        }

        private void CheckButtonInside(RectTransform frame, Button button, Vector2 size)
        {
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            var bounds = frame.rect;
            bounds.min -= Vector2.one;
            bounds.max += Vector2.one;
            var localCorners = corners.Select(frame.InverseTransformPoint).ToArray();
            Check(button.IsActive() && localCorners.All(corner => bounds.Contains(corner)),
                $"{button.name} is fully visible in dialogue viewport {size} ({localCorners[0]} to {localCorners[2]} in {bounds})");
        }

        private void CheckTopBar(DashboardTab tab)
        {
            var navigation = Object.FindFirstObjectByType<TopBarNavigationController>();
            var kinds = (DashboardTab[])typeof(TopBarNavigationController).GetField("tabKinds", Fields).GetValue(navigation);
            var buttons = (Button[])typeof(TopBarNavigationController).GetField("tabButtons", Fields).GetValue(navigation);
            var indicator = Object.FindFirstObjectByType<TopBarIndicatorController>();
            var mark = (RectTransform)typeof(TopBarIndicatorController).GetField("markCurrentTap", Fields).GetValue(indicator);
            var button = (RectTransform)buttons[Array.IndexOf(kinds, tab)].transform;
            var center = mark.TransformPoint(mark.rect.center);
            float centerError = Mathf.Abs(button.InverseTransformPoint(center).x - button.rect.center.x);
            Check(mark.gameObject.activeInHierarchy && centerError < 6f, "Top bar highlight follows " + tab + " (" + centerError + ")");
        }

        private IEnumerator VerifyOperations()
        {
            var nav = Object.FindFirstObjectByType<TopBarNavigationController>();
            var tabButtons = (Button[])typeof(TopBarNavigationController).GetField("tabButtons", Fields).GetValue(nav);
            var tabKinds = (DashboardTab[])typeof(TopBarNavigationController).GetField("tabKinds", Fields).GetValue(nav);
            Click(tabButtons[Array.IndexOf(tabKinds, DashboardTab.Operations)]);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var operations = Object.FindFirstObjectByType<TakeOver.Operations.OperationsScreen>();
            var panel = (Image)typeof(TakeOver.Operations.OperationsScreen).GetField("panel", Fields).GetValue(operations);
            var panelCorners = new Vector3[4];
            panel.rectTransform.GetWorldCorners(panelCorners);
            Check(panelCorners.All(corner => new Rect(-1f, -1f, Screen.width + 2f, Screen.height + 2f)
                .Contains(RectTransformUtility.WorldToScreenPoint(null, corner))), "Operations panel stays within the viewport");
            var subTabs = (Button[])typeof(TakeOver.Operations.OperationsScreen).GetField("subTabButtons", Fields).GetValue(operations);
            for (int i = 0; i < subTabs.Length; i++)
            {
                var tab = subTabs[(i + 1) % subTabs.Length];
                Click(tab);
                float ready = Time.unscaledTime + .2f;
                while (Time.unscaledTime < ready) yield return null;
                var labels = operations.GetComponentsInChildren<Text>();
                Check(labels.Any(label => label.text.Any(character => character >= '가' && character <= '힣')),
                    "Operations panel renders Korean labels: " + tab.name);
                foreach (var label in labels)
                    Check(label.text.Where(character => character >= '가' && character <= '힣').All(character => label.font != null && label.font.HasCharacter(character)),
                        "Operations font covers the visible Korean text: " + label.name);
                var screenshot = Await(Capture("operations-" + tab.name)); while (screenshot.MoveNext()) yield return null;
            }
            CheckTopBar(DashboardTab.Operations);
            nav.ReturnHome();
            yield return null;
            CheckTopBar(DashboardTab.Home);
        }

        private IEnumerator Run()
        {
            // Drive nested waits explicitly: this iterator is ticked once per editor Play frame.
            if (remoteResolution != null) for (int i = 0; i < 8; i++) yield return null;
            yield return null; yield return null;
            if (StoryPlayVerification.DashboardOnly)
            {
                if (StoryPlayVerification.FocusedSpeakerRoles)
                {
                    var focused = FocusedEvents(); while (focused.MoveNext()) yield return null;
                    File.AppendAllText(Results, "COMPLETE\n");
                    yield break;
                }
                CheckTopBar(DashboardTab.Home);
                var operations = VerifyOperations(); while (operations.MoveNext()) yield return null;
                File.AppendAllText(Results, "COMPLETE\n");
                yield break;
            }
            Check(!StoryDirector.IsRunning,"Main menu has no active intro");
            if(StoryPlayVerification.SkipRegression)
            {
                Check(!Object.FindFirstObjectByType<MainMenuController>().SkipIntroToggle.isOn,"Title checkbox defaults off");
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += StorySkipState.Seed;
            }
            Object.FindFirstObjectByType<MainMenuController>().startNewGame();
            var namePrompt = SessionSaveVerification.EnterProtagonistName("한서윤");
            while (namePrompt.MoveNext()) yield return null;
            Check(true, "Playing the tutorial requires a name against the launch blackout");
            int watchdog=0;
            while(StoryDirector.Active==null || StoryDirector.Active.Current==null || StoryDirector.Active.Busy) { if(watchdog++>10000) throw new Exception("Intro did not start"); yield return null; }
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            var companies=Object.FindFirstObjectByType<Companies>();
            var company=companies.getCompany(StoryDirector.CompanyId);
            var player=Object.FindFirstObjectByType<Player>();
            var clock=Object.FindFirstObjectByType<GameTurnClock>();
            var trading=Object.FindFirstObjectByType<ShareTradingSystem>();
            var detail=Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
            var events=Object.FindFirstObjectByType<EventSystemController>();
            var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            var layout = VerifyDialogueLayout();
            while (layout.MoveNext()) yield return null;
            Check(registry.Profiles.Count==30 && registry.States.Count==30,"All 30 NPC profiles initialized");
            Check(companies.CompanyList.Count==9,"All 9 companies indexed");
            foreach(var id in StoryContentBuilder.RivalIds)
            {
                Check(companies.getCompany(id)!=null,"Rival company lookup: "+id);
                Check(registry.TryFindByPosition(id,NpcPosition.CEO,out var ceo),"Unique rival CEO: "+id);
                Check(!string.IsNullOrEmpty(ceo.profile.personalitySummary),"CEO settings loaded: "+id);
            }
            int startTurn=clock.CurrentTurn;
            int choices=0; int visited=0;
            int last=-1;
            while(StoryDirector.IsRunning)
            {
                var director=StoryDirector.Active;
                if(director.Busy || director.Current==null) { yield return null; continue; }
                var beat=director.Current;
                float until=Time.unscaledTime+.22f; while(Time.unscaledTime<until) yield return null;
                if(last!=director.BeatIndex)
                {
                    last=director.BeatIndex; visited++;
                    Check(director.View.FadeAlpha==0,"Transition completed: "+beat.id);
                    if(beat.goal!=StoryGoal.EndDay && beat.id!=sequence.beats.Last().id && clock.CurrentTurn==startTurn)
                    { clock.AdvanceTurn(); Check(clock.CurrentTurn==startTurn,"Cutscene blocks unintended turn: "+beat.id); }
                    if(beat.speaker==StorySpeaker.Narration && beat.goal==StoryGoal.None)
                        Check(!director.View.Speaker.gameObject.activeSelf && director.View.Dialogue.alignment==TextAnchor.MiddleCenter &&
                            !director.View.PlayerImage.gameObject.activeSelf && !director.View.NpcImage.gameObject.activeSelf,"Narration centered without name or portraits: "+beat.id);
                    if(beat.goal==StoryGoal.None && beat.speaker==StorySpeaker.Narration)
                    {
                        Check(!director.View.SharedDialogue.NarrationUnderlineVisible,"Narration/system notice hides speaker underline: "+beat.id);
                        var body=director.View.Dialogue.rectTransform;
                        var ui=director.View.SharedDialogue.GetComponent<RectTransform>();
                        Check(Mathf.Abs(body.TransformPoint(body.rect.center).x-ui.TransformPoint(ui.rect.center).x)<1,"Narration/system body is centered across full shared band: "+beat.id);
                    }
                    if(beat.goal==StoryGoal.None && beat.speaker==StorySpeaker.Npc)
                    {
                        Check(director.View.Speaker.text.Contains(registry.Profiles.First(p=>p.npcId==beat.npcId).profile.displayName),"NPC label matches profile: "+beat.id);
                        var speakingProfile = registry.Profiles.First(p => p.npcId == beat.npcId).profile;
                        var speakingSeed = registry.Profiles.First(p => p.npcId == beat.npcId);
                        Check(director.View.Speaker.text == speakingSeed.GetSpeakerLabel(), "Tutorial uses shared company/role/name label: " + beat.id);
                        Check(director.View.Speaker.text.Contains(speakingProfile.DialogueRole), "Tutorial speaker uses dialogue role: " + beat.id);
                        if (!string.IsNullOrWhiteSpace(speakingProfile.speakerRole))
                        {
                            Check(!director.View.Speaker.text.Contains(speakingProfile.role), "Tutorial speaker excludes long profile role: " + beat.id);
                            var roleCapture = Await(Capture("tutorial-speaker-" + beat.npcId));
                            while (roleCapture.MoveNext()) yield return null;
                        }
                        else if (speakingSeed.companyId == "external-network")
                        {
                            var neutralCapture = Await(Capture("tutorial-speaker-" + beat.npcId));
                            while (neutralCapture.MoveNext()) yield return null;
                        }
                    }
                    if (beat.goal == StoryGoal.None && beat.speaker == StorySpeaker.Player)
                        Check(director.View.Speaker.text == player.ProtagonistName, "Player label uses the entered name: " + beat.id);
                    if (beat.id == "intro-033" || beat.id == "neutral-intro-T1-6" || beat.id == "neutral-intro-T4-tail-1")
                        Check(beat.speaker == StorySpeaker.Player && director.View.NpcImage.gameObject.activeSelf,
                            "NPC standing remains visible for the player's reply: " + beat.id);
                    if(StoryPlayVerification.SkipRegression)
                    {
                        if (beat.goal != StoryGoal.CompleteEvent && beat.goal != StoryGoal.OpenEvent)
                            Check(Named(director.transform,"Skip to choice").gameObject.activeInHierarchy,"Skip control stays visible: "+beat.id);
                        bool childhood=beat.sceneTitle.EndsWith("마지막 납품")||beat.sceneTitle.EndsWith("제안서")||beat.sceneTitle.EndsWith("빈자리");
                        if(childhood)Check(!director.View.PlayerImage.gameObject.activeSelf,"Childhood victim scene hides adult standing: "+beat.id);
                        else if(beat.speaker==StorySpeaker.Player && beat.goal==StoryGoal.None)Check(director.View.PlayerImage.gameObject.activeSelf,"Adult player standing retained: "+beat.id);
                    }
                }
                if (beat.goal==StoryGoal.None && beat.speaker!=StorySpeaker.System)
                {
                    Check(director.View.SharedDialogue!=null,"Story uses shared EventNegotiationView component: "+beat.id);
                    Check(director.View.SharedDialogue.CurrentDialogue.preferredHeight <= director.View.SharedDialogue.CurrentDialogue.rectTransform.rect.height + 1,"Current authored dialogue page fits its shared text area: "+beat.id);
                    Check(director.View.Dialogue.preferredHeight <= director.View.Dialogue.rectTransform.rect.height + .5f,"Shared dialogue page fits original text area: "+beat.id);
                    if (beat.choices.Count>0 && director.View.HasMoreDialoguePages)
                    { Click(director.View.ContinueButton); yield return null; continue; }
                }
                if(beat.speaker==StorySpeaker.System && beat.goal==StoryGoal.None)
                {
                    Check(director.View.InformationBody.gameObject.activeInHierarchy,"System explanation uses event-style information panel: "+beat.id);
                    Check(director.View.InformationBody.preferredHeight<=director.View.InformationBody.rectTransform.rect.height+.5f,"Information content fits panel: "+beat.id);
                    Check(director.View.ContinueButton.GetComponentInChildren<Text>().text=="확인","Information action uses concise confirm label");
                }
                if(beat.goal!=StoryGoal.None && beat.goal!=StoryGoal.OpenEvent && beat.goal!=StoryGoal.CompleteEvent)
                {
                    Check(director.View.InformationBody.preferredHeight<=director.View.InformationBody.rectTransform.rect.height+.5f,"Contextual explanation fits beside target: "+beat.id);
                    var bar = Object.FindFirstObjectByType<TopBarNavigationController>().transform.Find("TopBar_Background") as RectTransform;
                    var barCorners = new Vector3[4]; var infoCorners = new Vector3[4];
                    bar.GetWorldCorners(barCorners); director.View.InformationRect.GetWorldCorners(infoCorners);
                    Check(infoCorners[2].y <= barCorners[0].y - 1 && infoCorners[0].y >= 0,
                        "Tutorial explanation stays below the top bar and above the screen bottom: " + beat.id);
                }
                if(beat.id=="lesson-026" || beat.id=="lesson-041" || beat.id=="intro-026" || beat.id=="intro-070")
                { var cap=Await(Capture("polish-"+beat.id)); while(cap.MoveNext())yield return null; }
                if(beat.id==sequence.beats[0].id) { var cap=Await(Capture("01-narration")); while(cap.MoveNext()) yield return null; }
                if(beat.background!=null && beat.background.name=="HotelLounge" && beat.speaker==StorySpeaker.Npc)
                { var cap=Await(Capture("02-hotel-npc")); while(cap.MoveNext()) yield return null; }
                if(beat.goal==StoryGoal.SelectCompany)
                {
                    detail.show(companies.CompanyList.First(c=>c.definition.companyID!=StoryDirector.CompanyId));
                    Check(!detail.gameObject.activeInHierarchy,"Other company cannot bypass tutorial selection");
                    var node=Object.FindObjectsByType<CompanyMapNode>(FindObjectsSortMode.None).First(n=>n.company==company);
                    var cap=Await(Capture("03-map-target")); while(cap.MoveNext()) yield return null;
                    Click(node.GetComponentInChildren<Button>());
                }
                else if(beat.goal==StoryGoal.BuyShares || beat.goal==StoryGoal.BuyMore || beat.goal==StoryGoal.SellShares)
                {
                    Check(!detail.IsTutorialPreview && detail.gameObject.activeInHierarchy &&
                        detail.GetComponentsInChildren<Button>(true).Where(b => b.name == "BuyButton" || b.name == "SellButton").All(b => b.interactable),
                        "Trading lessons keep the original interactive company panel: " + beat.id);
                    Canvas.ForceUpdateCanvases();
                    var panelCorners=new Vector3[4]; var windowCorners=new Vector3[4];
                    director.View.InformationRect.GetWorldCorners(panelCorners);
                    ((RectTransform)detail.transform.Find("Window")).GetWorldCorners(windowCorners);
                    Check(windowCorners[0].x>=panelCorners[2].x+8,"Company window stays beside tutorial explanation: "+beat.id);
                    Check(windowCorners[2].x<=Screen.width,"Company window remains on screen: "+beat.id);
                    var input=(TMPro.TMP_InputField)typeof(CompanyDetailPanel).GetField("amountInput",Fields).GetValue(detail);
                    float cash=player.state.cash; float shares=company.state.getStat(CompanyStat.PLAYER_SHARES);
                    bool buying = beat.goal != StoryGoal.SellShares;
                    Check(!director.CanSkipToChoice,"Required transaction cannot be skipped: "+beat.id);
                    Check(buying ? !trading.sellShares(company,1) : !trading.buyShares(company,1),"Opposite transaction blocked: "+beat.id);
                    string method=buying?"buy":"sell";
                    input.text=int.MaxValue.ToString(); Click(Persistent(detail.transform,method));
                    Check(player.state.cash==cash && company.state.getStat(CompanyStat.PLAYER_SHARES)==shares,"Failed buy preserves state and does not advance");
                    input.text="1";
                    detail.show(company);
                    var cap=Await(Capture(buying ? beat.goal==StoryGoal.BuyMore?"15-buy-more":"04-buy-shares" : "16-sell-shares")); while(cap.MoveNext()) yield return null;
                    float price=company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
                    Click(Persistent(detail.transform,method));
                    Check(company.state.getStat(CompanyStat.PLAYER_SHARES)==shares+(buying?1:-1) && Mathf.Abs(player.state.cash-(cash+(buying?-price:price)))<.01f,"Actual UI transaction updates holdings and cash: "+beat.id);
                    Check(buying ? !trading.buyShares(company,1) : !trading.sellShares(company,1),"Repeated same-frame transaction rejected");
                }
                else if(beat.goal==StoryGoal.OpenEvent)
                {
                    Check(!director.CanSkipToChoice,"Required event-card discovery cannot be skipped");
                    Check(!director.View.HasEventOverlay && !director.GetComponent<GraphicRaycaster>().enabled,"Event discovery has no tutorial graphics or raycast blocker");
                    int ap=Object.FindFirstObjectByType<ActionPointService>().Current;
                    var card=Object.FindFirstObjectByType<DashboardCardSlide>();
                    Click(card.MainCard.GetComponent<Button>());
                    yield return null; yield return null;
                    Check(events.IsDetailOpen && events.ActiveEventId==StoryDirector.TutorialEventId,"Native event detail remains open across tutorial handoff");
                    Check(Object.FindFirstObjectByType<ActionPointService>().Current==ap,"Reading native detail does not consume AP");
                    var cap=Await(Capture("12-event-detail")); while(cap.MoveNext()) yield return null;
                    var eventDetail=Object.FindFirstObjectByType<EventDetailView>();
                    Click((Button)typeof(EventDetailView).GetField("backButton",Fields).GetValue(eventDetail));
                    yield return null; yield return null;
                    Check(events.IsClosed && director.Goal==StoryGoal.CompleteEvent,"Native back action closes detail without tutorial forcing it open");
                    Click(card.MainCard.GetComponent<Button>());
                    yield return null;

                }
                else if(beat.goal==StoryGoal.CompleteEvent)
                {
                    Check(!director.View.HasEventOverlay && !director.GetComponent<GraphicRaycaster>().enabled,"Native event has no guide strip, highlight, story skip or input mask");
                    var eventDetail=Object.FindFirstObjectByType<EventDetailView>();
                    int ap=Object.FindFirstObjectByType<ActionPointService>().Current;
                    float cash=player.state.cash;
                    int cost=events.getTodayEventInfos().First(e=>e.definition.getId()==StoryDirector.TutorialEventId).definition.getApCost();
                    var button=(Button)typeof(EventDetailView).GetField("startButton",Fields).GetValue(eventDetail);
                    var service=Object.FindFirstObjectByType<ActionPointService>();
                    typeof(ActionPointService).GetField("currentAp",Fields).SetValue(service,0);
                    Click(button);
                    Check(events.IsDetailOpen && service.Current==0 && !events.tryGetChoice(StoryDirector.TutorialEventId,out _),"Insufficient AP cannot start or commit event");
                    typeof(ActionPointService).GetField("currentAp",Fields).SetValue(service,ap);
                    Click(button); events.startNegotiation();
                    while (EventSceneTransition.IsBusy) yield return null;
                    Check(Object.FindFirstObjectByType<ActionPointService>().Current==ap-cost,"Negotiation consumes actual AP once");
                    var negotiation=Object.FindFirstObjectByType<EventNegotiationView>();
                    var cap=Await(Capture("13-event-choice")); while(cap.MoveNext()) yield return null;
                    var buttons=(List<Button>)typeof(EventNegotiationView).GetField("choices",Fields).GetValue(negotiation);
                    int chosen=StoryPlayVerification.SkipRegression?0:2;
                    Click(buttons[chosen]); events.confirmChoice((chosen+1)%3);
                    Check(events.tryGetChoice(StoryDirector.TutorialEventId,out int answer) && answer==chosen,"Player-selected event answer committed once");
                    Check(player.state.cash==cash && Object.FindFirstObjectByType<ActionPointService>().Current==ap-cost,"Event answer has actual financial effects and no second AP charge");
                    var close=negotiation.ContinueButton;
                    while (!events.IsClosed)
                    {
                        if (close.gameObject.activeInHierarchy && close.IsInteractable() && !EventSceneTransition.IsBusy)
                            Click(close);
                        yield return null;
                    }
                    while (EventSceneTransition.IsBusy) yield return null;
                }
                else if(beat.goal==StoryGoal.ViewCompany || beat.goal==StoryGoal.CompareCompany || beat.goal==StoryGoal.ViewStake)
                {
                    var reading = Object.FindObjectsByType<CompanyDetailPanel>(FindObjectsSortMode.None).Single(panel => panel.IsTutorialPreview);
                    Check(reading.gameObject.activeInHierarchy && reading.current.definition.companyID==(string.IsNullOrEmpty(beat.companyId)?StoryDirector.CompanyId:beat.companyId),"Mandatory company comparison/holdings view uses target company: "+beat.id);
                    Check(!trading.buyShares(reading.current,1) && !trading.sellShares(reading.current,1),"Information review cannot accidentally trade");
                    {
                        Check(reading != detail && !detail.gameObject.activeInHierarchy, "Only the copied financial statement is shown: " + beat.id);
                        var tradeButtons = reading.GetComponentsInChildren<Button>(true).Where(b => b.name == "BuyButton" || b.name == "SellButton").ToArray();
                        Check(tradeButtons.Length == 2 && tradeButtons.All(b => !b.interactable && b.onClick.GetPersistentEventCount() == 0),
                            "Preview buy/sell controls are visibly disabled and disconnected: " + beat.id);
                        var labels = tradeButtons.SelectMany(b => b.GetComponentsInChildren<Graphic>(true)).Where(g => g is TMPro.TMP_Text || g is Text).ToArray();
                        Check(labels.Length >= 2 && labels.All(t =>
                            Mathf.Abs(t.color.r - t.color.g) < .001f && Mathf.Abs(t.color.g - t.color.b) < .001f && t.color.a < 1),
                            "Preview buy/sell labels use the shared light-gray disabled color: " + beat.id);
                        Check(!reading.GetComponentInChildren<TMPro.TMP_InputField>(true).interactable, "Preview quantity input is read-only: " + beat.id);
                        float unchangedCash = player.state.cash, unchangedShares = reading.current.state.getStat(CompanyStat.PLAYER_SHARES);
                        foreach (var button in tradeButtons) button.onClick.Invoke();
                        reading.buy(); reading.sell();
                        Check(player.state.cash == unchangedCash && reading.current.state.getStat(CompanyStat.PLAYER_SHARES) == unchangedShares,
                            "Preview callbacks and direct trade methods cannot change cash or holdings: " + beat.id);
                    }
                    var cap=Await(Capture(beat.goal==StoryGoal.ViewStake ? "14-holdings-" + beat.id : beat.goal==StoryGoal.CompareCompany?"14-company-comparison":"14-company-metrics"));while(cap.MoveNext())yield return null;
                    Click(Named(director.transform,"Confirm profile"));
                }
                else if(beat.goal==StoryGoal.ViewRepresentative || beat.goal==StoryGoal.ViewMemory || beat.goal==StoryGoal.ViewEventMemory)
                {
                    var status = Object.FindFirstObjectByType<NpcStatusScreen>();
                    var profile = status.GetComponentInChildren<NpcProfilePanelView>();
                    string target = beat.goal == StoryGoal.ViewRepresentative ? StoryDirector.Representative
                        : beat.goal == StoryGoal.ViewMemory ? StoryDirector.FieldDirector : StoryDirector.TutorialContact;
                    Check(status.SelectedNpcId == target, "Profile shows the tutorial NPC: " + target);
                    var detailText = profile.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "DescriptionText");
                    Check(detailText.text.Contains("기억") && detailText.text.Contains("호불호 단서"), "Profile exposes memory and preferences");
                    var other = profile.GetComponentsInChildren<NpcListItemView>()
                        .First(item => item.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == "윤하진"));
                    var rows = profile.GetComponentsInChildren<NpcListItemView>();
                    Check(rows.Count(row => row.GetComponent<Button>().IsInteractable()) == 1,
                        "Only the tutorial target NPC is interactable: " + beat.id);
                    Check(rows.Where(row => !row.GetComponent<Button>().IsInteractable()).All(row => row.GetComponent<CanvasGroup>().alpha < .5f),
                        "Other NPC buttons are visibly faded gray: " + beat.id);
                    other.GetComponent<Button>().onClick.Invoke(); status.SelectNpc("npc-broker-yoon-hajin");
                    Check(status.SelectedNpcId == target, "Disabled NPC callbacks and direct selection cannot change the tutorial target: " + beat.id);
                    Canvas.ForceUpdateCanvases();
                    Check(profile.GetComponent<RectMask2D>().isActiveAndEnabled && profile.GetComponentsInChildren<Image>().Single(image => image.name == "StandingIllustration").canvasRenderer.hasRectClipping,
                        "Tutorial standing illustration is clipped to its profile window: " + beat.id);
                    if (beat.goal != StoryGoal.ViewRepresentative) status.FocusMemories();
                    if(beat.goal==StoryGoal.ViewMemory)
                    {
                        registry.TryGet(StoryDirector.FieldDirector,out var state);
                        Check(state.memories.Any(m=>m.sourceEventId.StartsWith("intro-")),"Field conversation has real Memory");
                        Check(detailText.text.Contains("남은 기간"), "Field conversation displays the memory duration");
                        var scroll = detailText.GetComponentInParent<ScrollRect>();
                        Check(scroll.content.rect.height > scroll.viewport.rect.height && scroll.verticalNormalizedPosition < .999f,
                            "Tutorial memory is brought into the scrollable profile viewport");
                        var memory = state.memories.First();
                        bool permanent = memory.permanent;
                        try
                        {
                            memory.permanent = true;
                            registry.NotifyStateChanged(state);
                            Check(detailText.text.Contains("영구 기억"), "Permanent memory label updates in the active profile");
                        }
                        finally
                        {
                            memory.permanent = permanent;
                            registry.NotifyStateChanged(state);
                        }
                        Check(state.memories.All(m=>m.memoryType!=NpcMemoryType.SavedCompany && m.memoryType!=NpcMemoryType.GoodFaith),"Promise not recorded as fulfilled good faith");
                    }
                    var cap=Await(Capture(beat.goal==StoryGoal.ViewEventMemory?"14-event-memory":beat.goal==StoryGoal.ViewMemory?"07-memory":"06-profile")); while(cap.MoveNext()) yield return null;
                    Click(Named(director.transform,"Confirm profile"));
                }
                else if(beat.goal==StoryGoal.EndDay)
                {
                    var cap=Await(Capture("08-end-day")); while(cap.MoveNext()) yield return null;
                    var day=Object.FindFirstObjectByType<DaySkipController>();
                    // DaySkipController wires its listener at runtime, preserving the production path.
                    var button=(Button)typeof(DaySkipController).GetField("daySkipButton",Fields).GetValue(day);
                    Click(button);
                    clock.AdvanceTurn();
                    Check(clock.CurrentTurn==startTurn+1,"Day end progresses exactly once");
                }
                else if((int)beat.goal >= (int)StoryGoal.ViewEmployees)
                {
                    var tutorialCopy = Object.FindFirstObjectByType<OperationsTutorialView>();
                    var ops = tutorialCopy != null ? tutorialCopy.Screen : null;
                    Check(tutorialCopy != null && tutorialCopy.Ready,
                        "Operations explanation shows a read-only real screen: " + beat.id);
                    Check(director.View.InformationBody.preferredHeight <= director.View.InformationBody.rectTransform.rect.height + 1,
                        "Operations explanation fits its shared panel: " + beat.id);
                    var contentCorners = new Vector3[4];
                    var explanationCorners = new Vector3[4];
                    ops.TutorialContent.GetWorldCorners(contentCorners);
                    director.View.InformationRect.GetWorldCorners(explanationCorners);
                    Check(contentCorners[0].x > explanationCorners[2].x + 4,
                        "Operations content stays beside the explanation: " + beat.id);
                    Check(contentCorners[0].x >= 0 && contentCorners[0].y >= 0 &&
                        contentCorners[2].x <= Screen.width && contentCorners[2].y <= Screen.height,
                        "Whole operations content remains on screen: " + beat.id);
                    if (beat.goal == StoryGoal.ViewStartups)
                    {
                        var startups = ops.GetComponentInChildren<TakeOver.Operations.StartupTabView>();
                        Check(startups.IsTutorialPreview && startups.GetComponentsInChildren<Text>().Any(t => t.text.Contains("새봄테크")),
                            "Startup tutorial shows an actual read-only example screen instead of an empty transparent panel");
                        Check(startups.GetComponentInChildren<TakeOver.Operations.StartupLineGraph>().pointCount >= 2,
                            "Startup tutorial renders its sample revenue graph");
                        Check(startups.GetComponentsInChildren<Image>().Count(i => i.IsActive() && i.color.a > .9f) >= 3,
                            "Startup tutorial has opaque list and detail panels");
                    }
                    if (beat.goal == StoryGoal.ViewEmployees)
                    {
                        var staff = ops.GetComponentInChildren<TakeOver.Employees.EmployeeTabView>();
                        var recruit = (Button)typeof(TakeOver.Employees.EmployeeTabView).GetField("recruitButton", Fields).GetValue(staff);
                        var buttonCorners = new Vector3[4];
                        ((RectTransform)recruit.transform).GetWorldCorners(buttonCorners);
                        Check(recruit.gameObject.activeInHierarchy && buttonCorners[0].x > explanationCorners[2].x + 4 &&
                            buttonCorners[0].y >= 0 && buttonCorners[2].x <= Screen.width && buttonCorners[2].y <= Screen.height,
                            "Actual recruitment button is visible beside the explanation: " + beat.id);
                    }
                    var tutorialDocuments = ops.GetComponentInChildren<TakeOver.Operations.DocumentTabView>();
                    var preview = tutorialDocuments != null && tutorialDocuments.IsTutorialPreview
                        ? tutorialDocuments.GetComponentInChildren<TakeOver.Operations.DocumentDetailView>() : null;
                    if (preview != null)
                    {
                        Check(preview.GetComponentsInChildren<Button>().All(button => !button.IsInteractable()),
                            "Tutorial preview shows read-only controls without allowing decisions: " + beat.id);
                        Check(((Text)typeof(TakeOver.Operations.DocumentDetailView).GetField("deadline", Fields).GetValue(preview)).text == "D-7",
                            "Tutorial uses the standard deadline display: " + beat.id);
                        if (beat.goal == StoryGoal.ViewContract)
                            Check(preview.GetComponentsInChildren<Text>().Any(label => label.text.Contains("새봄테크")),
                                "Contract preview uses a fictional example company");
                    }
                    float currentCash = Object.FindFirstObjectByType<Player>().state.cash;
                    int currentDay = clock.CurrentTurn;
                    int currentAp = Object.FindFirstObjectByType<ActionPointService>().Current;
                    var cap=Await(Capture(beat.id)); while(cap.MoveNext()) yield return null;
                    Click(Named(director.transform,"Confirm profile"));
                    Check(Object.FindFirstObjectByType<Player>().state.cash == currentCash && clock.CurrentTurn == currentDay &&
                        Object.FindFirstObjectByType<ActionPointService>().Current == currentAp,
                        "Operations confirmation does not spend cash, AP or days: " + beat.id);
                }
                else if(beat.choices.Count>0)
                {
                    for (int i = 0; i < beat.choices.Count; i++)
                    {
                        var label = Named(director.transform, "Choice" + i).transform.Find("Description").GetComponent<Text>();
                        Check(label.IsActive() && label.text == beat.choices[i].description && label.preferredHeight <= label.rectTransform.rect.height + 1f,
                            "Choice summary renders inside its button: " + beat.id + "/" + i);
                    }
                    var cap=Await(Capture("05-dialogue-choices")); while(cap.MoveNext()) yield return null;
                    int before=director.Decisions.Count;
                    int chosen=StoryPlayVerification.SkipRegression ? 0 : choices%3;
                    choices++;
                    Click(Named(director.transform,"Choice"+chosen));
                    director.Choose((chosen+1)%3);
                    Check(director.Decisions.Count==before+1,"Choice double submission rejected: "+beat.id);
                    Check(director.View.Dialogue.alignment==TextAnchor.UpperRight,"Player reply uses shared player dialogue block");
                    Check(director.View.NpcImage.gameObject.activeSelf, "NPC remains present during the selected reply");
                    Check(director.View.SharedDialogue.CurrentDialogue.preferredHeight <= director.View.SharedDialogue.CurrentDialogue.rectTransform.rect.height + 1 && director.View.Dialogue.rectTransform.rect.width>=600,"Player reply page fits a readable body");
                    var rule=director.View.SharedDialogue.PlayerImage.transform.parent.parent;
                    var playerBlock=(CanvasGroup)typeof(EventNegotiationView).GetField("playerBlock",Fields).GetValue(director.View.SharedDialogue);
                    var line=(RectTransform)playerBlock.transform.Find("Underline");
                    var lineCorners=new Vector3[4];var textCorners=new Vector3[4];
                    line.GetWorldCorners(lineCorners);director.View.Dialogue.rectTransform.GetWorldCorners(textCorners);
                    Check(Mathf.Abs(lineCorners[2].x-textCorners[2].x)<1,"Player reply right edge follows speaker underline");
                    yield return null;
                    yield return null;
                    while(director.View.HasMoreDialoguePages)
                    { Click(director.View.ContinueButton); until=Time.unscaledTime+.22f; while(Time.unscaledTime<until)yield return null; }
                    until=Time.unscaledTime+.22f; while(Time.unscaledTime<until) yield return null;
                    Click(Named(director.transform,"Continue"));
                    Check(director.View.Dialogue.alignment==TextAnchor.UpperLeft,"NPC reply uses shared response dialogue block");
                    Check(director.View.SharedDialogue.CurrentDialogue.preferredHeight <= director.View.SharedDialogue.CurrentDialogue.rectTransform.rect.height + 1,"Current NPC response page fits its shared text area");
                    yield return null;
                    yield return null;
                    while(director.View.HasMoreDialoguePages)
                    { Click(director.View.ContinueButton); until=Time.unscaledTime+.22f; while(Time.unscaledTime<until)yield return null; }
                    until=Time.unscaledTime+.22f; while(Time.unscaledTime<until) yield return null;
                    Click(Named(director.transform,"Continue"));
                }
                else
                {
                    if(beat==sequence.beats.Last()) { var cap=Await(Capture("09-complete")); while(cap.MoveNext()) yield return null; }
                    Click(Named(director.transform,"Continue"));
                }
                yield return null;
            }
            while(StoryDirector.Active!=null) yield return null;
            CheckTopBar(DashboardTab.Home);
            Check(visited==sequence.beats.Count,"Every authored beat visited without skip");
            Check(choices==sequence.beats.Count(item => item.choices.Count > 0),"All authored conversations completed");
            Check(clock.CurrentTurn==startTurn+1,"Only one real turn consumed");
            Check(StoryDirector.CanTrade(company,false) && StoryDirector.CanNavigate(DashboardTab.Npcs),"Normal controls restored");
            Check(GameObject.Find("Tutorial completion notice")!=null && GameObject.Find("Help introduction")==null,"Normal completion shows reward before help notice");
            Check(Object.FindObjectsByType<TakeOver.Operations.StartupTabView>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(t => !t.IsTutorialPreview),
                "Startup example is removed after tutorial without creating an investment");
            Check(events.TutorialRewardGranted && company.state.getStat(CompanyStat.PLAYER_SHARES)==51,"Normal completion grants 50 bonus shares in addition to the one remaining share");
            Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Confirm tutorial reward"));
            yield return null;
            Check(GameObject.Find("Help introduction")!=null,"Reward confirmation opens help location notice");
            if (!StoryPlayVerification.SkipRegression)
            {
                Click(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Confirm help notice"));
                yield return null;
                var navigation = Object.FindFirstObjectByType<TopBarNavigationController>();
                navigation.Navigate(DashboardTab.Npcs); yield return null; yield return null;
                var restoredProfile = Object.FindFirstObjectByType<NpcProfilePanelView>();
                Check(restoredProfile.GetComponentsInChildren<NpcListItemView>().All(row => row.GetComponent<Button>().IsInteractable()) &&
                    !restoredProfile.GetComponent<RectMask2D>().enabled, "All NPC buttons and normal standing framing are restored after tutorial completion");
                navigation.Navigate(DashboardTab.Home); yield return null;
                var home = Await(Capture("home-after-tutorial")); while (home.MoveNext()) yield return null;
                if (!StoryPlayVerification.FocusedSpeakerRoles)
                { var operations = VerifyOperations(); while (operations.MoveNext()) yield return null; }
            }
            events.refreshAvailability(); Check(events.getTodayEventInfos().Count>0,"Normal events available after intro");
            var journal = TakeOver.Journal.JournalEntryBuilder.buildStoryEntries(events, registry);
            Check(journal.Count == 8 && journal.All(entry => entry.people.Count > 0 && entry.keyPoints.Count > 0),
                "Played tutorial records its seven meetings and the financial review with choices and people");
            if(StoryPlayVerification.SkipRegression)
            {
                File.WriteAllText(Path.Combine(StorySkipVerification.Output,"normal-completed-state.json"),StorySkipState.Capture());
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= StorySkipState.Seed;
            }
            // Existing direct Game scene entry never requests the intro.
            if (StoryPlayVerification.FocusedSpeakerRoles)
            {
                var focused = FocusedEvents();
                while (focused.MoveNext()) yield return null;
                File.AppendAllText(Results, "COMPLETE\n");
                yield break;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
            yield return null; yield return null;
            Check(!StoryDirector.IsRunning,"Direct Game entry does not replay intro");
            File.AppendAllText(Results,"COMPLETE\n");
        }

        private IEnumerator FocusedEvents()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(SpeakerRoleInGameVerification.Verify(Check));
            while (stack.Count > 0)
            {
                var step = stack.Peek();
                if (!step.MoveNext()) { stack.Pop(); continue; }
                if (step.Current is IEnumerator nested) stack.Push(nested);
                else yield return null;
            }
        }
    }
}
