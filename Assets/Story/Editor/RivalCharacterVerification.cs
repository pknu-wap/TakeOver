using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    [InitializeOnLoad]
    public static class RivalCharacterVerification
    {
        private const string Key="TakeOver.RivalCharacters.Verify";
        internal static string Output=>Path.GetFullPath("../QA_RivalDepth");
        static RivalCharacterVerification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("TakeOver/Story/Verify Rival Character Designs")]
        public static void Run()
        {
            File.WriteAllText(Path.Combine(Output,"character-results.txt"),"RUNNING\n");SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
            var game=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));game.position=new Rect(40,40,1280,780);game.Show();game.Focus();
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Rival character verification").AddComponent<RivalCharacterProbe>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                if(Application.isBatchMode)EditorApplication.Exit(File.ReadAllText(Path.Combine(Output,"character-results.txt")).Contains("FAIL")?1:0);
            }
        }
    }
    public sealed class RivalCharacterProbe:MonoBehaviour
    {
        private string Results=>Path.Combine(RivalCharacterVerification.Output,"character-results.txt");
        private void Check(bool ok,string text)
        {if(!ok)throw new InvalidOperationException(text);File.AppendAllText(Results,"PASS: "+text+"\n");}
        private void OnEnable(){Application.logMessageReceived+=Log;}
        private void OnDisable(){Application.logMessageReceived-=Log;}
        private void Log(string message,string stack,LogType type)
        {if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)File.AppendAllText(Results,"FAIL runtime: "+message+"\n"+stack+"\n");}
        private IEnumerator Start()
        {
            yield return null;yield return null;
            bool failed=false;
            try{Verify();}catch(Exception error){File.AppendAllText(Results,"FAIL: "+error+"\n");failed=true;}
            if(!failed)
            {
                var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
                var status=Object.FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
                foreach(var item in new[]{new[]{"daeseong-industry","김성호","01-enduring-profile"},new[]{"polaris-finance","정유라","02-contract-profile"},new[]{"aurora-foodtech","정하린","03-insider-profile"}})
                {
                    var seed=registry.Profiles.Single(p=>p.companyId==item[0]&&p.profile.displayName==item[1]);
                    status.Open();status.SelectNpc(seed.npcId);
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(RivalCharacterVerification.Output,item[2]+".png"));
                    yield return null;yield return null;
                }
                File.AppendAllText(Results,"COMPLETE\n");
            }
            EditorApplication.isPlaying=false;
        }
        private void Verify()
        {
            var baseline=JsonUtility.FromJson<RivalCharacterAuthoring.Invariants>(File.ReadAllText(Path.Combine(RivalCharacterVerification.Output,"asset-invariants.json")));
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            Check(registry.Profiles.Count==30 && registry.States.Count==30,"All 30 NPCs still registered in Play mode");
            var stances=new List<NpcStoryStance>();
            foreach(var prior in baseline.npcs)
            {
                var definition=AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/NPC/Data/Profiles/"+prior.file+".asset");
                var seed=definition.CreateRuntimeSeed();var story=definition.CreateStoryDesignSnapshot();
                Check(seed.npcId==prior.id && seed.companyId==prior.companyId,"NPC ID and company unchanged: "+prior.file);
                Check(RivalCharacterAuthoring.SeedWithoutPublicText(definition)==prior.numericSeed,"Preferences, numeric effects, art and role unchanged: "+prior.file);
                Check(story.stance!=NpcStoryStance.Unspecified,"Explicit story stance: "+prior.file);stances.Add(story.stance);
                foreach(var field in typeof(NpcStoryDesign).GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType==typeof(string)))
                    Check(!string.IsNullOrWhiteSpace((string)field.GetValue(story)),"Complete story field: "+prior.file+"/"+field.Name);
                Check(registry.TryGetProfile(prior.id,out var runtime) && runtime.profile.personalitySummary==seed.profile.personalitySummary && runtime.profile.gameplayRoleSummary==seed.profile.gameplayRoleSummary,"Expanded public text reaches runtime profile: "+prior.file);
                Check(runtime.profile.personalitySummary.Contains("행동 양식:") && runtime.profile.gameplayRoleSummary.Contains("선호:") && runtime.profile.gameplayRoleSummary.Contains("기피:"),"Public personality/behavior/preferences/role readable: "+prior.file);
                var publicJson=JsonUtility.ToJson(runtime);
                Check(!publicJson.Contains("cooperationTrigger")&&!publicJson.Contains("informationScope")&&!publicJson.Contains("storyDesign"),"Hidden plot conditions excluded from registry public profile: "+prior.file);
                string goal=story.personalGoal;story.personalGoal="edited verification copy";
                Check(definition.CreateStoryDesignSnapshot().personalGoal==goal,"Authoring snapshot is independent: "+prior.file);
            }
            Check(stances.Count(s=>s==NpcStoryStance.EnduringAdversary)==4,"Four enduring adversaries");
            Check(stances.Count(s=>s==NpcStoryStance.ConditionalPartner)==4,"Four conditional deal-makers");
            Check(stances.Count(s=>s==NpcStoryStance.ProfessionalBoundary)==3,"Three professional-boundary actors");
            Check(stances.Count(s=>s==NpcStoryStance.AmbivalentInsider)==5,"Five conflicted insiders");
            foreach(var prior in baseline.companies)
            {
                var rival=AssetDatabase.LoadAssetAtPath<RivalCompanyDefinition>("Assets/Story/Data/Rivals/"+prior.companyId+".asset");
                Check(RivalCharacterAuthoring.CompanyWithoutDescription(rival.company)==prior.numericDefinition,"Company finances, IDs and art unchanged: "+prior.companyId);
                Check(!string.IsNullOrWhiteSpace(rival.loyaltyStructure)&&!string.IsNullOrWhiteSpace(rival.cooperationBoundaries),"Company loyalty and authority boundaries authored: "+prior.companyId);
            }
            var types=new[]{RivalAffiliationKind.IndependentPartner,RivalAffiliationKind.GroupSubsidiary,RivalAffiliationKind.ProjectPartner,RivalAffiliationKind.GroupSubsidiary,RivalAffiliationKind.StrategicInvestmentSubsidiary};
            for(int i=0;i<StoryContentBuilder.RivalIds.Length;i++)
            {
                var id=StoryContentBuilder.RivalIds[i];
                Check(AssetDatabase.LoadAssetAtPath<RivalCompanyDefinition>("Assets/Story/Data/Rivals/"+id+".asset").affiliationKind==types[i],"Company relationship matches design: "+id);
            }
            var channel=Object.FindFirstObjectByType<NpcEventChannel>();var harness=Object.FindFirstObjectByType<NpcFeatureTestHarness>();
            foreach(var prior in baseline.npcs)
            {
                harness.ResetAllNpcStates();
                channel.Publish(new NpcEventPayload{eventId="rival-design-qa-"+prior.file,companyId=prior.companyId,actorId="player",targetNpcIds=new List<string>{prior.id},memoryType=NpcMemoryType.Custom,relationDelta=new NpcRelationDelta{trust=2,respect=1},interpretation="기존 공용 사건 경로 회귀 검증"});
                registry.TryGet(prior.id,out var state);
                Check(state.memories.Count==1 && state.relation.trust==2 && state.relation.respect==1,"Existing channel and neutral Memory behavior retained: "+prior.file);
                Check(registry.States.Where(s=>s.npcId!=prior.id).All(s=>s.memories.Count==0),"No unintended NPC receives story text effects: "+prior.file);
            }
        }
    }
}
