using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Help;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HelpVerification
{
    public const string Output="../QA_Help/";
    static HelpVerification(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"results.txt","RUNNING\n");
        foreach(var path in Directory.GetFiles("Assets/Resources/HelpImages","*.png"))
        { var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport(); }
        SessionState.SetBool("Help.verify",true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
        var game=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));game.position=new Rect(40,40,1280,780);game.Show();game.Focus();
        EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool("Help.verify",false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){var probe=new GameObject("Help probe");UnityEngine.Object.DontDestroyOnLoad(probe);probe.AddComponent<HelpProbe>();}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Help.verify",false);EditorApplication.Exit(File.ReadAllText(Output+"results.txt").Contains("FAIL")?1:0);}
    }
}
public sealed class HelpProbe:MonoBehaviour
{
    void OnEnable(){Application.logMessageReceived+=OnLog;}
    void OnDisable(){Application.logMessageReceived-=OnLog;}
    void OnLog(string text,string stack,LogType type)
    {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)File.AppendAllText(HelpVerification.Output+"results.txt","FAIL runtime: "+text+"\n"+stack+"\n");}
    void Check(bool condition,string message){if(!condition)throw new Exception(message);File.AppendAllText(HelpVerification.Output+"results.txt","PASS: "+message+"\n");}
    IEnumerator Start()
    {
        yield return null;yield return null;
        try
        {
            var help=UnityEngine.Object.FindFirstObjectByType<HelpScreen>();
            Check(help!=null,"Help component initialized");
            Check(HelpTopics.Items.Length==11 && HelpTopics.Items.All(t=>t.title!="다시 시작하기"),"Removed artificial introductory topic");
            var tab=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="HelpTab");
            var template=UnityEngine.Object.FindFirstObjectByType<TakeOver.Events.EventNegotiationView>(FindObjectsInactive.Include);
            var question=(CanvasGroup)typeof(TakeOver.Events.EventNegotiationView).GetField("questionBlock",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(template);
            var questionRect=(RectTransform)question.transform;var initialPosition=questionRect.anchoredPosition;
            questionRect.anchoredPosition+=Vector2.up*40;
            tab.onClick.Invoke();Check(help.IsOpen,"Top bar opens help");
            Canvas.ForceUpdateCanvases();
            var title=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t=>t.text=="도움말 · 게임 기능과 용어");
            var titleCorners=new Vector3[4];var tabCorners=new Vector3[4];title.rectTransform.GetWorldCorners(titleCorners);((RectTransform)tab.transform).GetWorldCorners(tabCorners);
            Check(titleCorners[2].y<tabCorners[0].y-8,"Help title stays below top bar despite native dialogue animation");
            var headerClose=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Close help");
            var closeCorners=new Vector3[4];((RectTransform)headerClose.transform).GetWorldCorners(closeCorners);
            var lineCorners=new Vector3[4];((RectTransform)title.transform.parent.Find("Underline")).GetWorldCorners(lineCorners);
            Check(lineCorners[2].y<closeCorners[0].y-4,"Help close button has clear space above divider");
            Check(title.transform.parent==headerClose.transform.parent,"Help header uses one independent layout frame");
            questionRect.anchoredPosition=initialPosition;
            foreach(var topic in HelpTopics.Items)Check(topic.text.Length>150,"Detailed content: "+topic.title);
            foreach(var name in new[]{"company","buy","sell","day","event","profile","memory"})Check(Resources.Load<Sprite>("HelpImages/"+name)!=null,"Screenshot: "+name);
        }
        catch(Exception e){File.AppendAllText(HelpVerification.Output+"results.txt","FAIL: "+e+"\n");EditorApplication.isPlaying=false;yield break;}
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(HelpVerification.Output+"help.png"));
        var buttons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        buttons.Single(b=>b.name=="Help topic 2").onClick.Invoke();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(HelpVerification.Output+"finance.png"));
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Explanation image").onClick.Invoke();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(HelpVerification.Output+"photo.png"));
        var close=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Close enlarged image");
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,close.transform.TransformPoint(((RectTransform)close.transform).rect.center))};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && (hits[0].gameObject==close.gameObject || hits[0].gameObject.transform.IsChildOf(close.transform)),"Photo close button receives actual pointer raycast");
        UnityEngine.EventSystems.ExecuteEvents.Execute(close.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        yield return null;
        Check(GameObject.Find("Help enlarged image")==null,"Pointer click closes photo and returns to help");
        UnityEngine.Object.FindFirstObjectByType<HelpScreen>().Close();HelpScreen.ShowIntroduction();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(HelpVerification.Output+"introduction.png"));
        yield return null;
        TakeOver.Story.StoryLaunch.RequestNewGame(true);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        yield return null;yield return null;
        float until=Time.realtimeSinceStartup+45;
        while((TakeOver.Story.StoryDirector.IsRunning || GameObject.Find("Help introduction")==null) && Time.realtimeSinceStartup<until)yield return null;
        yield return null;
        try{Check(!TakeOver.Story.StoryDirector.IsRunning && GameObject.Find("Help introduction")!=null,"Whole tutorial skip always shows help introduction");}
        catch(Exception e){File.AppendAllText(HelpVerification.Output+"results.txt","FAIL: "+e+"\n");}
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Open help").onClick.Invoke();
        yield return null;
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Help topic 2").onClick.Invoke();
        for(int cycle=0;cycle<5;cycle++)
        {
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Explanation image").onClick.Invoke();
            yield return null;
            var photoClose=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Close enlarged image");
            Check(photoClose!=null,"Photo close created after tutorial destruction: "+cycle);
            photoClose.onClick.Invoke();yield return null;
            Check(GameObject.Find("Help enlarged image")==null,"Repeated photo close after tutorial: "+cycle);
        }
        File.AppendAllText(HelpVerification.Output+"results.txt","COMPLETE\n");
        yield return new WaitForSeconds(.3f);EditorApplication.isPlaying=false;
    }
}
