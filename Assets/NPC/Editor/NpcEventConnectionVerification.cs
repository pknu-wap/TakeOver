using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TakeOver.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeOver.NPC.Editor
{
    /// <summary>현재 Game 씬을 저장하지 않고 실제 이벤트 선택 확정부터 NPC 기록까지 Play 모드로 검증한다.</summary>
    [InitializeOnLoad]
    public static class NpcEventConnectionVerification
    {
        private const string Key = "TakeOver.NPC.EventConnectionVerification";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool StatsOnly => SessionState.GetBool(Key + ".stats", false);
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            StatsOnly ? "../../psm/stat-ui-verification/results.txt" : "../../psm/event-connection-verification.txt"));
        private static double readyAt;
        private static int capturePhase;
        static NpcEventConnectionVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnRuntimeLog;
        }

        private static void OnRuntimeLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            File.AppendAllText(Output, "FAIL: runtime " + type + ": " + message + "\n" + stack + "\n");
        }

        [MenuItem("TakeOver/NPC/Verify Event Connection")]
        public static void Run()
        {
            SessionState.SetBool(Key + ".stats", false);
            RunInternal();
        }

        [MenuItem("TakeOver/NPC/Verify Stat UI")]
        public static void RunStats()
        {
            SessionState.SetBool(Key + ".stats", true);
            RunInternal();
        }

        private static void RunInternal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 모드 종료 후 실행하세요.");
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/Game.unity")
                throw new InvalidOperationException("기존 Game 씬에서 실행하세요. 검증기는 씬을 열거나 저장하지 않습니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            SessionState.SetString(Key + ".hash", Hash());
            File.WriteAllText(Output, "RUNNING\n");
            SessionState.SetBool(Key, true);
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.Show();
            gameView.Focus();
            capturePhase = 0;
            EditorApplication.isPlaying = true;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) readyAt = EditorApplication.timeSinceStartup + 3;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (Hash() != SessionState.GetString(Key + ".hash", "")) File.AppendAllText(Output, "FAIL: Game scene file changed\n");
                else File.AppendAllText(Output, "PASS: Game scene file unchanged\n");
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(Output).Contains("FAIL") ? 1 : 0);
            }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || readyAt == 0
                || EditorApplication.timeSinceStartup < readyAt) return;
            readyAt = 0;
            try
            {
                if (StatsOnly)
                {
                    if (CaptureStats()) return;
                    File.AppendAllText(Output, "PASSED\n");
                    EditorApplication.isPlaying = false;
                    return;
                }
                if (capturePhase == 0)
                {
                    Verify();
                    var ui = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestScene>();
                    var harness = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestHarness>();
                    harness.SelectNpcProfile("npc-mirae-construction-ceo-kangjunhyuk");
                    ui.ShowTestUI = true;
                    Set(ui, "bodyTab", 2);
                    var preview = harness.GetRelatedEvents().First(d => d.getSpeakerSource() == EventSpeakerSource.CompanyCeo);
                    Set(ui, "selectedActualEvent", preview);
                    harness.PreviewActualEvent(preview, 0);
                    capturePhase = 1;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                if (capturePhase == 1)
                {
                    ScreenCapture.CaptureScreenshot(Path.ChangeExtension(Output, ".png"));
                    capturePhase = 2;
                    readyAt = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                File.AppendAllText(Output, "PASSED\n");
            }
            catch (Exception error) { File.AppendAllText(Output, "FAILED: " + error + "\n"); Debug.LogException(error); }
            if (readyAt == 0) EditorApplication.isPlaying = false;
        }

        /// <summary>0・양수・음수・양끝 상태를 실제 정보 화면에 띄우고 각각 캡처한다. 저장은 하지 않는다.</summary>
        private static bool CaptureStats()
        {
            var values = new[] { 0f, 50f, -50f, 100f, -100f };
            var names = new[] { "zero", "positive", "negative", "maximum", "minimum" };
            var index = capturePhase / 2;
            if (index >= values.Length) return false;
            var ui = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestScene>();
            var harness = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestHarness>();
            Check(ui != null && harness != null, "Stat screen exists");
            if (capturePhase % 2 == 0)
            {
                harness.SelectNpcProfile("npc-mirae-construction-cfo-seojiah");
                harness.ResetAllNpcStates();
                var amount = index == 0 || index == 4 ? 0 : index == 3 ? 100 : 65;
                harness.ApplyTestRelationDelta(new NpcRelationDelta { trust = values[index], respect = -values[index],
                    fear = amount, hostility = amount, dependency = amount, interest = amount });
                ui.ShowTestUI = true;
                ui.ShowNpcInfo = true;
                var state = harness.RuntimeState.relation;
                Check(state.trust == values[index] && state.respect == -values[index], "Signed values: " + names[index]);
                Check(state.fear == amount && state.hostility == amount && state.dependency == amount && state.interest == amount,
                    "Unsigned values: " + names[index]);
            }
            else
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Output), names[index] + ".png"));
            capturePhase++;
            readyAt = EditorApplication.timeSinceStartup + 2;
            return true;
        }
        private static void Verify()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<EventSystemController>();
            var receiver = UnityEngine.Object.FindFirstObjectByType<NpcEventResultReceiver>();
            var harness = UnityEngine.Object.FindFirstObjectByType<NpcFeatureTestHarness>();
            Check(controller != null && receiver != null && harness != null, "Actual Game scene event and NPC components exist");
            var registry = receiver.GetComponent<NpcStateRegistry>();
            foreach (var profile in registry.Profiles)
            {
                harness.SelectNpcProfile(profile.npcId);
                foreach (var related in harness.GetRelatedEvents())
                {
                    var source = EditorJsonUtility.ToJson(related);
                    var previewInfo = harness.GetPreviewEventInfo(related);
                    Check(!HasPlaceholder(previewInfo.title + previewInfo.description + previewInfo.dialogue),
                        "Preview text resolves all company/speaker placeholders: " + profile.npcId + "/" + related.getId());
                    if (related.hasCompanyConditions() && profile.companyId == "external-network")
                        Check(previewInfo.companyId != "external-network" && !string.IsNullOrEmpty(previewInfo.companyName),
                            "External speaker previews an actual company");
                    if (related.getSpeakerSource() == EventSpeakerSource.CompanyCeo)
                    {
                        Check(registry.TryFindByPosition(profile.companyId, NpcPosition.CEO, out var ceo)
                            && previewInfo.companyName == ceo.profile.affiliation && previewInfo.speakerName == ceo.profile.displayName
                            && previewInfo.description.Contains(ceo.profile.affiliation) && previewInfo.description.Contains(ceo.profile.displayName),
                            "CEO preview uses this company's representative: " + profile.companyId);
                    }
                    for (var i = 0; i < related.getChoices().Count; i++)
                    {
                        harness.ResetAllNpcStates();
                        Check(harness.PreviewActualEvent(related, i), "NPC preview: " + profile.npcId + "/" + related.getId() + "/" + i);
                        var payload = related.getChoices()[i].getNpcResult();
                        var targets = (payload?.targetNpcIds ?? new List<string>()).Where(id =>
                            !related.hasCompanyConditions() || (registry.TryGetProfile(id, out var seed)
                                && (seed.companyId == profile.companyId || seed.companyId == "external-network"))).ToArray();
                        Check(registry.States.Sum(s => s.memories.Count) == targets.Length, "Preview creates exactly the expected target memories");
                        foreach (var target in targets)
                        {
                            registry.TryGet(target, out var state);
                            var reaction = payload.targetReactions.FirstOrDefault(r => r.targetNpcId == target);
                            var type = reaction == null ? payload.memoryType : reaction.memoryType;
                            var delta = reaction == null ? payload.relationDelta : reaction.relationDelta;
                            delta = registry.EvaluatePreference(target, payload.actionId, type,
                                registry.ApplyProfileEventBias(target, type, delta), out _);
                            var expected = new NpcRelationState(); expected.Apply(delta, 1);
                            Check(state.relation.trust == expected.trust && state.relation.respect == expected.respect
                                && state.relation.fear == expected.fear && state.relation.hostility == expected.hostility
                                && state.relation.dependency == expected.dependency && state.relation.interest == expected.interest,
                                "Preview applies all six relation axes for " + target);
                        }
                        Check(harness.ActualEventPreviewReport.Contains("처리 완료"), "Preview reports processing result");
                        Check(!HasPlaceholder(harness.ActualEventPreviewReport + previewInfo.resolve(related.getChoices()[i].getText())),
                            "Preview choice and result text contain no unresolved placeholders");
                        Check(string.IsNullOrEmpty(receiver.LastError), "Preview receiver has no error");
                    }
                    Check(EditorJsonUtility.ToJson(related) == source, "Preview leaves event SO unchanged");
                }
            }
            var definitions = (List<EventDefinition>)Get(controller, "events");
            const string lee = "npc-economic-reporter-leemingyu";
            const string seo = "npc-mirae-construction-cfo-seojiah";
            foreach (var id in new[] { "press_interview_01", "inside_info_01" })
            {
                var definition = definitions.Single(d => d.getId() == id);
                var original = EditorJsonUtility.ToJson(definition);
                for (var index = 0; index < definition.getChoices().Count; index++)
                {
                    harness.ResetAllNpcStates();
                    // 각 분기는 동일한 실제 초기 조건에서 독립 실행한다. 런타임 내부 목록만 초기화한다.
                    ((IDictionary)Get(controller, "selectedChoices")).Clear();
                    ((IDictionary)Get(controller, "companyChoices")).Clear();
                    ((IDictionary)Get(controller, "availableDays")).Clear();
                    ((IList)Get(controller, "occurrences")).Clear();
                    Set(controller, "currentDay", 0);
                    controller.onDayStarted(1);
                    Check(controller.getTodayEvents().Contains(definition), id + " appears without extra conditions");
                    ChoiceConfirmedInfo observed = null;
                    Action<ChoiceConfirmedInfo> capture = value => observed = value;
                    controller.choiceConfirmed += capture;
                    harness.SelectNpcProfile(id == "press_interview_01" ? lee : seo);
                    Check(harness.OpenActualEvent(definition), "NPC button opens actual event detail");
                    controller.startNegotiation();
                    controller.confirmChoice(index);
                    controller.choiceConfirmed -= capture;
                    Check(observed != null && observed.choiceIndex == index, id + "/" + index + " confirmed through controller");
                    Check(string.IsNullOrEmpty(receiver.LastError), "Receiver reports no error");
                    var npcId = id == "press_interview_01" ? lee : seo;
                    Check(registry.TryGet(npcId, out var state), "Target registered");
                    var expectedTrust = index == 0 ? 10 : index == 1 ? -10 : 0;
                    Check(Mathf.Approximately(state.relation.trust, expectedTrust), "Actual trust = " + expectedTrust);
                    Check(Mathf.Approximately(state.relation.respect, id == "press_interview_01" && index == 0 ? 10 : 0), "Actual respect matches event");
                    Check(Mathf.Approximately(state.relation.interest, id == "inside_info_01" && index == 0 ? 10 : 0), "Actual interest matches event");
                    Check(state.memories.Count == (index == 2 ? 0 : 1), "Memory count matches effect/no-effect choice");
                    Check(registry.States.Where(s => s.npcId != npcId).All(s => s.memories.Count == 0), "Other NPCs receive no memory");
                    if (observed.npcResult != null)
                    {
                        Check(state.memories[0].memoryId == observed.occurrenceId + ":" + npcId, "Occurrence ID preserved");
                        var before = JsonUtility.ToJson(state);
                        // 동일 알림 재전송, 비활성 해제, 재활성 재구독을 실제 이벤트 delegate로 검사한다.
                        ((Action<ChoiceConfirmedInfo>)Get(controller, "choiceConfirmed"))?.Invoke(observed);
                        Check(JsonUtility.ToJson(state) == before, "Duplicate delivery applies once");
                        receiver.enabled = false;
                        Check(!((Action<ChoiceConfirmedInfo>)Get(controller, "choiceConfirmed")).GetInvocationList().Any(d => ReferenceEquals(d.Target, receiver)), "Disable unsubscribes receiver");
                        receiver.enabled = true;
                        Check(((Action<ChoiceConfirmedInfo>)Get(controller, "choiceConfirmed")).GetInvocationList().Count(d => ReferenceEquals(d.Target, receiver)) == 1, "Re-enable subscribes exactly once");
                        ((Action<ChoiceConfirmedInfo>)Get(controller, "choiceConfirmed"))?.Invoke(observed);
                        Check(JsonUtility.ToJson(state) == before, "Re-enable does not duplicate effects");
                        var save = receiver.GetComponent<NpcSaveService>();
                        var oldFile = Get(save, "fileName");
                        try
                        {
                            Set(save, "fileName", Path.GetFullPath(Path.Combine(Application.dataPath, "../../psm/event-connection-test-save.json")));
                            Check(save.TrySave(), "Isolated verification save succeeds");
                            harness.ResetAllNpcStates();
                            Check(save.Load(), "Verification save loads");
                            registry.TryGet(npcId, out state);
                            Check(JsonUtility.ToJson(state) == before, "Relations and memory survive save/load");
                        }
                        finally { Set(save, "fileName", oldFile); }
                    }
                    controller.closeResult();
                    Check(EditorJsonUtility.ToJson(definition) == original, "Event SO unchanged");
                }
            }
        }
        private static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
        private static bool HasPlaceholder(string text) => text.Contains("{company}") || text.Contains("{speaker}") || text.Contains("{speakerRole}");
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            File.AppendAllText(Output, "PASS: " + label + "\n");
        }
        private static string Hash()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes("Assets/Scenes/Game.unity")));
        }
    }
}
