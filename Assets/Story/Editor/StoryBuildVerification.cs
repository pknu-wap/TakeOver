using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TakeOver.Story.Editor
{
    public static class StoryBuildVerification
    {
        private static readonly List<string> results=new List<string>();
        private static string outputOverride;
        public static void RunExpandedBuild()
        {
            outputOverride="../QA_NarrativeRevision";
            Directory.CreateDirectory(Path.GetFullPath(outputOverride));
            Run();
        }
        public static void RunEventSkipBuild()
        {
            outputOverride="../QA_EventDialogueSkip";
            Run();
        }
        public static void RunSkipBuild()
        {
            outputOverride="../QA_StorySkip";
            Run();
        }
        public static void RunRivalCharacterBuild()
        {
            outputOverride="../QA_RivalDepth";
            Run();
        }
        private static void Check(bool condition,string name)
        {
            if(!condition) throw new InvalidOperationException(name);
            results.Add("PASS: "+name);
        }
        public static void Run()
        {
            string output=Path.GetFullPath(outputOverride ?? "../QA_Story");
            results.Clear();
            try
            {
                var profiles=AssetDatabase.FindAssets("t:NpcDefinition",new[]{"Assets/NPC/Data/Profiles"})
                    .Select(g=>AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(g)).CreateRuntimeSeed())
                    .Where(s=>!string.IsNullOrWhiteSpace(s.npcId)).ToArray();
                var ids=new HashSet<string>(profiles.Select(p=>p.npcId));
                Check(ids.Count==30 && profiles.Length==30,"No duplicate or empty registered NPC IDs");
                var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
                Check(sequence!=null && sequence.font!=null && sequence.playerStanding!=null,"Intro visual dependencies available in build");
                Check(sequence.beats.Count==136 && sequence.beats.Select(b=>b.id).Distinct().Count()==136,"All 136 beat IDs unique");
                int choiceCount=0;
                foreach(var beat in sequence.beats)
                {
                    Check(beat.background!=null && !string.IsNullOrWhiteSpace(beat.text),"Beat has background and text: "+beat.id);
                    if(beat.speaker==StorySpeaker.Npc) Check(ids.Contains(beat.npcId),"Beat NPC registered: "+beat.id);
                    Check(beat.choices.Count<=3,"Choice layout capacity: "+beat.id);
                    if(beat.choices.Count>0)Check(beat.skipDefaultChoice>=0 && beat.skipDefaultChoice<beat.choices.Count,"Whole skip has valid default answer: "+beat.id);
                    foreach(var choice in beat.choices)
                    {
                        choiceCount++;
                        Check(ids.Contains(choice.targetNpcId) && !string.IsNullOrWhiteSpace(choice.playerLine) && !string.IsNullOrWhiteSpace(choice.response),"Choice response and target: "+beat.id+"/"+choice.title);
                        Check(choice.memoryType==NpcMemoryType.Custom,"Intro promise/intent does not imply completed good faith: "+beat.id);
                    }
                }
                Check(choiceCount==9,"All nine conversation branches authored");
                Check(sequence.beats.Where(b=>b.goal!=StoryGoal.None).Select(b=>b.goal).SequenceEqual(new[]{StoryGoal.SelectCompany,StoryGoal.BuyShares,StoryGoal.ViewRepresentative,StoryGoal.ViewMemory,StoryGoal.EndDay,
                    StoryGoal.OpenEvent,StoryGoal.CompleteEvent,StoryGoal.ViewEventMemory,StoryGoal.ViewCompany,StoryGoal.CompareCompany,StoryGoal.ViewStake,StoryGoal.BuyMore,StoryGoal.SellShares,StoryGoal.ViewStake}),"All mandatory tutorial goals in approved order");
                foreach(var id in StoryContentBuilder.RivalIds)
                {
                    var rival=AssetDatabase.LoadAssetAtPath<RivalCompanyDefinition>("Assets/Story/Data/Rivals/"+id+".asset");
                    Check(rival!=null && rival.company.companyID==id,"Rival story linked to company: "+id);
                    Check(rival.members.Count==(id=="daeseong-industry"?4:3),"Full source roster registered: "+id);
                    Check(rival.members.All(m=>m.CreateRuntimeSeed().companyId==id),"Rival members belong to company: "+id);
                    Check(!string.IsNullOrEmpty(rival.groupRelationship) && !string.IsNullOrEmpty(rival.pressureStrategy) && !string.IsNullOrEmpty(rival.internalConflict) && !string.IsNullOrEmpty(rival.negotiationOpening),"Rival motives, pressure, conflicts, negotiation recorded: "+id);
                    foreach(var member in rival.members)
                    {
                        var seed=member.CreateRuntimeSeed();
                        Check(seed.npcId.StartsWith("npc-"+id+"-") && seed.memoryPreferences.Count>=3,"Canonical new NPC with individual Memory settings: "+seed.npcId);
                        Check(seed.profile.gameplayRoleSummary.Contains("선호:") && seed.profile.gameplayRoleSummary.Contains("기피:"),"Behavior preferences visible in NPC info: "+seed.npcId);
                    }
                }
                foreach(var guid in AssetDatabase.FindAssets("t:EventDefinition",new[]{"Assets/EventSystem/Data","Assets/Resources/Story"}))
                {
                    var definition=AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach(var choice in definition.getChoices())
                    {
                        var payload=choice.getNpcResult();
                        if(payload?.targetNpcIds==null) continue;
                        Check(payload.targetNpcIds.All(ids.Contains) && payload.targetNpcIds.Distinct().Count()==payload.targetNpcIds.Count,"Event targets registered without duplicates: "+definition.name+"/"+choice.getResultValue());
                        Check(payload.targetReactions==null || payload.targetReactions.All(r=>payload.targetNpcIds.Contains(r.targetNpcId)),"Event reactions refer to declared targets: "+definition.name);
                    }
                }
                File.WriteAllLines(Path.Combine(output,"content-results.txt"),results);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    target=BuildTarget.StandaloneWindows64,
                    locationPathName=Path.Combine(output,"Build/TakeOver.exe"), options=BuildOptions.Development
                });
                File.WriteAllText(Path.Combine(output,"build-results.txt"),report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\n"+
                    string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Warning).Select(m=>m.content)));
                if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed");
                if(Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch(Exception error)
            {
                File.WriteAllLines(Path.Combine(output,"content-results.txt"),results.Concat(new[]{"FAIL: "+error}));
                Debug.LogException(error); if(Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
