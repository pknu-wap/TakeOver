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
        public static string Output => Path.GetFullPath(SkipRegression ? "../QA_StorySkip/Normal" : "../QA_Story");
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
            deadline=Time.realtimeSinceStartup+240;
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
        private static Button Named(Transform root,string name) => root.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
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
                    if(beat.goal==StoryGoal.None && beat.speaker==StorySpeaker.Npc)
                        Check(director.View.Speaker.text.Contains(registry.Profiles.First(p=>p.npcId==beat.npcId).profile.displayName),"NPC label matches profile: "+beat.id);
                    if(StoryPlayVerification.SkipRegression)
                    {
                        Check(Named(director.transform,"Skip to choice").gameObject.activeInHierarchy,"Skip control stays visible: "+beat.id);
                        bool childhood=beat.sceneTitle.EndsWith("마지막 납품")||beat.sceneTitle.EndsWith("제안서")||beat.sceneTitle.EndsWith("빈자리");
                        if(childhood)Check(!director.View.PlayerImage.gameObject.activeSelf,"Childhood victim scene hides adult standing: "+beat.id);
                        else if(beat.speaker==StorySpeaker.Player && beat.goal==StoryGoal.None)Check(director.View.PlayerImage.gameObject.activeSelf,"Adult player standing retained: "+beat.id);
                    }
                }
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
                else if(beat.goal==StoryGoal.BuyShares)
                {
                    var input=(TMPro.TMP_InputField)typeof(CompanyDetailPanel).GetField("amountInput",Fields).GetValue(detail);
                    float cash=player.state.cash; float shares=company.state.getStat(CompanyStat.PLAYER_SHARES);
                    Check(!trading.sellShares(company,1),"Selling blocked during buy tutorial");
                    input.text=int.MaxValue.ToString(); Click(Persistent(detail.transform,"buy"));
                    Check(player.state.cash==cash && company.state.getStat(CompanyStat.PLAYER_SHARES)==shares,"Failed buy preserves state and does not advance");
                    input.text="1";
                    var cap=Await(Capture("04-buy-shares")); while(cap.MoveNext()) yield return null;
                    float price=company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE);
                    Click(Persistent(detail.transform,"buy"));
                    Check(company.state.getStat(CompanyStat.PLAYER_SHARES)==shares+1 && Mathf.Abs(player.state.cash-(cash-price))<.01f,"Actual UI buy updates holdings and cash");
                    Check(!trading.buyShares(company,1),"Repeated same-frame purchase rejected");
                }
                else if(beat.goal==StoryGoal.ViewRepresentative || beat.goal==StoryGoal.ViewMemory)
                {
                    var cap=Await(Capture(beat.goal==StoryGoal.ViewMemory?"07-memory":"06-profile")); while(cap.MoveNext()) yield return null;
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
                    Check(director.View.Dialogue.alignment==TextAnchor.UpperRight,"Player reply aligned to player");
                    until=Time.unscaledTime+.22f; while(Time.unscaledTime<until) yield return null;
                    Click(Named(director.transform,"Continue"));
                    Check(director.View.Dialogue.alignment==TextAnchor.UpperLeft,"NPC reply aligned to NPC");
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
                File.WriteAllText(Path.GetFullPath("../QA_StorySkip/normal-completed-state.json"),StorySkipState.Capture());
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
