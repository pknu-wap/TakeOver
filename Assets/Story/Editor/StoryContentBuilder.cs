using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TakeOver.Story.Editor
{
    /// <summary>최초 제작/명시적 재생성 전용. 평소 대본과 NPC는 생성된 SO에서 편집한다.</summary>
    public static class StoryContentBuilder
    {
        [Serializable] private class NpcDraft { public string file; public NpcStateRegistry.NpcSeed seed; public NpcStoryDesign storyDesign; }
        [Serializable] private class CompanyDraft
        {
            public string companyID, companyName, industry, coreValue;
            public string groupRelationship, pressureStrategy, internalConflict, negotiationOpening;
            public RivalAffiliationKind affiliationKind;
            public string loyaltyStructure, cooperationBoundaries, publicSummary;
            public float[] financials;
        }
        [Serializable] private class Draft { public List<NpcDraft> npcs; public List<CompanyDraft> companies; public List<StoryBeat> beats; }
        [Serializable] private class BeatPath { public string backgroundPath; }
        [Serializable] private class Paths { public List<BeatPath> beats; }
        public static readonly string[] RivalIds = { "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech" };

        [MenuItem("TakeOver/Story/Rebuild Approved Intro and Rivals")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드 종료 후 실행하세요.");
            var json = File.ReadAllText("Assets/Story/Editor/AuthoringData.json");
            var draft = JsonUtility.FromJson<Draft>(json);
            var paths = JsonUtility.FromJson<Paths>(json);
            Directory.CreateDirectory("Assets/Story/Data/Rivals");
            AssetDatabase.Refresh();
            foreach (var path in new[] { "Assets/Story/Art/FamilyOffice.png", "Assets/Story/Art/HotelLounge.png", "Assets/Story/Art/ConstructionSite.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            var definitions = new List<NpcDefinition>();
            foreach (var npc in draft.npcs)
            {
                var path = "Assets/NPC/Data/Profiles/" + npc.file + ".asset";
                var definition = LoadOrCreate<NpcDefinition>(path);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(npc), definition);
                definition.name = npc.file; EditorUtility.SetDirty(definition); definitions.Add(definition);
            }
            var companies = new List<CompanyDefinition>();
            foreach (var company in draft.companies)
            {
                var definition = LoadOrCreate<CompanyDefinition>("Assets/ShareTrading/Data/" + company.companyID + ".asset");
                definition.companyName = company.companyName; definition.companyID = company.companyID;
                definition.industry = company.industry; definition.coreValue = company.coreValue;
                definition.description = string.IsNullOrEmpty(company.publicSummary) ? company.groupRelationship+"\n"+company.pressureStrategy+"\n"+company.internalConflict : company.publicSummary;
                definition.totalShares = 10000;
                var f = company.financials;
                definition.initialBaseCompanyValue = f[0]; definition.initialRevenue = f[1]; definition.initialCost = f[2];
                definition.initialCash = f[3]; definition.initialDebt = f[4];
                definition.initialTangibleAssets = f[5]; definition.initialIntangibleAssets = f[6];
                definition.initialPlayerShares = 0;
                EditorUtility.SetDirty(definition); companies.Add(definition);
                var rival = LoadOrCreate<RivalCompanyDefinition>("Assets/Story/Data/Rivals/"+company.companyID+".asset");
                rival.company = definition;
                rival.members = definitions.Where(n => n.CreateRuntimeSeed().companyId == company.companyID).ToList();
                rival.groupRelationship = company.groupRelationship; rival.pressureStrategy = company.pressureStrategy;
                rival.internalConflict = company.internalConflict; rival.negotiationOpening = company.negotiationOpening;
                rival.affiliationKind = company.affiliationKind; rival.loyaltyStructure = company.loyaltyStructure;
                rival.cooperationBoundaries = company.cooperationBoundaries;
                EditorUtility.SetDirty(rival);
            }
            EditPrefab("Assets/NPC/Prefabs/NpcLogic.prefab", root =>
            {
                var so = new SerializedObject(root.GetComponent<NpcStateRegistry>());
                AppendObjects(so.FindProperty("npcDefinitions"), definitions); so.ApplyModifiedPropertiesWithoutUndo();
            });
            EditPrefab("Assets/ShareTrading/Prefabs/ShareTradingLogic.prefab", root =>
            {
                var registry = root.GetComponentInChildren<Companies>(true);
                var so = new SerializedObject(registry);
                var list = so.FindProperty("companies");
                var model = registry.CompanyList.First();
                foreach (var definition in companies)
                {
                    var instance = root.GetComponentsInChildren<Company>(true).FirstOrDefault(c => c.definition == definition);
                    if (instance == null)
                    {
                        var copy = Object.Instantiate(model.gameObject, model.transform.parent);
                        copy.name = definition.companyID;
                        instance = copy.GetComponent<Company>();
                        var companySo = new SerializedObject(instance);
                        companySo.FindProperty("companyDefinition").objectReferenceValue = definition;
                        companySo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AppendObjects(list, new[] { instance });
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            EditPrefab("Assets/ShareTrading/Prefabs/CompanyBrowserUI.prefab", root =>
            {
                var map = root.GetComponentInChildren<CompanyMapView>(true);
                var so = new SerializedObject(map); var nodes = so.FindProperty("nodeSettings");
                var locations = new[] { new Vector2(-550,320), new Vector2(560,300), new Vector2(0,380), new Vector2(-520,-320), new Vector2(520,-300) };
                for (int i=0;i<RivalIds.Length;i++)
                {
                    SerializedProperty node=null;
                    for(int j=0;j<nodes.arraySize;j++) if(nodes.GetArrayElementAtIndex(j).FindPropertyRelative("companyID").stringValue==RivalIds[i]) node=nodes.GetArrayElementAtIndex(j);
                    if(node==null) { nodes.InsertArrayElementAtIndex(nodes.arraySize); node=nodes.GetArrayElementAtIndex(nodes.arraySize-1); }
                    node.FindPropertyRelative("companyID").stringValue=RivalIds[i]; node.FindPropertyRelative("position").vector2Value=locations[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            ConnectCompanyEvents(definitions);
            var sequence = LoadOrCreate<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            sequence.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/EventSystem/Fonts/NanumGothic-Regular.ttf");
            sequence.playerStanding = Sprite("Assets/NPC/Art/Standing/Player_Standing.png");
            sequence.playerDimmed = Sprite("Assets/NPC/Art/Standing/Player_Dim.png");
            sequence.beats = draft.beats;
            for(int i=0;i<sequence.beats.Count;i++) sequence.beats[i].background=Sprite(paths.beats[i].backgroundPath);
            EditorUtility.SetDirty(sequence);
            var intro = new GameObject("Intro",typeof(RectTransform),typeof(StoryDirector));
            var introSo=new SerializedObject(intro.GetComponent<StoryDirector>());
            introSo.FindProperty("sequence").objectReferenceValue=sequence; introSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(intro,"Assets/Resources/Story/Intro.prefab"); Object.DestroyImmediate(intro);
            AssetDatabase.SaveAssets();
            File.WriteAllText("../QA_Story/content-build.txt", "5 rival companies / 16 new NPCs / "+sequence.beats.Count+" beats installed.\n");
            Debug.Log("Story content installed. Edit IntroSequence and NPC SOs for subsequent authoring.");
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void ConnectCompanyEvents(List<NpcDefinition> definitions)
        {
            var seeds = definitions.Select(d=>d.CreateRuntimeSeed()).ToList();
            var ceos = seeds.Where(s=>s.position==NpcPosition.CEO).ToList();
            var contactIds = new[] { "Song_Hyeonseok", "Choi_Eunsu", "Kang_Minji", "Kim_Dongseok", "Jung_Harin" };
            var contacts=contactIds.Select(n=>AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/NPC/Data/Profiles/"+n+".asset").CreateRuntimeSeed()).ToList();
            foreach(var guid in AssetDatabase.FindAssets("t:EventDefinition",new[]{"Assets/EventSystem/Data"}))
            {
                var definition=AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(!definition.hasCompanyConditions()) continue;
                bool restructuring=definition.getId()=="restructuring_01";
                if(restructuring)
                {
                    var so=new SerializedObject(definition); var field=so.FindProperty("restructuringSpeakers");
                    if(field==null) throw new InvalidOperationException("Restructuring speaker field missing");
                    foreach(var contact in contacts)
                    {
                        bool found=false;
                        for(int i=0;i<field.arraySize;i++) if(field.GetArrayElementAtIndex(i).FindPropertyRelative("companyId").stringValue==contact.companyId) found=true;
                        if(found) continue;
                        field.InsertArrayElementAtIndex(field.arraySize); var entry=field.GetArrayElementAtIndex(field.arraySize-1);
                        entry.FindPropertyRelative("companyId").stringValue=contact.companyId; entry.FindPropertyRelative("npcId").stringValue=contact.npcId;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach(var choice in definition.getChoices())
                {
                    var payload=choice.getNpcResult();
                    if(payload?.targetNpcIds==null || payload.targetNpcIds.Count==0) continue;
                    var targets=restructuring?contacts:ceos;
                    // 외부 인물의 단독 사건은 회사별 임원 반응으로 바꾸지 않는다.
                    if(!restructuring && !payload.targetNpcIds.Any(id=>id.Contains("-ceo-"))) continue;
                    foreach(var target in targets)
                    {
                        if(payload.targetNpcIds.Contains(target.npcId)) continue;
                        payload.targetNpcIds.Add(target.npcId);
                        // 공통 결과를 기본으로 사용한다. 인물 차이는 각 SO의 Memory 반응에서 계산한다.
                        var sample=payload.targetReactions?.FirstOrDefault();
                        if(sample!=null)
                        {
                            var reaction=JsonUtility.FromJson<NpcEventReaction>(JsonUtility.ToJson(sample));
                            reaction.targetNpcId=target.npcId;
                            reaction.interpretation=target.profile.displayName+"이(가) 자사에서 확정된 경영 선택을 기억한다.";
                            payload.targetReactions.Add(reaction);
                        }
                    }
                }
                EditorUtility.SetDirty(definition);
            }
        }

        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        private static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null) { asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); }
            return asset;
        }
        private static void AppendObjects<T>(SerializedProperty list, IEnumerable<T> objects) where T:Object
        {
            foreach(var obj in objects)
            {
                bool found=false; for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==obj) found=true;
                if(found) continue;
                int index=list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue=obj;
            }
        }
        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
