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
        public static bool SkipRegression => SessionState.GetBool("TakeOver.StorySkip.Normal", false);
        public static string Output => Path.GetFullPath(SkipRegression ? "../QA_ContextUI/Normal" : "../QA_ContextUI");
        public static void RunSkipRegression()
        {
            SessionState.SetBool("TakeOver.StorySkip.Normal", true);
            Run();
        }
        static StoryPlayVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("TakeOver/Story/Verify New Game in Play Mode")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"play-results.txt"),"RUNNING\n");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
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
                if(Application.isBatchMode) EditorApplication.Exit(failed?1:0);
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
        private void Start()
        {
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
        private void OnDestroy() { Application.logMessageReceived-=OnLog; }
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
            if(!captures.Add(name)) yield break;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(StoryPlayVerification.Output,name+".png"));
            yield return null;
        }
        private static Button Named(Transform root,string name) => root.GetComponentsInChildren<Button>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);
        private static void Click(Button button)
        {
            if(!button.gameObject.activeInHierarchy || !button.interactable) throw new Exception("Button not available: "+button.name);
            var canvas=button.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var rect=button.GetComponent<RectTransform>();
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0 || !(hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)))
                throw new Exception("Button occluded: "+button.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private static Button Persistent(Transform root,string method)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
                for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
                    if(button.onClick.GetPersistentMethodName(i)==method) return button;
            throw new Exception("No button for "+method);
        }
        private IEnumerator Run()
        {
            // Drive nested waits explicitly: this iterator is ticked once per editor Play frame.
            yield return null; yield return null;
            Check(!StoryDirector.IsRunning,"Main menu has no active intro");
            if(StoryPlayVerification.SkipRegression)
            {
                Check(!Object.FindFirstObjectByType<MainMenuController>().SkipIntroToggle.isOn,"Title checkbox defaults off");
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += StorySkipState.Seed;
            }
            Object.FindFirstObjectByType<MainMenuController>().startNewGame();
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
                        Check(director.View.Speaker.text.Contains(registry.Profiles.First(p=>p.npcId==beat.npcId).profile.displayName),"NPC label matches profile: "+beat.id);
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
                    Check(director.View.SharedDialogue.DialoguePageCount==1,"Authored line fits one readable page without excessive wrapping: "+beat.id);
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
                    Check(Object.FindFirstObjectByType<ActionPointService>().Current==ap-cost,"Negotiation consumes actual AP once");
                    var negotiation=Object.FindFirstObjectByType<EventNegotiationView>();
                    var cap=Await(Capture("13-event-choice")); while(cap.MoveNext()) yield return null;
                    var buttons=(List<Button>)typeof(EventNegotiationView).GetField("choices",Fields).GetValue(negotiation);
                    int chosen=StoryPlayVerification.SkipRegression?0:2;
                    Click(buttons[chosen]); events.confirmChoice((chosen+1)%3);
                    Check(events.tryGetChoice(StoryDirector.TutorialEventId,out int answer) && answer==chosen,"Player-selected event answer committed once");
                    Check(player.state.cash==cash && Object.FindFirstObjectByType<ActionPointService>().Current==ap-cost,"Event answer has actual financial effects and no second AP charge");
                    var close=(Button)typeof(EventNegotiationView).GetField("closeButton",Fields).GetValue(negotiation);
                    while(!close.gameObject.activeInHierarchy)yield return null;
                    Click(close);
                }
                else if(beat.goal==StoryGoal.ViewCompany || beat.goal==StoryGoal.CompareCompany || beat.goal==StoryGoal.ViewStake)
                {
                    Check(detail.gameObject.activeInHierarchy && detail.current.definition.companyID==(string.IsNullOrEmpty(beat.companyId)?StoryDirector.CompanyId:beat.companyId),"Mandatory company comparison/holdings view uses target company: "+beat.id);
                    Check(!trading.buyShares(detail.current,1) && !trading.sellShares(detail.current,1),"Information review cannot accidentally trade");
                    var cap=Await(Capture(beat.goal==StoryGoal.CompareCompany?"14-company-comparison":"14-company-metrics"));while(cap.MoveNext())yield return null;
                    Click(Named(director.transform,"Confirm profile"));
                }
                else if(beat.goal==StoryGoal.ViewRepresentative || beat.goal==StoryGoal.ViewMemory || beat.goal==StoryGoal.ViewEventMemory)
                {
                    var cap=Await(Capture(beat.goal==StoryGoal.ViewEventMemory?"14-event-memory":beat.goal==StoryGoal.ViewMemory?"07-memory":"06-profile")); while(cap.MoveNext()) yield return null;
                    if(beat.goal==StoryGoal.ViewMemory)
                    {
                        registry.TryGet(StoryDirector.FieldDirector,out var state);
                        Check(state.memories.Any(m=>m.sourceEventId.StartsWith("intro-")),"Field conversation has real Memory");
                        Check(state.memories.All(m=>m.memoryType!=NpcMemoryType.SavedCompany && m.memoryType!=NpcMemoryType.GoodFaith),"Promise not recorded as fulfilled good faith");
                    }
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
                else if(beat.choices.Count>0)
                {
                    var cap=Await(Capture("05-dialogue-choices")); while(cap.MoveNext()) yield return null;
                    int before=director.Decisions.Count;
                    int chosen=StoryPlayVerification.SkipRegression ? 0 : choices%3;
                    choices++;
                    Click(Named(director.transform,"Choice"+chosen));
                    director.Choose((chosen+1)%3);
                    Check(director.Decisions.Count==before+1,"Choice double submission rejected: "+beat.id);
                    Check(director.View.Dialogue.alignment==TextAnchor.UpperRight,"Player reply uses shared player dialogue block");
                    Check(director.View.SharedDialogue.DialoguePageCount==1 && director.View.Dialogue.rectTransform.rect.width>=600,"Player reply has a readable single-page body");
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
                    Check(director.View.SharedDialogue.DialoguePageCount==1,"NPC response fits one readable page");
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
            Check(visited==sequence.beats.Count,"Every authored beat visited without skip");
            Check(choices==3,"All three conversations completed");
            Check(clock.CurrentTurn==startTurn+1,"Only one real turn consumed");
            Check(StoryDirector.CanTrade(company,false) && StoryDirector.CanNavigate(3),"Normal controls restored");
            events.refreshAvailability(); Check(events.getTodayEventInfos().Count>0,"Normal events available after intro");
            if(StoryPlayVerification.SkipRegression)
            {
                File.WriteAllText(Path.GetFullPath("../QA_ContextUI/normal-completed-state.json"),StorySkipState.Capture());
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= StorySkipState.Seed;
            }
            // Existing direct Game scene entry never requests the intro.
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
            yield return null; yield return null;
            Check(!StoryDirector.IsRunning,"Direct Game entry does not replay intro");
            File.AppendAllText(Results,"COMPLETE\n");
        }
    }
}
