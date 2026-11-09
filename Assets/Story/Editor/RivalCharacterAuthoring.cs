using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.NPC;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    /// <summary>공개 소개와 제작용 서사 설계만 갱신한다. NPC·회사 수치, ID, 이미지는 재생성하지 않는다.</summary>
    public static class RivalCharacterAuthoring
    {
        [Serializable] private sealed class NpcDraft { public string file; public NpcStateRegistry.NpcSeed seed; public NpcStoryDesign storyDesign; }
        [Serializable] private sealed class CompanyDraft
        {
            public string companyID,groupRelationship,pressureStrategy,internalConflict,negotiationOpening;
            public string loyaltyStructure,cooperationBoundaries,publicSummary;
            public RivalAffiliationKind affiliationKind;
        }
        [Serializable] private sealed class Draft { public List<NpcDraft> npcs; public List<CompanyDraft> companies; }
        [Serializable] public sealed class NpcInvariant { public string file,id,companyId,numericSeed; }
        [Serializable] public sealed class CompanyInvariant { public string companyId,numericDefinition; }
        [Serializable] public sealed class Invariants
        { public List<NpcInvariant> npcs=new List<NpcInvariant>(); public List<CompanyInvariant> companies=new List<CompanyInvariant>(); }

        internal static string SeedWithoutPublicText(NpcDefinition definition)
        {
            var seed=definition.CreateRuntimeSeed();
            seed.profile.personalitySummary="";seed.profile.gameplayRoleSummary="";
            return JsonUtility.ToJson(seed);
        }
        internal static string CompanyWithoutDescription(CompanyDefinition definition)
        {
            var copy=Object.Instantiate(definition);
            try{copy.name=definition.name;copy.description="";return JsonUtility.ToJson(copy);}
            finally{Object.DestroyImmediate(copy);}
        }

        [MenuItem("TakeOver/Story/Apply Authored Rival Character Text")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play 모드 종료 후 실행하세요.");
            string output=Path.GetFullPath("../QA_RivalDepth");Directory.CreateDirectory(output);
            var draft=JsonUtility.FromJson<Draft>(File.ReadAllText("Assets/Story/Editor/AuthoringData.json"));
            var baseline=new Invariants();var checks=new List<string>();
            foreach(var npc in draft.npcs)
            {
                var asset=AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/NPC/Data/Profiles/"+npc.file+".asset");
                if(asset==null || npc.storyDesign==null)throw new InvalidOperationException("NPC 서사 데이터 누락: "+npc.file);
                var seed=asset.CreateRuntimeSeed();
                baseline.npcs.Add(new NpcInvariant{file=npc.file,id=seed.npcId,companyId=seed.companyId,numericSeed=SeedWithoutPublicText(asset)});
                var so=new SerializedObject(asset);var profile=so.FindProperty("seed").FindPropertyRelative("profile");
                profile.FindPropertyRelative("personalitySummary").stringValue=npc.seed.profile.personalitySummary;
                profile.FindPropertyRelative("gameplayRoleSummary").stringValue=npc.seed.profile.gameplayRoleSummary;
                var story=so.FindProperty("storyDesign");
                story.FindPropertyRelative("stance").intValue=(int)npc.storyDesign.stance;
                foreach(var field in typeof(NpcStoryDesign).GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType==typeof(string)))
                    story.FindPropertyRelative(field.Name).stringValue=(string)field.GetValue(npc.storyDesign);
                so.ApplyModifiedPropertiesWithoutUndo();
                if(SeedWithoutPublicText(asset)!=baseline.npcs.Last().numericSeed)throw new Exception("NPC 수치/ID/이미지 변경: "+npc.file);
                checks.Add("PASS: public text and separate story design applied without numeric changes: "+npc.file);
            }
            foreach(var company in draft.companies)
            {
                var rival=AssetDatabase.LoadAssetAtPath<RivalCompanyDefinition>("Assets/Story/Data/Rivals/"+company.companyID+".asset");
                baseline.companies.Add(new CompanyInvariant{companyId=company.companyID,numericDefinition=CompanyWithoutDescription(rival.company)});
                var so=new SerializedObject(rival);
                foreach(var field in typeof(CompanyDraft).GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType==typeof(string) && f.Name!="companyID" && f.Name!="publicSummary"))
                    so.FindProperty(field.Name).stringValue=(string)field.GetValue(company);
                so.FindProperty("affiliationKind").intValue=(int)company.affiliationKind;so.ApplyModifiedPropertiesWithoutUndo();
                var definitionSo=new SerializedObject(rival.company);definitionSo.FindProperty("description").stringValue=company.publicSummary;definitionSo.ApplyModifiedPropertiesWithoutUndo();
                if(CompanyWithoutDescription(rival.company)!=baseline.companies.Last().numericDefinition)throw new Exception("회사 수치 변경: "+company.companyID);
                checks.Add("PASS: company affiliation and policy applied without financial changes: "+company.companyID);
            }
            File.WriteAllText(Path.Combine(output,"asset-invariants.json"),JsonUtility.ToJson(baseline,true));
            File.WriteAllLines(Path.Combine(output,"apply-results.txt"),checks);
            AssetDatabase.SaveAssets();
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
