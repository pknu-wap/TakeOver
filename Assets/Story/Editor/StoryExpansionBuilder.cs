using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using UnityEditor;
using UnityEngine;

namespace TakeOver.Story.Editor
{
    public static class StoryExpansionBuilder
    {
        private static bool verifyAfterBuild;
        public static void BuildAndVerify() { verifyAfterBuild = true; Build(); global::CampaignIntegrationVerification.Run(); }
        [MenuItem("TakeOver/Story/Build Early Campaign and Repeatable Events")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드 종료 후 실행하세요.");
            var data = JObject.Parse(File.ReadAllText("Assets/Story/Editor/ExpansionData.json"));
            Directory.CreateDirectory("Assets/EventSystem/Data/Repeatable");
            Directory.CreateDirectory("Assets/Story/Data/Campaign");
            Directory.CreateDirectory("Assets/Story/Data/LegacyCampaign");
            Directory.CreateDirectory("Assets/Resources/Events");
            AssetDatabase.Refresh();
            var originals = AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data" })
                .Where(guid => !AssetDatabase.GUIDToAssetPath(guid).StartsWith("Assets/EventSystem/Data/Repeatable/", StringComparison.Ordinal))
                .Select(guid => AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            var general = new List<EventDefinition> { originals.Single(item => item.getId() == "greenmail_01") };
            foreach (JObject draft in data["general"])
            {
                string id = (string)draft["eventId"];
                var item = originals.FirstOrDefault(definition => definition.getId() == id) ?? Create<EventDefinition>("Assets/EventSystem/Data/Repeatable/" + id + ".asset");
                JsonUtility.FromJsonOverwrite(draft.ToString(), item);
                AddArt(item, originals);
                EditorUtility.SetDirty(item); general.Add(item);
            }
            var generalDatabase = Create<GeneralEventDatabase>("Assets/Resources/Events/GeneralEventDatabase.asset");
            SetList(generalDatabase, general);
            var storyDatabase = AssetDatabase.LoadAssetAtPath<StoryEventDatabase>("Assets/Resources/Story/StoryEventDatabase.asset");
            var legacyStories = new List<EventDefinition> { originals.Single(item => item.getId() == EventSystemController.MandatoryEventId) };
            foreach (JObject draft in data["legacyCampaign"])
            {
                var item = Create<EventDefinition>("Assets/Story/Data/LegacyCampaign/" + (string)draft["eventId"] + ".asset");
                JsonUtility.FromJsonOverwrite(draft.ToString(), item); AddArt(item, originals); EditorUtility.SetDirty(item); legacyStories.Add(item);
            }
            SetList(Create<StoryEventDatabase>("Assets/Resources/Story/LegacyStoryEventDatabase.asset"), legacyStories);
            File.WriteAllText("Assets/Resources/Story/EarlyCampaign.json", data["plan"].ToString());
            var stories = new List<EventDefinition> { originals.Single(item => item.getId() == EventSystemController.MandatoryEventId) };
            foreach (JObject draft in data["campaign"])
            {
                var item = Create<EventDefinition>("Assets/Story/Data/Campaign/" + (string)draft["eventId"] + ".asset");
                JsonUtility.FromJsonOverwrite(draft.ToString(), item);
                if (item.getChoices().Count == 0) throw new InvalidDataException("스토리 선택지가 없습니다: " + item.getId());
                AddArt(item, originals); EditorUtility.SetDirty(item); stories.Add(item);
            }
            SetList(storyDatabase, stories);
            var intro = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            var source = JObject.Parse(File.ReadAllText("Assets/Story/Editor/AuthoringData.json"));
            intro.beats = JsonUtility.FromJson<BeatList>(source.ToString()).beats;
            for (int i = 0; i < intro.beats.Count; i++)
                intro.beats[i].background = AssetDatabase.LoadAssetAtPath<Sprite>((string)source["beats"][i]["backgroundPath"]);
            EditorUtility.SetDirty(intro);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory("../QA_Campaign");
            File.WriteAllText("../QA_Campaign/content-build.txt", $"General {general.Count}; Story {stories.Count}; Intro {intro.beats.Count}\n");
            Debug.Log($"Campaign content installed: {general.Count} general / {stories.Count} story / {intro.beats.Count} intro beats");
            if (Application.isBatchMode && !verifyAfterBuild) EditorApplication.Exit(0);
        }
        [Serializable] private sealed class BeatList { public List<StoryBeat> beats; }
        private static T Create<T>(string path) where T : ScriptableObject
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result == null) { result = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(result, path); }
            return result;
        }
        private static void SetList(UnityEngine.Object database, List<EventDefinition> definitions)
        {
            var serialized = new SerializedObject(database);
            var list = serialized.FindProperty("events"); list.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(database);
        }
        private static void AddArt(EventDefinition item, List<EventDefinition> originals)
        {
            string npc = item.getSpeakerNpcId();
            if (string.IsNullOrEmpty(npc)) return;
            var source = originals.FirstOrDefault(definition => definition.getSpeakerNpcId() == npc);
            if (source == null)
                source = originals.FirstOrDefault(definition =>
                {
                    var list = new SerializedObject(definition).FindProperty("npcArts");
                    for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("npcId").stringValue == npc) return true;
                    return false;
                });
            var serialized = new SerializedObject(item);
            if (source == null)
                source = originals.Single(definition => definition.getId() == "inside_info_01");
            serialized.FindProperty("cardImage").objectReferenceValue = source.getCardImageFor(npc);
            serialized.FindProperty("image").objectReferenceValue = source.getImageFor(npc);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
