using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    /// <summary>원본 SO를 저장하지 않고 인물별 호불호·Inspector·런타임 기록을 검증한다.</summary>
    public static class NpcPreferenceDefinitionVerification
    {
        private static string Output => Path.GetFullPath("../QA_NpcIndividualSO");
        private static readonly List<string> results = new List<string>();
        private static int frames;
        private static NpcPreferenceInspectorProbe probe;
        private static NpcDefinition preview;
        private static bool guiFailed;

        [Serializable] private sealed class Wrapper { public NpcStateRegistry.NpcSeed seed; }
        private static NpcDefinition Definition(NpcStateRegistry.NpcSeed seed)
        {
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), definition);
            return definition;
        }

        private static NpcStateRegistry.NpcSeed Seed(string id, float multiplier, float bonus)
            => new NpcStateRegistry.NpcSeed
            {
                npcId = id, companyId = "external-network",
                actionPreferences = new List<NpcActionPreference>
                {
                    new NpcActionPreference { actionId = "GoodFaith", disposition = NpcActionDisposition.Likes,
                        effect = new NpcPreferenceEffect { mode = NpcPreferenceEffectMode.Custom, multiplier = multiplier,
                            affectedAxes = NpcPreferenceAxes.Trust, relationBonus = new NpcRelationDelta { trust = bonus } } }
                }
            };

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }
        private static void Near(float actual, float expected, string message)
            => Check(Mathf.Abs(actual - expected) < .001f, message + $" ({actual}/{expected})");

        [MenuItem("TakeOver/NPC/Verify Individual SO Settings")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            results.Clear(); guiFailed = false; frames = 0;
            try
            {
                VerifyDefinitions();
                preview = Definition(Seed("npc-broker-yoon-hajin", 2, 1));
                preview.name = "개별 호불호 편집 미리보기";
                var serialized = new SerializedObject(preview);
                serialized.FindProperty("seed").FindPropertyRelative("actionPreferences").GetArrayElementAtIndex(0).isExpanded = true;
                probe = ScriptableObject.CreateInstance<NpcPreferenceInspectorProbe>();
                probe.Bind(preview);
                probe.position = new Rect(80, 70, 650, 1000);
                probe.Show();
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Inspect;
            }
            catch (Exception error) { Fail(error); }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            guiFailed = true;
            results.Add("FAIL: Inspector: " + message);
        }

        private static void Inspect()
        {
            if (++frames < 20) { probe.Repaint(); return; }
            EditorApplication.update -= Inspect;
            Application.logMessageReceived -= OnLog;
            try
            {
                Check(!guiFailed && probe.DrawCount > 1, "Custom Inspector renders repeatedly without GUI errors");
                probe.Close(); UnityEngine.Object.DestroyImmediate(preview);
                File.WriteAllLines(Path.Combine(Output, "definition-results.txt"), results);
                NpcUnityVerification.Run();
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Fail(Exception error)
        {
            EditorApplication.update -= Inspect;
            Application.logMessageReceived -= OnLog;
            if (probe != null) probe.Close();
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            results.Add("FAIL: " + error);
            File.WriteAllLines(Path.Combine(Output, "definition-results.txt"), results);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void VerifyDefinitions()
        {
            var ids = AssetDatabase.FindAssets("t:NpcDefinition", new[] { "Assets/NPC/Data/Profiles" });
            var registered = 0;
            foreach (var guid in ids)
            {
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var seed = definition.CreateRuntimeSeed();
                if (string.IsNullOrWhiteSpace(seed.npcId)) continue;
                Check(NpcConfigurationDiagnostics.Definition(definition).Count == 0, "Existing SO valid: " + seed.npcId);
                Check(!seed.preferenceResponse.useCustomAxes, "Existing SO retains default axes: " + seed.npcId);
                registered++;
            }
            Check(registered == 14, "All 14 registered SOs retained");
            var a = Definition(Seed("npc-broker-yoon-hajin", 2, 1));
            var b = Definition(Seed("npc-investigative-reporter-jung-sua", 1.25f, -1));
            try
            {
                var input = new NpcRelationDelta { trust = 4, respect = 4, fear = -2, hostility = -4, dependency = 4 };
                var result = a.PreviewPreference("GoodFaith", NpcMemoryType.GoodFaith, input);
                Near(result.relationDelta.trust, 9, "SO A custom multiplier and bonus work with default response disabled");
                Near(b.PreviewPreference("GoodFaith", NpcMemoryType.GoodFaith, input).relationDelta.trust, 4, "SO B owns a different bonus for the same action");
                Near(result.relationDelta.respect, 4, "Unselected axis does not scale");
                Check(result.enabled && result.usedOverride && result.multiplier == 2, "Evaluation reports actual custom settings");
                VerifyListEditing(b);
                var log = result.CreateLog(input);
                Near(log.configuredBonus.trust, 1, "Effect log retains configured fixed bonus");
                var copy = a.CreateRuntimeSeed();
                copy.actionPreferences[0].effect.multiplier = 9;
                Near(a.PreviewPreference("GoodFaith", NpcMemoryType.GoodFaith, input).multiplier, 2, "Runtime copy is independent from SO nested custom settings");
                var seed = a.CreateRuntimeSeed();
                seed.memoryPreferences.Add(new NpcMemoryPreference { memoryType = NpcMemoryType.GoodFaith,
                    disposition = NpcActionDisposition.Likes, effect = new NpcPreferenceEffect
                    { mode = NpcPreferenceEffectMode.Custom, multiplier = 3, relationBonus = new NpcRelationDelta { trust = 2 } } });
                seed.preferenceResponse.enabled = true;
                seed.actionPreferences[0].effect.mode = NpcPreferenceEffectMode.Disabled;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                var disabled = a.PreviewPreference("GoodFaith", NpcMemoryType.GoodFaith, input);
                Near(disabled.relationDelta.trust, 4, "Disabled action bonus blocks both default and Memory bonus");
                Check(!disabled.enabled, "Disabled action is logged as disabled");
                seed.actionPreferences[0].disposition = NpcActionDisposition.Unspecified;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                Near(a.PreviewPreference("GoodFaith", NpcMemoryType.GoodFaith, input).relationDelta.trust, 14, "Unspecified action falls back to custom Memory bonus exactly once");
                seed.memoryPreferences.Clear();
                seed.actionPreferences[0].disposition = NpcActionDisposition.Likes;
                var effect = seed.actionPreferences[0].effect;
                effect.mode = NpcPreferenceEffectMode.Custom; effect.multiplier = 2;
                effect.affectedAxes = NpcPreferenceAxes.Fear | NpcPreferenceAxes.Dependency;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                var optional = a.PreviewPreference("GoodFaith", NpcMemoryType.Custom, input);
                Near(optional.relationDelta.fear, -4, "Optional fear axis scales a decrease on liked action");
                Near(optional.relationDelta.dependency, 8, "Optional dependency axis scales an increase on liked action");
                effect.affectedAxes = NpcPreferenceAxes.None; effect.relationBonus.trust = 3;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                Near(a.PreviewPreference("GoodFaith", NpcMemoryType.Custom, input).relationDelta.trust, 7, "No axes selected supports fixed-bonus-only effects");
                effect.mode = NpcPreferenceEffectMode.NpcDefault;
                seed.preferenceResponse.useCustomAxes = true; seed.preferenceResponse.affectedAxes = NpcPreferenceAxes.Respect;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                var defaults = a.PreviewPreference("GoodFaith", NpcMemoryType.Custom, input);
                Near(defaults.relationDelta.trust, 4, "NPC default axis selection leaves trust unchanged");
                Near(defaults.relationDelta.respect, 6, "NPC default axis selection scales respect");
                effect.mode = NpcPreferenceEffectMode.Custom; effect.multiplier = .5f;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Wrapper { seed = seed }), a);
                Check(NpcConfigurationDiagnostics.Definition(a).Count != 0, "Inspector reports invalid custom multiplier");
                var rejected = false;
                try { a.PreviewPreference("GoodFaith", NpcMemoryType.Custom, input); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Runtime rejects invalid custom multiplier");
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        internal static void VerifyRuntime()
        {
            Directory.CreateDirectory(Output);
            var start = results.Count;
            var go = new GameObject("Individual NPC SO runtime verification"); go.SetActive(false);
            try
            {
                var registry = go.AddComponent<NpcStateRegistry>();
                var seeds = new List<NpcStateRegistry.NpcSeed>
                { Seed("npc-broker-yoon-hajin", 2, 1), Seed("npc-investigative-reporter-jung-sua", 1.25f, -1) };
                typeof(NpcStateRegistry).GetField("initialNpcs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, seeds);
                var memory = go.AddComponent<NpcMemoryService>();
                var clock = go.AddComponent<GameTurnClock>();
                var channel = go.AddComponent<NpcEventChannel>();
                go.AddComponent<NpcEventProcessor>();
                var save = go.AddComponent<NpcSaveService>();
                go.SetActive(true);
                save.Configure(registry, clock);
                var serialized = new SerializedObject(save);
                serialized.FindProperty("fileName").stringValue = Path.Combine(Output, "individual-save.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var payload = new NpcEventPayload { eventId = "individual-so-verification", actionId = "GoodFaith",
                    targetNpcIds = new List<string> { seeds[0].npcId, seeds[1].npcId },
                    relationDelta = new NpcRelationDelta { trust = 4 }, memoryType = NpcMemoryType.GoodFaith };
                channel.Publish(payload); channel.Publish(payload);
                Check(registry.TryGet(seeds[0].npcId, out var first) && registry.TryGet(seeds[1].npcId, out _), "Separate runtime states initialized");
                registry.TryGet(seeds[1].npcId, out var second);
                Near(first.relation.trust, 9, "Actual event applies NPC A custom bonus once");
                Near(second.relation.trust, 4, "Actual event applies NPC B custom bonus once");
                Check(first.memories.Count == 1 && second.memories.Count == 1, "Repeated event creates one Memory per NPC");
                Near(first.memories[0].effectLog.multiplier, 2, "Runtime log records actual custom multiplier");
                Check(NpcFeatureTestScene.FormatLastMemoryLog(first.memories[0]).Contains("개별 설정"), "Memory display identifies custom bonus");
                Check(save.TrySave(), "Custom bonus memory saved: " + save.LastError);
                memory.ResetState(first); memory.ResetState(second);
                Check(save.Load(), "Custom bonus memory loaded: " + save.LastError);
                Near(first.relation.trust, 9, "Save/load retains relation without reapplying custom bonus");
                Near(first.memories[0].effectLog.configuredBonus.trust, 1, "Save/load retains historical configured bonus");
                var cloned = first.memories[0].Clone(); cloned.effectLog.configuredBonus.trust = 99;
                Near(first.memories[0].effectLog.configuredBonus.trust, 1, "Copied custom log does not mutate original");
                Check(seeds[0].actionPreferences[0].effect.multiplier == 2, "Runtime processing leaves profile rules unchanged");
                seeds[1].actionPreferences[0].effect.multiplier = float.NaN;
                registry.ConfigureActionPreferences(seeds[1].npcId, seeds[1].actionPreferences);
                payload.eventId = "individual-so-invalid-verification";
                var rejected = false;
                try { channel.Publish(payload); } catch (ArgumentException) { rejected = true; }
                Check(rejected && first.memories.Count == 1 && second.memories.Count == 1,
                    "Invalid later target rejects event before either target is changed");
                File.WriteAllLines(Path.Combine(Output, "runtime-results.txt"), results.GetRange(start, results.Count - start));
            }
            finally { UnityEngine.Object.Destroy(go); }
        }

        private static void VerifyListEditing(NpcDefinition definition)
        {
            var editor = UnityEditor.Editor.CreateEditor(definition);
            var original = JsonUtility.ToJson(definition);
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var list = (UnityEditorInternal.ReorderableList)editor.GetType().GetField("actions", flags).GetValue(editor);
                var group = Undo.GetCurrentGroup();
                Undo.RecordObject(definition, "Verify NPC list add");
                list.onAddCallback(list);
                editor.serializedObject.ApplyModifiedProperties();
                Undo.FlushUndoRecordObjects();
                var seed = definition.CreateRuntimeSeed();
                Check(seed.actionPreferences.Count == 2 && seed.actionPreferences[1].actionId == "",
                    "Inspector add creates a blank action rather than duplicating the previous key");
                Check(seed.actionPreferences[1].effect.mode == NpcPreferenceEffectMode.NpcDefault
                    && seed.actionPreferences[1].effect.multiplier == 1.5f && seed.actionPreferences[1].effect.relationBonus.trust == 0,
                    "Inspector add resets custom bonus values");
                Undo.CollapseUndoOperations(group); Undo.PerformUndo();
                Check(JsonUtility.ToJson(definition) == original, "Inspector list add supports Undo");
                editor.serializedObject.Update();
                var memories = (UnityEditorInternal.ReorderableList)editor.GetType().GetField("memories", flags).GetValue(editor);
                memories.onAddCallback(memories); memories.onAddCallback(memories);
                editor.serializedObject.ApplyModifiedProperties();
                seed = definition.CreateRuntimeSeed();
                Check(seed.memoryPreferences.Count == 2 && seed.memoryPreferences[0].memoryType != seed.memoryPreferences[1].memoryType,
                    "Inspector add picks distinct unused Memory types");
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(original, definition);
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }
    }

    internal sealed class NpcPreferenceInspectorProbe : EditorWindow
    {
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        internal int DrawCount { get; private set; }
        internal void Bind(NpcDefinition definition)
        {
            inspector = UnityEditor.Editor.CreateEditor(definition);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            inspector.GetType().GetField("showPreview", flags).SetValue(inspector, true);
            inspector.GetType().GetField("showProfile", flags).SetValue(inspector, false);
        }
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
            DrawCount++;
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    }
}
