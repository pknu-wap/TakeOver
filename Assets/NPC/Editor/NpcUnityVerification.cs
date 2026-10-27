using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    /// <summary>실제 Unity Play 모드에서 NPC 프리팹·선호·저장·정보 UI를 검증한다. 씬과 에셋은 저장하지 않는다.</summary>
    [InitializeOnLoad]
    public static class NpcUnityVerification
    {
        private const string Key = "TakeOver.NPC.Verification";
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../psm/unity-verification"));
        private static readonly List<string> results = new List<string>();
        private static double readyAt;
        private static int phase;
        private static bool failed;
        private static bool started;
        private static NpcFeatureTestScene ui;
        private static NpcFeatureTestHarness harness;

        static NpcUnityVerification()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
            readyAt = EditorApplication.timeSinceStartup + 5;
        }

        [MenuItem("TakeOver/NPC/Verify in Unity Play Mode")]
        public static void Run()
        {
            started = false;
            failed = false;
            phase = 0;
            results.Clear();
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + ".sceneHash", Hash("Assets/Scenes/Game.unity"));
            SessionState.SetBool(Key + ".failed", false);
            SessionState.SetString(Key + ".results", "");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NPC/Prefabs/NpcLogic.prefab");
                Require(prefab != null, "NPC prefab imported");
                var registry = prefab.GetComponent<NpcStateRegistry>();
                var errors = new List<string>();
                var ids = NpcConfigurationDiagnostics.RegistryIds(registry, errors);
                Require(errors.Count == 0, "Inspector diagnostics: " + string.Join("; ", errors));
                Require(ids.Count == 14, "Prefab has 14 unique NPC definitions");
                var definitions = new SerializedObject(registry).FindProperty("npcDefinitions");
                var ceos = 0;
                for (var i = 0; i < definitions.arraySize; i++)
                {
                    var seed = ((NpcDefinition)definitions.GetArrayElementAtIndex(i).objectReferenceValue).CreateRuntimeSeed();
                    Require(new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry", "external-network" }.Contains(seed.companyId)
                        && (seed.companyId == "external-network" || seed.npcId.StartsWith("npc-" + seed.companyId + "-", StringComparison.Ordinal)), "Canonical ID: " + seed.npcId);
                    if (seed.position == NpcPosition.CEO) ceos++;
                }
                Require(ceos == 4, "Exactly four CEO definitions");
                VerifyProjectIdReferences();
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
                var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                gameView.Show();
                gameView.Focus();
                EditorApplication.playModeStateChanged -= OnPlayMode;
                EditorApplication.playModeStateChanged += OnPlayMode;
                EditorApplication.update -= Update;
                EditorApplication.update += Update;
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                readyAt = EditorApplication.timeSinceStartup + 5;
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(error); Finish(); }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            failed = true;
            SessionState.SetBool(Key + ".failed", true);
            Append("ERROR: " + message + "\n" + stack);
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // 실행 중에만 NPC 프리팹과 렌더링 카메라를 남긴다. 씬 파일이나 타 팀 에셋은 저장하지 않는다.
                var npc = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestHarness>();
                if (npc != null)
                    foreach (var root in npc.gameObject.scene.GetRootGameObjects()) KeepNpcAndCamera(root.transform, npc.transform);
                readyAt = EditorApplication.timeSinceStartup + 4; phase = 0;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false)) Finish();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
            try
            {
                if (phase == 0)
                {
                    if (started) return;
                    started = true;
                    VerifyRuntime();
                    ui.ShowTestUI = true;
                    ui.ShowNpcInfo = true;
                    phase = 1;
                    readyAt = EditorApplication.timeSinceStartup + 3;
                }
                else if (phase == 1)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, "npc-info.png"));
                    Require(ui.ShowTestUI && ui.ShowNpcInfo, "NPC info view enabled in live Game scene");
                    phase = 2;
                    readyAt = EditorApplication.timeSinceStartup + 3;
                }
                else if (phase == 2)
                {
                    harness.SelectNpcProfile("npc-haneul-food-ceo-kimharin");
                    Require(harness.SelectedProfile.profile.displayName == "김하린", "Info view switches to selected NPC");
                    ui.ShowNpcInfo = false;
                    phase = 3;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                }
                else if (phase == 3)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, "npc-test.png"));
                    harness.SelectNpcProfile("npc-mirae-construction-cfo-seojiah");
                    harness.ResetSelectedNpcState();
                    harness.PreviewPreferenceAction(0);
                    typeof(NpcFeatureTestScene).GetField("bodyTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(ui, 1);
                    phase = 4;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                }
                else if (phase == 4)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, "npc-preferences.png"));
                    phase = 5;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                }
                else
                {
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Fail(error); EditorApplication.isPlaying = false; }
        }

        private static void VerifyRuntime()
        {
            harness = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestHarness>();
            ui = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestScene>();
            Require(harness != null && ui != null, "NPC runtime exists in Game scene");
            var registry = harness.GetComponent<NpcStateRegistry>();
            Require(registry.Profiles.Count == 14 && registry.States.Count == 14, "All 14 NPCs initialized in Play mode");
            // 타 팀은 비활성 상태로 유지하고 초기화된 정식 ID 인덱스만 조회한다.
            var trading = UnityEngine.Object.FindFirstObjectByType<Companies>(FindObjectsInactive.Include);
            var market = UnityEngine.Object.FindFirstObjectByType<TmpMarketData>(FindObjectsInactive.Include);
            Require(trading != null && market != null, "Trading ID index exists for reference verification");
            foreach (var id in new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" })
                Require(trading.getCompany(id) != null && market.tryGetCompany(id, out var company)
                    && company.definition.companyID == id, "Runtime company lookup: " + id);
            foreach (var company in new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" })
                Require(registry.TryFindByPosition(company, NpcPosition.CEO, out var ceo) && ceo.position == NpcPosition.CEO,
                    "CEO lookup: " + company);
            const string seo = "npc-mirae-construction-cfo-seojiah";
            Require(harness.SelectNpcProfile(seo), "Select SEO JIAH");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(0);
            Near(harness.RuntimeState.relation.trust, 6, "Liked action amplifies trust +4 to +6");
            Near(harness.LastRecordedMemory.effectLog.beforePreference.trust, 4, "Last-event log keeps pre-preference base");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 2, "Last-event log records liked bonus +2");
            Require(NpcFeatureTestScene.FormatLastMemoryLog(harness.LastRecordedMemory).Contains("선호 ×1.5"), "Footer log identifies applied preference multiplier");
            Near(harness.RuntimeState.relation.hostility, 0, "Hostility reduction stops at zero");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(1);
            Near(harness.RuntimeState.relation.trust, -6, "Disliked action amplifies trust -4 to -6");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, -2, "Last-event log records disliked extra penalty -2");
            Near(harness.RuntimeState.relation.hostility, 6, "Disliked action amplifies hostility increase");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(2);
            Near(harness.RuntimeState.relation.trust, 4, "Unspecified action keeps base effect");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 0, "Neutral log has no preference bonus");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(3);
            Near(harness.RuntimeState.relation.trust, 6, "Result Memory preference works without actionId");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(4);
            Near(harness.RuntimeState.relation.trust, -6, "Disliked result Memory amplifies negative effect");
            var mixed = new NpcRelationDelta { trust = 4, respect = -4, hostility = 4, fear = 4, dependency = 4 };
            var result = registry.EvaluatePreference(seo, "ClearFundingPlan", NpcMemoryType.ContractBreach, mixed, out var disposition);
            Require(disposition == NpcActionDisposition.Likes, "Action preference takes priority over conflicting Memory preference");
            Near(result.trust, 6, "Mixed liked effect: favorable axis amplified");
            Near(result.respect, -4, "Mixed liked effect: unfavorable axis unchanged");
            Near(result.hostility, 4, "Mixed liked effect: hostility increase unchanged");
            Near(result.fear, 4, "Fear is not treated as affection");
            Near(result.dependency, 4, "Dependency is not treated as affection");
            var channel = harness.GetComponent<NpcEventChannel>();
            var payload = new NpcEventPayload { eventId = "unity-duplicate-check", actionId = "ClearFundingPlan",
                targetNpcIds = new List<string> { seo }, memoryType = NpcMemoryType.Custom, relationDelta = new NpcRelationDelta { trust = 4 } };
            harness.ResetSelectedNpcState();
            channel.Publish(payload);
            channel.Publish(payload);
            Require(harness.RuntimeState.memories.Count == 1, "Repeated action occurrence records Memory once");
            Near(harness.RuntimeState.relation.trust, 6, "Repeated occurrence applies amplified effect once");
            Near(payload.relationDelta.trust, 4, "Publisher payload is not mutated by preference processing");
            harness.SelectNpcProfile("npc-haneul-food-ceo-kimharin");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(0);
            Near(harness.RuntimeState.relation.trust, 4, "Other NPC keeps default effect with response disabled");
            var saver = harness.GetComponent<NpcSaveService>();
            var serialized = new SerializedObject(saver);
            serialized.FindProperty("fileName").stringValue = Path.Combine(Output, "test-save.json");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(saver.TrySave(), "Save NPC runtime to isolated test file: " + saver.LastError);
            Require(saver.Load(), "Load canonical ID save: " + saver.LastError);
            Require(registry.States.Count == 14 && registry.States.All(s => registry.TryGetProfile(s.npcId, out var seed) && seed.companyId == s.companyId), "Saved states match canonical profile IDs");
            Require(registry.TryGet(seo, out var restored) && restored.memories.Count == 1
                && restored.memories[0].targetId == seo && restored.memories[0].memoryId.EndsWith(seo, StringComparison.Ordinal), "Memory target and deduplication key use canonical IDs");
            Near(restored.relation.trust, 6, "Saved preference-adjusted relation restored without reapplying multiplier");
            Near(restored.memories[0].effectLog.preferenceBonus.trust, 2, "Bonus details survive save/load");
            var canonicalSave = File.ReadAllText(saver.SavePath);
            File.WriteAllText(saver.SavePath, canonicalSave.Replace(seo, "npc-unregistered-test"));
            Require(!saver.Load(), "Unregistered saved ID is rejected instead of creating an orphan NPC");
            Require(registry.TryGet(seo, out var unchanged) && unchanged.memories.Count == 1, "Rejected save leaves runtime state intact");
            File.WriteAllText(saver.SavePath, canonicalSave);
            harness.SelectNpcProfile(seo);
            harness.ResetSelectedNpcState();
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = 99 });
            harness.PreviewPreferenceAction(0);
            Near(harness.LastRecordedMemory.relationDelta.trust, 6, "Clamped log retains requested final effect");
            Near(harness.LastRecordedMemory.effectLog.actuallyApplied.trust, 1, "Clamped log records actual +1 at trust cap");
            var cloned = harness.LastRecordedMemory.Clone();
            cloned.effectLog.preferenceBonus.trust = 999;
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 2, "Copied logs do not mutate original Memory details");
            // 이번에 바뀐 스탯 하한/상한과 이전 저장의 음수 값 보정을 함께 확인한다.
            harness.ResetSelectedNpcState();
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = -200, respect = -200, fear = -200,
                hostility = -200, dependency = -200, interest = -200 });
            var ranges = harness.RuntimeState.relation;
            Require(ranges.trust == -100 && ranges.respect == -100 && ranges.fear == 0
                && ranges.hostility == 0 && ranges.dependency == 0 && ranges.interest == 0, "All six lower limits match current rules");
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = 400, respect = 400, fear = 400,
                hostility = 400, dependency = 400, interest = 400 });
            ranges = harness.RuntimeState.relation;
            Require(ranges.trust == 100 && ranges.respect == 100 && ranges.fear == 100
                && ranges.hostility == 100 && ranges.dependency == 100 && ranges.interest == 100, "All six upper limits remain 100");
            harness.RuntimeState.relation.hostility = -25;
            harness.RuntimeState.relation.interest = -50;
            Require(saver.TrySave(), "Write isolated legacy negative-stat fixture");
            Require(saver.Load(), "Load legacy negative-stat save");
            Require(registry.TryGet(seo, out var normalized) && normalized.relation.hostility == 0
                && normalized.relation.interest == 0 && normalized.relation.trust == 100 && normalized.relation.respect == 100,
                "Load normalizes hostility and interest without changing signed axes");
        }

        private static void Near(float actual, float expected, string label) => Require(Mathf.Abs(actual - expected) < 0.001f, label + $" ({actual} vs {expected})");

        /// <summary>이벤트·NPC·회사 데이터의 문자열 참조가 실제 SO와 프리팹에 모두 연결되는지 검사한다.</summary>
        private static void VerifyProjectIdReferences()
        {
            var npcs = new Dictionary<string, NpcStateRegistry.NpcSeed>();
            foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition", new[] { "Assets/NPC/Data/Profiles" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                var seed = definition.CreateRuntimeSeed();
                if (!string.IsNullOrWhiteSpace(seed.npcId))
                {
                    npcs.Add(seed.npcId, seed);
                    var file = Path.GetFileNameWithoutExtension(path);
                    Require(definition.name == file.Replace('_', ' ').ToUpperInvariant(), "Passport asset/internal name matches: " + seed.profile.displayName);
                    Require(seed.npcId.EndsWith(file.Replace("_", "").ToLowerInvariant(), StringComparison.Ordinal),
                        "ID joins surname and given name without syllable hyphens: " + seed.profile.displayName);
                }
            }
            var expected = new HashSet<string> { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry" };
            var companies = AssetDatabase.FindAssets("t:CompanyDefinition", new[] { "Assets/ShareTrading/Data" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CompanyDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Require(companies.Length == 4 && new HashSet<string>(companies.Select(c => c.companyID)).SetEquals(expected), "Trading has exactly four canonical company SOs");
            var tradingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShareTrading/Prefabs/ShareTradingLogic.prefab");
            var referenced = tradingPrefab.GetComponentInChildren<Companies>(true).CompanyList;
            Require(referenced.Count == 4 && referenced.All(c => c != null && c.definition != null)
                && new HashSet<string>(referenced.Select(c => c.definition.companyID)).SetEquals(expected), "Trading prefab references all four companies without missing assets");
            var eventPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EventSystem/Prefabs/EventLogic.prefab");
            var world = eventPrefab.GetComponent<TakeOver.Events.EventTempWorldData>().getCompanies();
            Require(world.Count == 4 && new HashSet<string>(world.Select(c => c.companyId)).SetEquals(expected), "Event company table matches trading IDs");
            foreach (var company in world)
            {
                Require(npcs.TryGetValue(company.ceoNpcId, out var ceo) && ceo.companyId == company.companyId && ceo.position == NpcPosition.CEO,
                    "Event CEO resolves: " + company.companyId);
                Require(npcs.TryGetValue(company.restructuringNpcId, out var contact) && contact.companyId == company.companyId,
                    "Event restructuring contact resolves: " + company.companyId);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<TakeOver.Events.EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Require(definition.getCompanyIds().All(expected.Contains), "Event company conditions resolve: " + definition.name);
                foreach (var condition in definition.getConditions())
                    if (!string.IsNullOrWhiteSpace(condition.npcId)) Require(npcs.ContainsKey(condition.npcId), "Event NPC condition resolves: " + definition.name);
                foreach (var choice in definition.getChoices())
                {
                    var payload = choice.getNpcResult();
                    if (payload == null) continue;
                    Require((string.IsNullOrWhiteSpace(payload.companyId) || expected.Contains(payload.companyId))
                        && (payload.targetNpcIds == null || payload.targetNpcIds.All(npcs.ContainsKey))
                        && (payload.targetReactions == null || payload.targetReactions.All(r => r != null && npcs.ContainsKey(r.targetNpcId))),
                        "Event choice targets resolve: " + definition.name + "/" + choice.getResultValue());
                }
            }
        }
        private static void KeepNpcAndCamera(Transform node, Transform npc)
        {
            if (node == npc || node.IsChildOf(npc) || node.GetComponent<Camera>() != null) return;
            if (!npc.IsChildOf(node) && node.GetComponentInChildren<Camera>(true) == null)
            { node.gameObject.SetActive(false); return; }
            foreach (Transform child in node) KeepNpcAndCamera(child, npc);
        }
        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            Append("PASS: " + label);
        }
        private static void Append(string text)
        {
            results.Add(text);
            SessionState.SetString(Key + ".results", SessionState.GetString(Key + ".results", "") + text + "\n");
            Debug.Log("[NPC verification] " + text);
        }
        private static void Fail(Exception error)
        {
            failed = true;
            SessionState.SetBool(Key + ".failed", true);
            Append("FAIL: " + error);
        }
        private static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Finish()
        {
            try { Require(Hash("Assets/Scenes/Game.unity") == SessionState.GetString(Key + ".sceneHash", ""), "Game scene file unchanged"); }
            catch (Exception error) { Fail(error); }
            failed |= SessionState.GetBool(Key + ".failed", false);
            File.WriteAllText(Path.Combine(Output, "results.txt"), (failed ? "FAILED" : "PASSED") + "\n" + SessionState.GetString(Key + ".results", ""));
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Application.logMessageReceived -= OnLog;
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
