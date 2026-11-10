using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using TakeOver.MainMenu;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class StorySkipVerification
    {
        private const string Key="TakeOver.StorySkip.Verify";
        internal static string Output=>Path.GetFullPath("../QA_ContextUI");
        static StorySkipVerification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("TakeOver/Story/Verify Intro Skip Options")]
        public static void Run()
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output,"skip-results.txt"),"RUNNING\n");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var game=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));game.position=new Rect(40,40,1280,780);game.Show();game.Focus();
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                var go=new GameObject("Skip verification");Object.DontDestroyOnLoad(go);go.AddComponent<StorySkipProbe>();
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                if(Application.isBatchMode)EditorApplication.Exit(File.ReadAllText(Path.Combine(Output,"skip-results.txt")).Contains("FAIL")?1:0);
            }
        }
    }
    public sealed class StorySkipProbe:MonoBehaviour
    {
        private string Results=>Path.Combine(StorySkipVerification.Output,"skip-results.txt");
        private float deadline;
        private void Check(bool value,string text)
        {if(!value)throw new InvalidOperationException(text);File.AppendAllText(Results,"PASS: "+text+"\n");}
        private void OnLog(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)File.AppendAllText(Results,"FAIL runtime: "+text+"\n"+stack+"\n");}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;SceneManager.sceneLoaded-=StorySkipState.Seed;}
        private void Update(){if(deadline>0 && Time.realtimeSinceStartup>deadline){File.AppendAllText(Results,"FAIL: Timeout\n");EditorApplication.isPlaying=false;}}
        private IEnumerator Start()
        {
            deadline=Time.realtimeSinceStartup+180; Application.logMessageReceived+=OnLog;
            SceneManager.sceneLoaded+=StorySkipState.Seed;
            var test=Verify();
            while(true)
            {
                bool next=false;object current=null;Exception failure=null;
                try{next=test.MoveNext();if(next)current=test.Current;}catch(Exception error){failure=error;}
                if(failure!=null){File.AppendAllText(Results,"FAIL: "+failure+"\n");break;}
                if(!next)break;
                yield return current;
            }
            deadline=0;EditorApplication.isPlaying=false;
        }
        private static Button Button(Transform parent,string name)=>parent.GetComponentsInChildren<Button>(true).Single(b=>b.name==name && b.gameObject.activeInHierarchy);
        private static void Click(Selectable selectable)
        {
            if(!selectable.IsInteractable() || !selectable.gameObject.activeInHierarchy)throw new Exception("Unavailable control: "+selectable.name);
            var canvas=selectable.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var rect=selectable.GetComponent<RectTransform>();
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || !(hits[0].gameObject==selectable.gameObject || hits[0].gameObject.transform.IsChildOf(selectable.transform)))
                throw new Exception("Occluded control: "+selectable.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
            ExecuteEvents.Execute(selectable.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static void NewGame()
        {
            var button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="startNewGame"));
            Click(button);
        }
        private IEnumerator Verify()
        {
            yield return null;yield return null;
            var menu=Object.FindFirstObjectByType<MainMenuController>();
            Check(menu.SkipIntroToggle!=null && !menu.SkipIntroToggle.isOn,"Serialized title checkbox defaults off");
            Check(menu.SkipIntroToggle.GetComponentInChildren<Text>().fontSize<=20,"Title option is small");
            var corners=new Vector3[4];menu.SkipIntroToggle.GetComponent<RectTransform>().GetWorldCorners(corners);
            var camera=menu.SkipIntroToggle.GetComponentInParent<Canvas>().worldCamera;
            Check(RectTransformUtility.WorldToScreenPoint(camera,corners[0]).y<80,"Title option anchored to screen bottom");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(StorySkipVerification.Output,"01-title-default-off.png"));yield return null;
            Click(menu.SkipIntroToggle);Check(menu.SkipIntroToggle.isOn,"Title checkbox toggles through UI");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(StorySkipVerification.Output,"02-title-skip-on.png"));yield return null;
            NewGame();
            while(StoryDirector.Active==null)yield return null;
            bool sawDialogue=false;
            while(StoryDirector.Active!=null)
            {sawDialogue|=StoryDirector.Active.View.Dialogue.gameObject.activeInHierarchy;yield return null;}
            Check(!sawDialogue,"Whole-intro skip shows no story or choices");
            var fullState=StorySkipState.Capture();
            File.WriteAllText(Path.Combine(StorySkipVerification.Output,"skipped-completed-state.json"),fullState);
            var normalState=File.ReadAllText(Path.Combine(StorySkipVerification.Output,"normal-completed-state.json"));
            Check(fullState==normalState,"Whole skip matches normal completed tutorial: cash, all company data/history, NPC relations/Memory, date, AP");
            Check(Object.FindFirstObjectByType<GameTurnClock>().CurrentTurn==2,"Whole skip advances exactly one real day");
            var company=Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
            Check(company.state.getStat(CompanyStat.PLAYER_SHARES)==1,"Whole skip completes two purchases and one sale, leaving one share");
            Check(StoryDirector.CanNavigate(3) && StoryDirector.CanTrade(company,false),"Whole skip releases normal gameplay controls");
            Check(Object.FindFirstObjectByType<DashboardCardSlide>().MainCard.gameObject.activeInHierarchy,"Whole skip restores remaining real event cards");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(StorySkipVerification.Output,"03-whole-skip-complete.png"));yield return null;

            SceneManager.LoadScene("MainMenu");yield return null;yield return null;
            menu=Object.FindFirstObjectByType<MainMenuController>();
            Check(!menu.SkipIntroToggle.isOn,"Returning to title resets option off");
            NewGame();while(StoryDirector.Active==null || StoryDirector.Active.Current==null || StoryDirector.Active.Busy)yield return null;
            var director=StoryDirector.Active;
            var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            int choices = 0;
            var goals = new System.Collections.Generic.HashSet<string>();
            while (StoryDirector.IsRunning)
            {
                director = StoryDirector.Active;
                if (director.Busy || director.Current == null) { yield return null; continue; }
                yield return new WaitForSeconds(.24f);
                var beat = director.Current;
                if (beat.goal != StoryGoal.None)
                {
                    goals.Add(beat.id);
                    Check(!director.CanSkipToChoice, "Dialogue skip cannot bypass required goal: " + beat.id);
                    int index = director.BeatIndex;
                    director.SkipToNextChoice();
                    Check(director.BeatIndex == index && !director.Busy, "Required goal stays active after skip attempt: " + beat.id);
                    var companies = Object.FindFirstObjectByType<Companies>();
                    var detail = Object.FindFirstObjectByType<CompanyDetailPanel>(FindObjectsInactive.Include);
                    var events = Object.FindFirstObjectByType<TakeOver.Events.EventSystemController>();
                    switch (beat.goal)
                    {
                        case StoryGoal.SelectCompany:
                            var node = Object.FindObjectsByType<CompanyMapNode>(FindObjectsSortMode.None).First(n=>n.company.definition.companyID==StoryDirector.CompanyId);
                            Click(node.GetComponentInChildren<Button>());
                            break;
                        case StoryGoal.BuyShares:
                        case StoryGoal.BuyMore:
                        case StoryGoal.SellShares:
                            var input = (TMP_InputField)typeof(CompanyDetailPanel).GetField("amountInput",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(detail);
                            input.text = "1";
                            string method = beat.goal == StoryGoal.SellShares ? "sell" : "buy";
                            var transaction = detail.GetComponentsInChildren<Button>().First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)==method));
                            yield return new WaitForEndOfFrame();
                            ScreenCapture.CaptureScreenshot(Path.Combine(StorySkipVerification.Output,"guided-"+beat.goal+".png"));
                            yield return null;
                            Click(transaction);
                            break;
                        case StoryGoal.EndDay:
                            Click(Object.FindFirstObjectByType<DaySkipController>().DaySkipButton);
                            break;
                        case StoryGoal.OpenEvent:
                            if (!events.IsDetailOpen)
                            {
                                Click(Object.FindFirstObjectByType<DashboardCardSlide>().MainCard.GetComponent<Button>());
                                yield return null;
                            }
                            Check(!director.View.HasEventOverlay,"Native detail is shown without tutorial overlay");
                            break;
                        case StoryGoal.CompleteEvent:
                            Check(!director.View.HasEventOverlay && !director.GetComponent<GraphicRaycaster>().enabled,"Native event retains sole control of its presentation and input");
                            var eventDetail = Object.FindFirstObjectByType<TakeOver.Events.EventDetailView>();
                            Click((Button)typeof(TakeOver.Events.EventDetailView).GetField("startButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(eventDetail));
                            yield return null;
                            yield return null;
                            var negotiation = Object.FindFirstObjectByType<TakeOver.Events.EventNegotiationView>();
                            var buttons = (System.Collections.Generic.List<Button>)typeof(TakeOver.Events.EventNegotiationView).GetField("choices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(negotiation);
                            Click(buttons[0]);
                            var close = (Button)typeof(TakeOver.Events.EventNegotiationView).GetField("closeButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(negotiation);
                            while (!close.gameObject.activeInHierarchy) yield return null;
                            yield return null;
                            yield return null;
                            Click(close);
                            break;
                        default:
                            Click(Button(director.transform,"Confirm profile"));
                            break;
                    }
                }
                else if (beat.choices.Count > 0)
                {
                    Check(!director.CanSkipToChoice,"Unanswered dialogue choice cannot be skipped: "+beat.id);
                    if(director.View.HasMoreDialoguePages)
                    { Click(director.View.ContinueButton); yield return null; continue; }
                    int[] answers = {2,1,0};
                    Click(Button(director.transform,"Choice"+answers[choices++]));
                    yield return new WaitForSeconds(.24f);
                    Click(Button(director.transform,"Skip to choice"));
                    director.SkipToNextChoice();
                }
                else
                {
                    Click(Button(director.transform,"Skip to choice"));
                    director.SkipToNextChoice();
                }
                yield return null;
            }
            while (StoryDirector.Active != null) yield return null;
            Check(goals.Count == 14,"Dialogue skipping still requires all 14 learning objectives");
            Check(choices == 3,"No introductory choice was auto-selected");
            var clock=Object.FindFirstObjectByType<GameTurnClock>();
            Check(clock.CurrentTurn==2,"Dialogue skipping advances the actual day once");
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            Check(registry.States.Sum(s=>s.memories.Count)==4,"Three conversations and one event record memories without duplicates");
            Check(Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId).state.getStat(CompanyStat.PLAYER_SHARES)==1,"Initial buy, additional buy and first sale all complete");
            Check(Object.FindFirstObjectByType<TakeOver.Events.EventSystemController>().tryGetChoice(StoryDirector.TutorialEventId,out int selected) && selected==0,"General event choice committed during guided playback");
            Check(Object.FindFirstObjectByType<DashboardCardSlide>().MainCard.gameObject.activeInHierarchy,"Guided completion restores remaining real event cards");

            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ShareTrading/Fonts/NotoSansKR-Regular SDF.asset");
            Check(font.atlasPopulationMode==AtlasPopulationMode.Dynamic && font.isMultiAtlasTexturesEnabled,"Dynamic Korean font and multi-atlas retained");
            Check(!new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue,"Build-time font mutation disabled");
            string characters=new string(Enumerable.Range(0xac00,600).Select(i=>(char)i).ToArray());
            Check(font.TryAddCharacters(characters,out string missing) && string.IsNullOrEmpty(missing),"600 Korean syllables added without missing glyphs");
            Check(font.atlasTextureCount>1,"Korean glyph overflow expands to additional atlas textures");
            var property=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.CrashReporting.CrashReportingSettings",false)).First(t=>t!=null)
                .GetProperty("canUploadReports",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            Check(!(bool)property.GetValue(null),"Unused Cloud native-symbol upload path is disabled");
            File.AppendAllText(Results,"COMPLETE\n");
        }
    }
}
