using System;
using System.Collections;
using System.IO;
using System.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class RivalPlayVerification
    {
        private const string Key="TakeOver.Story.Rivals";
        public static string Output=>Path.GetFullPath("../QA_Story/rival-results.txt");
        static RivalPlayVerification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("TakeOver/Story/Verify Rival NPCs in Play Mode")]
        public static void Run()
        {
            File.WriteAllText(Output,"RUNNING\n"); SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
            var game=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            game.position=new Rect(40,40,1280,780); game.Show(); game.Focus();
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) new GameObject("Rival verification").AddComponent<RivalPlayProbe>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(File.ReadAllText(Output).Contains("FAIL")?1:0);}
        }
    }
    public sealed class RivalPlayProbe:MonoBehaviour
    {
        private void Check(bool condition,string text)
        {if(!condition)throw new InvalidOperationException(text);File.AppendAllText(RivalPlayVerification.Output,"PASS: "+text+"\n");}
        private void OnEnable(){Application.logMessageReceived+=Log;}
        private void OnDisable(){Application.logMessageReceived-=Log;}
        private void Log(string text,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception)File.AppendAllText(RivalPlayVerification.Output,"FAIL: "+text+"\n"+stack+"\n");}
        private IEnumerator Start()
        {
            yield return null;yield return null;
            try{Verify();}catch(Exception error){File.AppendAllText(RivalPlayVerification.Output,"FAIL: "+error+"\n");EditorApplication.isPlaying=false;yield break;}
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../QA_Story/10-rival-dialogue.png"));
            yield return null;yield return null;
            Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include).hide();
            var status=Object.FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
            status.Open();status.SelectNpc("npc-aurora-foodtech-chief-scientist-jung-harin");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("../QA_Story/11-rival-profile.png"));
            yield return null;yield return null;
            File.AppendAllText(RivalPlayVerification.Output,"COMPLETE\n");EditorApplication.isPlaying=false;
        }
        private void Verify()
        {
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            var companies=Object.FindFirstObjectByType<Companies>();
            var harness=Object.FindFirstObjectByType<NpcFeatureTestHarness>();
            var world=new EventWorldReader(companies,registry);
            var channel=Object.FindFirstObjectByType<NpcEventChannel>();
            var controller=Object.FindFirstObjectByType<EventSystemController>();
            var view=Object.FindFirstObjectByType<EventNegotiationView>(FindObjectsInactive.Include);
            var definition=AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/ManagementDefense.asset");
            foreach(var id in StoryContentBuilder.RivalIds)
            {
                var company=companies.getCompany(id);
                Check(company.state.getDerivedStat(CompanyDerivedStat.SHARE_PRICE)>0,"Rival stock price initialized: "+id);
                Check(registry.TryFindByPosition(id,NpcPosition.CEO,out var ceo),"Exactly one rival CEO: "+id);
                var info=new TodayEventInfo(definition,world.getCompany(id,definition),3);
                Check(info.speakerNpcId==ceo.npcId && info.speakerLabel.Contains(ceo.profile.displayName),"Rival event speaker and company resolve: "+id);
                var art=world.getSpeakerIllustration(info);
                Check(art!=null && art.texture!=null,"Missing character art uses shared silhouette: "+id);
                harness.ResetAllNpcStates();
                channel.Publish(new NpcEventPayload{eventId="rival-qa-"+id,companyId=id,actorId="player",targetNpcIds=new System.Collections.Generic.List<string>{ceo.npcId},memoryType=NpcMemoryType.GoodFaith,relationDelta=new NpcRelationDelta{trust=4},interpretation="확인된 거래 약속을 실제로 이행했다."});
                Check(registry.States.Single(s=>s.npcId==ceo.npcId).memories.Count==1,"Rival receives shared channel Memory: "+id);
                Check(registry.States.Where(s=>s.npcId!=ceo.npcId).All(s=>s.memories.Count==0),"Other companies' NPCs unchanged: "+id);
                var contact=AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Restructuring.asset");
                Check(contact.tryGetRestructuringNpcId(id,out var target) && registry.TryGetProfile(target,out var profile) && profile.companyId==id,"Rival restructuring contact registered: "+id);
                view.show(info,controller,art,world.getSpeakerDimmedIllustration(info));
            }
        }
    }
}
