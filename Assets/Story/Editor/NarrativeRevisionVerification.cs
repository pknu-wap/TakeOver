using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class NarrativeRevisionVerification
{
    public const string Output="../QA_NarrativeRevision/";
    static NarrativeRevisionVerification(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"results.txt","RUNNING\n");SessionState.SetBool("NarrativeRevision",true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool("NarrativeRevision",false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){var go=new GameObject("Narrative regression");Object.DontDestroyOnLoad(go);go.AddComponent<NarrativeRevisionProbe>();}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("NarrativeRevision",false);EditorApplication.Exit(File.ReadAllText(Output+"results.txt").Contains("FAIL")?1:0);}
    }
}
public sealed class NarrativeRevisionProbe:MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    void Check(bool value,string message){if(!value)throw new Exception(message);File.AppendAllText(NarrativeRevisionVerification.Output+"results.txt","PASS: "+message+"\n");}
    void OnEnable(){Application.logMessageReceived+=Log;}
    void OnDisable(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)File.AppendAllText(NarrativeRevisionVerification.Output+"results.txt","FAIL runtime: "+message+"\n"+stack+"\n");}
    IEnumerator Start()
    {
        var run=Verify();
        while(true){bool more;try{more=run.MoveNext();}catch(Exception e){File.AppendAllText(NarrativeRevisionVerification.Output+"results.txt","FAIL: "+e+"\n");break;}if(!more)break;yield return run.Current;}
        EditorApplication.isPlaying=false;
    }
    bool Meets(EventSystemController controller,EventDefinition definition,string company)=>
        (bool)typeof(EventSystemController).GetMethod("meetsConditions",Flags).Invoke(controller,new object[]{definition,company});
    IEnumerator Verify()
    {
        for(int answer=0;answer<3;answer++)
        {
            if(answer>0)yield return SceneManager.LoadSceneAsync("Game");
            yield return null;yield return null;yield return null;
            var controller=Object.FindFirstObjectByType<EventSystemController>();
            Check(!controller.getTodayEvents().Any(e=>e.getId()==StoryDirector.TutorialEventId),"Tutorial-only event hidden in free play: "+answer);
            var definition=Object.Instantiate(Resources.Load<EventDefinition>("Story/TutorialVisit"));
            var serialized=new SerializedObject(definition);serialized.FindProperty("tutorialOnly").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
            var events=(List<EventDefinition>)typeof(EventSystemController).GetField("events",Flags).GetValue(controller);
            events.RemoveAll(e=>e.getId()==StoryDirector.TutorialEventId);events.Add(definition);
            var clock=Object.FindFirstObjectByType<GameTurnClock>();clock.AdvanceTurn();
            var player=Object.FindFirstObjectByType<Player>();var company=Object.FindFirstObjectByType<Companies>().getCompany(StoryDirector.CompanyId);
            var ap=Object.FindFirstObjectByType<ActionPointService>();int beforeAp=ap.Current;float cash=player.state.cash,shares=company.state.getStat(CompanyStat.PLAYER_SHARES);
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();registry.TryGet(StoryDirector.TutorialContact,out var state);int count=state.memories.Count;
            controller.openEvent(StoryDirector.TutorialEventId);controller.startNegotiation();controller.confirmChoice(answer);controller.confirmChoice((answer+1)%3);
            Check(ap.Current==beforeAp-definition.getApCost(),"New event consumes AP exactly once: "+answer);
            Check(player.state.cash==cash && company.state.getStat(CompanyStat.PLAYER_SHARES)==shares,"New event creates no financial/ownership contract: "+answer);
            Check(state.memories.Count==count+1 && state.memories.Last().memoryType==NpcMemoryType.Custom,"Correct contact receives one observational memory: "+answer);
            Check(!string.IsNullOrEmpty(state.memories.Last().interpretation) && state.memories.Last().actorId=="player","Memory describes the real player's observation: "+answer);
            controller.closeResult();Object.Destroy(definition);
        }
        var all=Object.FindFirstObjectByType<Companies>();var mirae=all.getCompany(StoryDirector.CompanyId);var game=Object.FindFirstObjectByType<EventSystemController>();
        var inside=AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/InsideInfo.asset");
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,99);mirae.calculator.calculateAboutShares(mirae);
        Check(!Meets(game,inside,StoryDirector.CompanyId),"Main-story InsideInfo is not opened by tutorial's small holding");
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,100);mirae.calculator.calculateAboutShares(mirae);
        Check(Meets(game,inside,StoryDirector.CompanyId),"Main-story InsideInfo opens at 10 percent");
        Check(!Meets(game,inside,"haneul-food"),"Fixed company event cannot select another company");
        game.onDayStarted(game.getCurrentDay()+1);game.openEvent("inside_info_01");
        Check(game.IsDetailOpen,"Eligible main-story detail opens");
        int detailAp=Object.FindFirstObjectByType<ActionPointService>().Current;
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,99);mirae.calculator.calculateAboutShares(mirae);game.startNegotiation();
        Check(game.IsClosed && Object.FindFirstObjectByType<ActionPointService>().Current==detailAp,"Lost stake closes stale detail without consuming AP");
        var relationBranch=Object.Instantiate(inside);var edit=new SerializedObject(relationBranch);var requirements=edit.FindProperty("conditions");requirements.arraySize=2;
        var relationRequirement=requirements.GetArrayElementAtIndex(1);relationRequirement.FindPropertyRelative("kind").enumValueIndex=0;relationRequirement.FindPropertyRelative("npcId").stringValue="";relationRequirement.FindPropertyRelative("relationAxis").enumValueIndex=0;relationRequirement.FindPropertyRelative("comparison").enumValueIndex=0;relationRequirement.FindPropertyRelative("value").floatValue=5;edit.ApplyModifiedPropertiesWithoutUndo();
        Object.FindFirstObjectByType<NpcStateRegistry>().TryGet(StoryDirector.Representative,out var ceo);ceo.relation.trust=0;
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,100);mirae.calculator.calculateAboutShares(mirae);
        Check(!Meets(game,relationBranch,StoryDirector.CompanyId),"Rebuying shares does not bypass a failing CEO relationship condition");
        ceo.relation.trust=5;Check(Meets(game,relationBranch,StoryDirector.CompanyId),"Branch requires both stake and that company's CEO relation");Object.Destroy(relationBranch);
        var restructure=AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Restructuring.asset");
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,510);mirae.state.setStat(CompanyStat.COST,mirae.state.getStat(CompanyStat.REVENUE)+100);mirae.calculator.recalculate(mirae,Object.FindFirstObjectByType<Marketdata>());
        Check(Meets(game,restructure,StoryDirector.CompanyId),"Loss-making company permits restructuring at 51 percent");
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,509);mirae.calculator.calculateAboutShares(mirae);
        Check(!Meets(game,restructure,StoryDirector.CompanyId),"Losing required stake removes restructuring branch");
        mirae.state.setStat(CompanyStat.PLAYER_SHARES,510);mirae.state.setStat(CompanyStat.COST,0);mirae.calculator.recalculate(mirae,Object.FindFirstObjectByType<Marketdata>());mirae.state.beforeNetProfit=mirae.state.getDerivedStat(CompanyDerivedStat.NET_PROFIT);
        Check(!Meets(game,restructure,StoryDirector.CompanyId),"Stable profitable company is not offered restructuring just for stake");
        var effects=Object.FindFirstObjectByType<TradingEventEffects>();float held=mirae.state.getStat(CompanyStat.PLAYER_SHARES);float money=Object.FindFirstObjectByType<Player>().state.cash;
        effects.Apply(new ChoiceConfirmedInfo("greenmail_01",1,"reject",2,null,0,StoryDirector.CompanyId));
        Check(mirae.state.getStat(CompanyStat.PLAYER_SHARES)==held && Object.FindFirstObjectByType<Player>().state.cash==money && !mirae.state.greenmailBuyBlocked,"Rejecting greenmail does not sell holdings or block trading");
        effects.Apply(new ChoiceConfirmedInfo("greenmail_01",0,"accept",2,null,0,StoryDirector.CompanyId,buybackPremiumRatio:.1f));
        Check(mirae.state.getStat(CompanyStat.PLAYER_SHARES)==0 && mirae.state.greenmailBuyBlocked,"Accepted greenmail cashes out and blocks future buying");
        Check(!Object.FindFirstObjectByType<ShareTradingSystem>().buyShares(mirae,1),"Buy block enforced by actual trading service");
        var adapter=Object.FindFirstObjectByType<TradingSaveAdapter>();var save=adapter.captureJson();
        Check(JsonUtility.FromJson<TradingSaveData>(save).companies.Single(c=>c.companyId==StoryDirector.CompanyId).state.greenmailBuyBlocked,"Permanent buy restriction is included in saved company state");
        Check(!JsonUtility.FromJson<TradingSaveData.CompanyStateData>("{}").greenmailBuyBlocked,"Older save without restriction field remains compatible");
        var day=new TakeOver.SaveSystem.DayStartData(Object.FindFirstObjectByType<GameTurnClock>().CurrentTurn,TakeOver.SaveSystem.FixedDaySeed.seed);
        adapter.prepareLoad(save,adapter.saveVersion,day);adapter.applyLoad();adapter.rebuildAfterLoad();
        Check(mirae.state.greenmailBuyBlocked && !Object.FindFirstObjectByType<ShareTradingSystem>().buyShares(mirae,1),"Buy restriction remains enforced after actual save restoration");
        Check(!all.getCompany("haneul-food").state.greenmailBuyBlocked,"Buy restriction does not leak into another company");
        string legacy=save.Replace("\"greenmailBuyBlocked\":true,","").Replace("\"greenmailBuyBlocked\":false,","");
        adapter.prepareLoad(legacy,adapter.saveVersion,day);adapter.applyLoad();adapter.rebuildAfterLoad();
        Check(!mirae.state.greenmailBuyBlocked,"Legacy save without buy restriction loads through actual save adapter");
        File.AppendAllText(NarrativeRevisionVerification.Output+"results.txt","COMPLETE\n");
    }
}
