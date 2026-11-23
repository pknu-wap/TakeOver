using System;
using System.Collections;
using System.IO;
using System.Linq;
using TakeOver.MainMenu;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using TakeOver.Story;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SaveSlotMenuVerification
{
    internal const string Root = "../QA_SaveSlotMenu";
    static SaveSlotMenuVerification() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Root + "/results.txt", "RUNNING\n");
        SessionState.SetString("SaveSlotMenu.PreviousSaveRoot", Environment.GetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", Path.GetFullPath(Root + "/save-" + Guid.NewGuid().ToString("N")));
        SessionState.SetBool("SaveSlotMenu.Running", true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("SaveSlotMenu.Running", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var probe = new GameObject("Save slot menu verification");
            Object.DontDestroyOnLoad(probe); probe.AddComponent<SaveSlotMenuProbe>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("SaveSlotMenu.Running", false);
            Environment.SetEnvironmentVariable("TAKEOVER_SAVE_DIRECTORY", SessionState.GetString("SaveSlotMenu.PreviousSaveRoot", ""));
            EditorApplication.Exit(File.ReadAllText(Root + "/results.txt").Contains("FAIL") ? 1 : 0);
        }
    }
}

public sealed class SaveSlotMenuProbe : MonoBehaviour
{
    private float deadline;
    private void Update()
    {
        if (deadline > 0 && Time.realtimeSinceStartup > deadline)
        {
            File.AppendAllText(SaveSlotMenuVerification.Root + "/results.txt", "FAIL: timeout\n");
            deadline = 0; EditorApplication.isPlaying = false;
        }
    }
    private static Button FindButton(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
    private static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        File.AppendAllText(SaveSlotMenuVerification.Root + "/results.txt", "PASS: " + text + "\n");
    }
    private IEnumerator Start()
    {
        deadline = Time.realtimeSinceStartup + 120;
        var run = Verify();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error) { File.AppendAllText(SaveSlotMenuVerification.Root + "/results.txt", "FAIL: " + error); break; }
            yield return next;
        }
        deadline = 0; EditorApplication.isPlaying = false;
    }
    private IEnumerator Verify()
    {
        yield return null;
        Check(!Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "LoadGameButton"), "Title has one continue entry without a separate load button");
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        Check(FindButton("ContinueButton").interactable, "Empty saves still allow opening the slot list");
        menu.startContinue(); yield return null;
        Check(Object.FindFirstObjectByType<SaveSlotWindow>() != null, "Continue opens the slot picker instead of changing scenes");
        Check(!FindButton("Automatic save load").interactable, "Empty automatic save cannot be loaded");
        for (int slot = 1; slot <= 3; slot++)
            Check(!FindButton("Save slot " + slot + " load").interactable, "Empty manual slot disabled: " + slot);
        FindButton("Close saves").onClick.Invoke(); yield return null;
        var fixture = new SaveFileData { formatVersion = SaveFileData.currentFormatVersion, dayStart = new DayStartData { day = 7 } };
        new SaveFileStorage(GameSaveManager.SaveDirectory).write(JsonUtility.ToJson(fixture));
        new SaveFileStorage(Path.Combine(GameSaveManager.SaveDirectory, "manual", "2")).write(JsonUtility.ToJson(fixture));
        menu.startContinue(); yield return null;
        var window = Object.FindFirstObjectByType<SaveSlotWindow>();
        var labels = window.GetComponentsInChildren<Text>().Where(t => t.name == "Automatic save slot" || t.name.StartsWith("Save slot ")).OrderByDescending(t => t.rectTransform.anchoredPosition.y).ToArray();
        Check(labels.Length == 4 && labels[0].name == "Automatic save slot" && labels[1].name == "Save slot 1" && labels[2].name == "Save slot 2" && labels[3].name == "Save slot 3", "Automatic save appears above the three manual slots");
        Check(labels[0].text.Contains("7일차") && labels[2].text.Contains("7일차"), "Automatic and manual save dates are shown");
        Check(FindButton("Automatic save load").interactable && FindButton("Save slot 2 load").interactable, "Existing automatic and manual saves can be selected");
        Check(window.GetComponentsInChildren<Image>().Single(i => i.name == "Event style information").color.a == 1f, "Slot panel is opaque");
        Check(!window.GetComponentsInChildren<Button>().Any(b => b.name.EndsWith(" save")), "Title picker does not offer saving");
        var canvas = window.GetComponent<Canvas>();
        var cameraObject = new GameObject("Slot capture camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.GetFullPath(SaveSlotMenuVerification.Root + "/slots.png"), texture.EncodeToPNG());
        camera.backgroundColor = Color.red; camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); var redBackdrop = texture.GetPixel(640, 360);
        camera.backgroundColor = Color.green; camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        Check(Vector4.Distance(redBackdrop, texture.GetPixel(640, 360)) < .01f, "Panel pixels do not reveal the background behind the window");
        RenderTexture.active = previous; camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(texture);
        yield return null;
        FindButton("Automatic save load").onClick.Invoke(); yield return null;
        Check(window.GetComponentsInChildren<Text>().Any(t => t.text == "자동저장한 시점부터 다시 진행합니다."), "Automatic save opens the matching confirmation");
        Check(window.GetComponentsInChildren<Image>().Single(i => i.name == "Event style information").color.a == 1f, "Confirmation panel is opaque");
        FindButton("Cancel save action").onClick.Invoke(); yield return null;
        FindButton("Save slot 2 load").onClick.Invoke(); yield return null;
        Check(window.GetComponentsInChildren<Text>().Any(t => t.text == "수동저장 2의 시점부터 다시 진행합니다."), "Manual save opens its own matching confirmation");
        FindButton("Cancel save action").onClick.Invoke(); yield return null;
        FindButton("Close saves").onClick.Invoke(); yield return null;
        Check(Object.FindFirstObjectByType<SaveSlotWindow>() == null, "Cancel and close return to the title");
        menu.startWithTutorialChoice(true);
        var enterName = SessionSaveVerification.EnterProtagonistName("저장 확인");
        while (enterName.MoveNext()) yield return enterName.Current;
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready || StoryDirector.Active == null) yield return null;
        while (StoryDirector.Active != null) yield return null;
        foreach (string name in new[] { "Confirm tutorial reward", "Confirm help notice" })
        { yield return null; FindButton(name).onClick.Invoke(); }
        yield return null;
        var manager = Object.FindFirstObjectByType<GameSaveManager>();
        var clock = Object.FindFirstObjectByType<GameTurnClock>();
        var player = Object.FindFirstObjectByType<Player>();
        player.state.addCash(11111 - player.state.cash);
        Check(manager.TrySaveManualSlot(2, new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Real manual snapshot saved");
        player.state.addCash(22222 - player.state.cash);
        Check(manager.trySaveDayEnd(new DayStartData(clock.CurrentTurn, FixedDaySeed.seed)), "Distinct automatic snapshot saved");
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        FindButton("ContinueButton").onClick.Invoke(); SessionSaveVerification.SelectAutomaticSave();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        Check(Object.FindFirstObjectByType<Player>().state.cash == 22222, "Continue picker restores the selected automatic snapshot");
        SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
        FindButton("ContinueButton").onClick.Invoke();
        FindButton("Save slot 2 load").onClick.Invoke(); FindButton("Confirm save action").onClick.Invoke();
        while (GameSessionCoordinator.Active == null || !GameSessionCoordinator.Active.Ready) yield return null;
        Check(Object.FindFirstObjectByType<Player>().state.cash == 11111, "Continue picker restores the selected manual snapshot separately");
        File.AppendAllText(SaveSlotMenuVerification.Root + "/results.txt", "COMPLETE\n");
    }
}
