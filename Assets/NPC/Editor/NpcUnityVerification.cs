using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TakeOver.Events;
using TakeOver.SaveSystem;
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
        private static string Output => Path.GetFullPath(Environment.GetEnvironmentVariable("TAKEOVER_NPC_QA_OUTPUT") ?? Path.Combine(Application.dataPath, "../../psm/unity-verification"));
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
                Require(ids.Count == 30, "Prefab has 30 unique NPC definitions");
                var definitions = new SerializedObject(registry).FindProperty("npcDefinitions");
                var ceos = 0;
                for (var i = 0; i < definitions.arraySize; i++)
                {
                    var seed = ((NpcDefinition)definitions.GetArrayElementAtIndex(i).objectReferenceValue).CreateRuntimeSeed();
                    Require(new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry", "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech", "external-network" }.Contains(seed.companyId)
                        && (seed.companyId == "external-network" || seed.npcId.StartsWith("npc-" + seed.companyId + "-", StringComparison.Ordinal)), "Canonical ID: " + seed.npcId);
                    if (seed.position == NpcPosition.CEO) ceos++;
                }
                Require(ceos == 9, "Exactly nine CEO definitions");
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
                    harness.SelectNpcProfile("npc-haneul-food-ceo-kim-harin");
                    Require(harness.SelectedProfile.profile.displayName == "김하린", "Info view switches to selected NPC");
                    ui.ShowNpcInfo = false;
                    phase = 3;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                }
                else if (phase == 3)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output, "npc-test.png"));
                    harness.SelectNpcProfile("npc-mirae-construction-cfo-seo-jia");
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
            Require(registry.Profiles.Count == 30 && registry.States.Count == 30, "All 30 NPCs initialized in Play mode");
            VerifyPositionLookups(registry);
            VerifySpeakerRoleOverrides(registry);
            NpcMemoryBalanceVerification.Verify(registry);
            VerifyPreferenceRegistration(registry);
            // 타 팀은 비활성 상태로 유지하고 초기화된 정식 ID 인덱스만 조회한다.
            var trading = UnityEngine.Object.FindFirstObjectByType<Companies>(FindObjectsInactive.Include);
            var market = UnityEngine.Object.FindFirstObjectByType<Marketdata>(FindObjectsInactive.Include);
            Require(trading != null && market != null, "Trading ID index exists for reference verification");
            foreach (var id in new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry", "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech" })
                Require(trading.getCompany(id) != null && market.tryGetCompany(id, out var company)
                    && company.definition.companyID == id, "Runtime company lookup: " + id);
            foreach (var company in new[] { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry", "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech" })
                Require(registry.TryFindByPosition(company, NpcPosition.CEO, out var ceo) && ceo.position == NpcPosition.CEO,
                    "CEO lookup: " + company);
            const string seo = "npc-mirae-construction-cfo-seo-jia";
            Require(harness.SelectNpcProfile(seo), "Select SEO JIA");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(0);
            Near(harness.RuntimeState.relation.trust, 4, "Action-only demo keeps base effect under Memory-only policy");
            Near(harness.LastRecordedMemory.effectLog.beforePreference.trust, 4, "Last-event log keeps pre-preference base");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 0, "Action-only log records no preference bonus");
            Require(NpcFeatureTestScene.FormatLastMemoryLog(harness.LastRecordedMemory).Contains("보정 비활성"), "Footer log identifies inactive action bonus");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(1);
            Near(harness.RuntimeState.relation.trust, -4, "Disliked action alone does not receive a bonus");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 0, "Action-only negative log has no bonus");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(2);
            Near(harness.RuntimeState.relation.trust, 4, "Unspecified action keeps base effect");
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 0, "Neutral log has no preference bonus");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(3);
            Near(harness.RuntimeState.relation.trust, 4.8f, "Seo GoodFaith Memory applies individual trust multiplier 1.2");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(4);
            Near(harness.RuntimeState.relation.trust, -5.6f, "Seo ContractBreach Memory applies individual multiplier 1.4");
            var mixed = new NpcRelationDelta { trust = -4, respect = -4, closeness = -4 };
            var result = registry.EvaluatePreference(seo, "ClearFundingPlan", NpcMemoryType.ContractBreach, mixed, out var disposition);
            Require(disposition == NpcActionDisposition.Dislikes, "Memory-only policy ignores conflicting liked action");
            Near(result.trust, -5.6f, "Contract breach scales negative trust once");
            Near(result.respect, -4, "Unselected respect axis remains unchanged");
            Near(result.closeness, -4, "Unselected closeness axis remains unchanged");
            var channel = harness.GetComponent<NpcEventChannel>();
            var payload = new NpcEventPayload { eventId = "unity-duplicate-check", actionId = "ClearFundingPlan",
                targetNpcIds = new List<string> { seo }, memoryType = NpcMemoryType.Custom, relationDelta = new NpcRelationDelta { trust = 4 } };
            harness.ResetSelectedNpcState();
            channel.Publish(payload);
            channel.Publish(payload);
            Require(harness.RuntimeState.memories.Count == 1, "Repeated action occurrence records Memory once");
            Near(harness.RuntimeState.relation.trust, 4, "Repeated action-only occurrence applies base effect once");
            Near(payload.relationDelta.trust, 4, "Publisher payload is not mutated by preference processing");
            harness.SelectNpcProfile("npc-haneul-food-ceo-kim-harin");
            harness.ResetSelectedNpcState();
            harness.PreviewPreferenceAction(0);
            Near(harness.RuntimeState.relation.trust, 4, "Other NPC keeps default effect with response disabled");
            var saver = harness.GetComponent<NpcSaveService>();
            var serialized = new SerializedObject(saver);
            serialized.FindProperty("fileName").stringValue = Path.Combine(Output, "test-save.json");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(saver.TrySave(), "Save NPC runtime to isolated test file: " + saver.LastError);
            Require(saver.Load(), "Load canonical ID save: " + saver.LastError);
            Require(registry.States.Count == 30 && registry.States.All(s => registry.TryGetProfile(s.npcId, out var seed) && seed.companyId == s.companyId), "Saved states match canonical profile IDs");
            Require(registry.TryGet(seo, out var restored) && restored.memories.Count == 1
                && restored.memories[0].targetId == seo && restored.memories[0].memoryId.EndsWith(seo, StringComparison.Ordinal), "Memory target and deduplication key use canonical IDs");
            Near(restored.relation.trust, 4, "Saved relation restored without reapplying a preference");
            Near(restored.memories[0].effectLog.preferenceBonus.trust, 0, "Zero action bonus survives save/load");
            var canonicalSave = File.ReadAllText(saver.SavePath);
            File.WriteAllText(saver.SavePath, canonicalSave.Replace(seo, "npc-unregistered-test"));
            Require(!saver.Load(), "Unregistered saved ID is rejected instead of creating an orphan NPC");
            Require(registry.TryGet(seo, out var unchanged) && unchanged.memories.Count == 1, "Rejected save leaves runtime state intact");
            File.WriteAllText(saver.SavePath, canonicalSave);
            VerifyLegacySave(saver, registry, seo, canonicalSave);
            harness.SelectNpcProfile(seo);
            harness.ResetSelectedNpcState();
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = 99 });
            harness.PreviewPreferenceAction(0);
            Near(harness.LastRecordedMemory.relationDelta.trust, 4, "Clamped log retains Memory-only action base effect");
            Near(harness.LastRecordedMemory.effectLog.actuallyApplied.trust, 1, "Clamped log records actual +1 at trust cap");
            var cloned = harness.LastRecordedMemory.Clone();
            cloned.effectLog.preferenceBonus.trust = 999;
            Near(harness.LastRecordedMemory.effectLog.preferenceBonus.trust, 0, "Copied logs do not mutate original Memory details");
            harness.ResetSelectedNpcState();
            var neutral = harness.RuntimeState.relation;
            Require(neutral.trust == 0 && neutral.respect == 0 && neutral.closeness == 0, "All three axes start at zero");
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = -200, respect = -200, closeness = -200 });
            var ranges = harness.RuntimeState.relation;
            Require(ranges.trust == -100 && ranges.respect == -100 && ranges.closeness == -100, "All three axes clamp at -100");
            harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = 400, respect = 400, closeness = 400 });
            ranges = harness.RuntimeState.relation;
            Require(ranges.trust == 100 && ranges.respect == 100 && ranges.closeness == 100, "All three axes clamp at +100");
            VerifyCautiousInterview(channel, registry);
            VerifyProfileGauges(registry);
            NpcPreferenceDefinitionVerification.VerifyRuntime();
        }

        private static void VerifySpeakerRoleOverrides(NpcStateRegistry registry)
        {
            var expected = new Dictionary<string, string>
            {
                { "npc-dongbang-trading-hr-director-park-minu", "인사책임자" },
                { "npc-daeseong-industry-hr-director-choi-eunsu", "인사책임자" },
                { "npc-sinseong-development-community-director-kang-minji", "지역관계 책임자" },
                { "npc-blueharbor-retail-operations-director-kim-dongseok", "매장운영 책임자" },
                { "npc-political-lobbyist-park-sanghyeon", "로비스트" }
            };
            var restructuring = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/Restructuring.asset");
            var world = new EventWorldReader(null, registry);
            foreach (var seed in registry.Profiles.Where(s => s.companyId != "external-network"))
            {
                if (!restructuring.tryGetRestructuringNpcId(seed.companyId, out var npcId) || npcId != seed.npcId) continue;
                var info = world.getEventInfo(restructuring, seed.companyId, 1);
                var role = expected.TryGetValue(seed.npcId, out var configured) ? configured : seed.profile.role;
                Require(info.speakerNpcId == seed.npcId && info.speakerRole == role,
                    "Restructuring speaker role override or fallback: " + seed.npcId);
                Require(info.speakerLabel.Contains(role) && info.resolve("{speakerRole}") == role,
                    "Speaker label and placeholder use event role: " + seed.npcId);
            }
            var fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data", "Assets/Story/Data" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.getSpeakerSource() != EventSpeakerSource.Fixed
                    || !expected.TryGetValue(definition.getSpeakerNpcId(), out var role)) continue;
                var info = world.getEventInfo(definition, "", 1);
                Require(info.speakerRole == role && info.speakerLabel.Contains(role),
                    "Fixed speaker event role: " + definition.getId());
                fixedCount++;
            }
            Require(fixedCount == 7, "All seven existing fixed-speaker events have configured roles");
        }

        private static void VerifyPositionLookups(NpcStateRegistry registry)
        {
            var neutralPositions = new HashSet<NpcPosition>
            {
                NpcPosition.MABroker, NpcPosition.InvestigativeReporter, NpcPosition.EconomicReporter,
                NpcPosition.InstitutionalInvestor, NpcPosition.PoliticalLobbyist
            };
            var assignedNeutralPositions = new HashSet<NpcPosition>();
            foreach (var seed in registry.Profiles)
            {
                Require(seed.position != NpcPosition.None && Enum.IsDefined(typeof(NpcPosition), seed.position),
                    "Assigned search position: " + seed.npcId);
                Require(registry.TryFindByPosition(seed.companyId, seed.position, out var found) && found.npcId == seed.npcId,
                    "Company and position resolve exact NPC: " + seed.npcId);
                if (seed.companyId == "external-network")
                    Require(neutralPositions.Contains(seed.position) && assignedNeutralPositions.Add(seed.position),
                        "Independent neutral position: " + seed.npcId);
                else
                    Require(!neutralPositions.Contains(seed.position), "Company NPC excludes neutral positions: " + seed.npcId);
            }
            Require(assignedNeutralPositions.SetEquals(neutralPositions), "All five neutral positions assigned");
            Require(!registry.TryFindByPosition("external-network", NpcPosition.MA, out _), "Company MA does not select neutral broker");
            Require(!registry.TryFindByPosition("polaris-finance", NpcPosition.MABroker, out _), "Neutral broker does not select company MA");
            Require(!registry.TryFindByPosition("missing-company", NpcPosition.CEO, out _), "Unknown company has no substitute NPC");
            Require(!registry.TryFindByPosition("external-network", NpcPosition.None, out _), "None has no substitute NPC");
            foreach (var seed in NpcStateRegistry.CreateDefaultNpcRoster())
                Require(registry.TryGetProfile(seed.npcId, out var registered) && registered.position == seed.position,
                    "Fallback position matches SO: " + seed.npcId);
        }

        private static void VerifyCautiousInterview(NpcEventChannel channel, NpcStateRegistry registry)
        {
            const string reporter = "npc-economic-reporter-lee-mingyu";
            var interview = AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/PressInterview.asset");
            var authored = interview.getChoices().Single(choice => choice.getResultValue() == "evade").getNpcResult();
            var payload = JsonUtility.FromJson<NpcEventPayload>(JsonUtility.ToJson(authored));
            payload.eventId = "unity-cautious-interview";
            Require(registry.TryGet(reporter, out var state), "Interview reporter is registered");
            var before = state.relation.closeness;
            channel.Publish(payload);
            Near(state.relation.closeness - before, -1f, "Actual evasive interview increases the reporter's caution by one");
        }

        private static void VerifyProfileGauges(NpcStateRegistry registry)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var view = UnityEngine.Object.FindFirstObjectByType<NpcProfilePanelView>(FindObjectsInactive.Include);
            Require(view != null, "Dashboard profile panel exists in the Game scene");
            view.Refresh();
            var entries = (System.Collections.IList)typeof(NpcProfilePanelView).GetField("_entries", fields).GetValue(view);
            Require(entries.Count > 0, "Dashboard profile panel has NPC entries");
            var first = entries[0];
            var npcId = (string)first.GetType().GetField("id").GetValue(first);
            Require(registry.TryGet(npcId, out var state), "Dashboard entry resolves to the canonical NPC state");
            var select = typeof(NpcProfilePanelView).GetMethod("SelectIndex", fields);
            RectTransform Tick(string fieldName) => (RectTransform)typeof(NpcRelationGaugeView)
                .GetField("indicatorTickRect", fields).GetValue(typeof(NpcProfilePanelView).GetField(fieldName, fields).GetValue(view));
            var trust = Tick("hostilityTrustGauge");
            var respect = Tick("fearRespectGauge");
            var closeness = Tick("dependencyInterestGauge");
            var original = state.relation;
            try
            {
                state.relation = default;
                select.Invoke(view, new object[] { 0 });
                Require(trust.anchorMin.x == .5f && respect.anchorMin.x == .5f && closeness.anchorMin.x == .5f,
                    "All dashboard relation gauges place zero at the midpoint");
                state.relation.Apply(new NpcRelationDelta { trust = -100f, respect = 100f, closeness = 1f }, 1);
                select.Invoke(view, new object[] { 0 });
                Near(trust.anchorMin.x, 0f, "Hostility appears at the left limit");
                Near(respect.anchorMin.x, 1f, "Respect appears at the right limit");
                Near(closeness.anchorMin.x, .505f, "Closeness +1 appears just right of the midpoint");
                state.relation.Apply(new NpcRelationDelta { closeness = -3f }, 1);
                select.Invoke(view, new object[] { 0 });
                Near(closeness.anchorMin.x, .49f, "Caution -2 appears just left of the midpoint");
            }
            finally
            {
                state.relation = original;
                select.Invoke(view, new object[] { 0 });
            }
        }

        private static void VerifyLegacySave(NpcSaveService saver, NpcStateRegistry registry, string npcId, string canonicalSave)
        {
            var legacy = JObject.Parse(canonicalSave);
            legacy.Remove("formatVersion");
            var state = ((JArray)legacy["npcs"]).OfType<JObject>().Single(entry => (string)entry["npcId"] == npcId);
            var relation = (JObject)state["relation"];
            relation.Remove("closeness");
            relation["hostility"] = 7;
            relation["respect"] = 8;
            relation["fear"] = 3;
            relation["interest"] = 1;
            relation["dependency"] = 0;
            var memory = (JObject)((JArray)state["memories"])[0];
            var delta = (JObject)memory["relationDelta"];
            delta.Remove("closeness");
            delta["interest"] = 1;
            delta["dependency"] = 3;
            var beforePreference = (JObject)memory["effectLog"]["beforePreference"];
            beforePreference["hostility"] = 2;
            try
            {
                File.WriteAllText(saver.SavePath, legacy.ToString());
                Require(saver.Load() && registry.TryGet(npcId, out _), "Load legacy six-stat NPC save: " + saver.LastError);
                registry.TryGet(npcId, out var restored);
                Near(restored.relation.trust, -3, "Legacy hostility offsets trust");
                Near(restored.relation.respect, 5, "Legacy fear offsets respect");
                Near(restored.relation.closeness, 1, "Legacy interest of one restores as closeness +1");
                Near(restored.memories[0].relationDelta.closeness, -2, "Legacy memory delta preserves net closeness");
                Near(restored.memories[0].effectLog.beforePreference.trust, 2, "Legacy effect log converts to signed trust");
                Require(saver.TrySave() && (int)JObject.Parse(File.ReadAllText(saver.SavePath))["formatVersion"] == 2,
                    "Migrated relation is saved as version 2");
                Require(saver.Load() && registry.TryGet(npcId, out restored), "Migrated save round-trips");
                Near(restored.relation.closeness, 1, "Reloading version 2 preserves closeness");
                saver.prepareLoad(legacy.ToString(), 1, new DayStartData((int)legacy["currentTurn"], 1));
                saver.applyLoad();
                saver.rebuildAfterLoad();
                Require(registry.TryGet(npcId, out restored), "Game save participant loads NPC section version 1");
                Near(restored.relation.closeness, 1, "Game save participant preserves migrated closeness");
            }
            finally
            {
                File.WriteAllText(saver.SavePath, canonicalSave);
            }
            Require(saver.Load(), "Restore the canonical test save");
        }

        private static void VerifyPreferenceRegistration(NpcStateRegistry registry)
        {
            const string npcId = "npc-mirae-construction-cfo-seo-jia";
            Require(registry.TryGetProfile(npcId, out var profile), "Preference regression profile exists");
            try
            {
                var first = new NpcActionPreference { actionId = "GoodFaith", disposition = NpcActionDisposition.Unspecified };
                var liked = new NpcActionPreference { actionId = "ClearFundingPlan", disposition = NpcActionDisposition.Likes };
                registry.ConfigureActionPreferences(npcId, new List<NpcActionPreference>
                {
                    null, new NpcActionPreference { actionId = " " }, first,
                    new NpcActionPreference { actionId = "GoodFaith", disposition = NpcActionDisposition.Dislikes }, liked
                });
                Require(!registry.TryGetActionPreference(npcId, "GoodFaith", out var disposition)
                    && disposition == NpcActionDisposition.Unspecified, "First duplicate wins even when Unspecified");
                registry.EvaluatePreference(npcId, "GoodFaith", NpcMemoryType.GoodFaith,
                    new NpcRelationDelta { trust = 4 }, out disposition);
                Require(disposition == NpcActionDisposition.Likes, "Unspecified action still falls back to Memory preference");
                liked.actionId = "ChangedAfterRegistration";
                liked.disposition = NpcActionDisposition.Dislikes;
                Require(registry.TryGetActionPreference(npcId, "ClearFundingPlan", out disposition)
                    && disposition == NpcActionDisposition.Likes, "Preference registration snapshots source values");
                Require(!registry.TryGetActionPreference(npcId, "ChangedAfterRegistration", out _), "Source mutation does not change registered keys");
                Require(!registry.TryGetActionPreference(npcId, "clearfundingplan", out _), "Action lookup remains case sensitive");
                Require(!registry.TryGetActionPreference(null, "ClearFundingPlan", out _)
                    && !registry.TryGetActionPreference(npcId, null, out _), "Null lookup keeps false result");
                registry.ConfigureActionPreferences(npcId, null);
                Require(!registry.TryGetActionPreference(npcId, "ClearFundingPlan", out _), "Reconfiguration replaces old preferences");
                Require(registry.TryGet(npcId, out var state) && state.memories.Count == 0,
                    "Preference reconfiguration does not create memories");
            }
            finally { registry.ConfigureActionPreferences(npcId, profile.actionPreferences); }

            var roster = NpcStateRegistry.CreateDefaultNpcRoster();
            var second = NpcStateRegistry.CreateDefaultNpcRoster();
            Require(roster.Count == 5 && JsonUtility.ToJson(roster[0]) == JsonUtility.ToJson(second[0]),
                "Fallback roster retains the same five profiles");
            roster[0].profile.displayName = "changed";
            roster[0].actionPreferences[0].disposition = NpcActionDisposition.Unspecified;
            Require(second[0].profile.displayName != "changed"
                && second[0].actionPreferences[0].disposition == NpcActionDisposition.Likes,
                "Fallback roster returns independent nested profiles and preferences");
        }

        private static void Near(float actual, float expected, string label) => Require(Mathf.Abs(actual - expected) < 0.001f, label + $" ({actual} vs {expected})");

        /// <summary>이벤트·NPC·회사 데이터의 문자열 참조가 실제 SO와 프리팹에 모두 연결되는지 검사한다.</summary>
        private static void VerifyProjectIdReferences()
        {
            var expectedNpcIds = new Dictionary<string, string>
            {
                { "Jung_Sua", "npc-investigative-reporter-jung-sua" },
                { "Kim_Taeseong", "npc-institutional-investor-kim-taeseong" },
                { "Yoon_Hajin", "npc-broker-yoon-hajin" },
                { "Park_Sanghyeon", "npc-political-lobbyist-park-sanghyeon" },
                { "Kang_Junhyeok", "npc-mirae-construction-ceo-kang-junhyeok" },
                { "Seo_Jia", "npc-mirae-construction-cfo-seo-jia" },
                { "Park_Dosik", "npc-mirae-construction-field-director-park-dosik" },
                { "Lee_Mingyu", "npc-economic-reporter-lee-mingyu" },
                { "Kim_Harin", "npc-haneul-food-ceo-kim-harin" },
                { "Oh_Jaeseong", "npc-haneul-food-cfo-oh-jaeseong" },
                { "Kang_Jaehyeon", "npc-dongbang-trading-ceo-kang-jaehyeon" },
                { "Park_Minu", "npc-dongbang-trading-hr-director-park-minu" },
                { "Jo_Minseok", "npc-hansung-industry-ceo-jo-minseok" },
                { "Choi_Mijeong", "npc-hansung-industry-labor-director-choi-mijeong" },
                { "Lim_Dohyeon", "npc-polaris-finance-ceo-lim-dohyeon" },
                { "Song_Hyeonseok", "npc-polaris-finance-risk-director-song-hyeonseok" },
                { "Jung_Yura", "npc-polaris-finance-ma-director-jung-yura" },
                { "Kim_Seongho", "npc-daeseong-industry-ceo-kim-seongho" },
                { "Noh_Hyeonjeong", "npc-daeseong-industry-cfo-noh-hyeonjeong" },
                { "Oh_Jinu", "npc-daeseong-industry-business-development-director-oh-jinu" },
                { "Choi_Eunsu", "npc-daeseong-industry-hr-director-choi-eunsu" },
                { "Jung_Dohyeon", "npc-sinseong-development-ceo-jung-dohyeon" },
                { "Yoo_Jeongmin", "npc-sinseong-development-planning-director-yoo-jeongmin" },
                { "Kang_Minji", "npc-sinseong-development-community-director-kang-minji" },
                { "Seo_Yujin", "npc-blueharbor-retail-ceo-seo-yujin" },
                { "Han_Dogyeong", "npc-blueharbor-retail-data-director-han-dogyeong" },
                { "Kim_Dongseok", "npc-blueharbor-retail-operations-director-kim-dongseok" },
                { "Kim_Seohu", "npc-aurora-foodtech-ceo-kim-seohu" },
                { "Jung_Harin", "npc-aurora-foodtech-chief-scientist-jung-harin" },
                { "Han_Yuna", "npc-aurora-foodtech-business-development-director-han-yuna" }
            };
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
                    // Unity asset names are not NPC identifiers and have no required display format.
                    Require(expectedNpcIds.TryGetValue(file, out var expectedId) && seed.npcId == expectedId,
                        "NPC ID matches explicit approved roster: " + seed.profile.displayName);
                }
            }
            Require(npcs.Count == expectedNpcIds.Count && expectedNpcIds.Values.All(npcs.ContainsKey),
                "All approved NPC IDs are present without inferred name formats");
            var expected = new HashSet<string> { "mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry", "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech" };
            var companies = AssetDatabase.FindAssets("t:CompanyDefinition", new[] { "Assets/ShareTrading/Data" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CompanyDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Require(companies.Length == 9 && new HashSet<string>(companies.Select(c => c.companyID)).SetEquals(expected), "Trading has exactly nine canonical company SOs");
            var tradingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShareTrading/Prefabs/ShareTradingLogic.prefab");
            var referenced = tradingPrefab.GetComponentInChildren<Companies>(true).CompanyList;
            Require(referenced.Count == 9 && referenced.All(c => c != null && c.definition != null)
                && new HashSet<string>(referenced.Select(c => c.definition.companyID)).SetEquals(expected), "Trading prefab references all nine companies without missing assets");
            var restructuring = AssetDatabase.LoadAssetAtPath<TakeOver.Events.EventDefinition>("Assets/EventSystem/Data/Restructuring.asset");
            foreach (var company in referenced)
            {
                var companyId = company.definition.companyID;
                Require(npcs.Values.Count(npc => npc.companyId == companyId && npc.position == NpcPosition.CEO) == 1,
                    "Event CEO resolves: " + companyId);
                Require(restructuring.tryGetRestructuringNpcId(companyId, out var contactId)
                    && npcs.TryGetValue(contactId, out var contact) && contact.companyId == companyId,
                    "Event restructuring contact resolves: " + companyId);
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
