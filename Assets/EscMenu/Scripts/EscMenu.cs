using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TakeOver.NPC;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class EscMenu : MonoBehaviour
{
    private const string mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [Tooltip("esc로 켜고 끄는 메뉴 전체 (어두운 배경 + 버튼 묶음)")]
    [SerializeField] private GameObject menuRoot;

    [Tooltip("설정 버튼을 누르면 열리는 패널")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("메인메뉴로 / 게임 끄기 전에 한 번 더 묻는 창")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private Text confirmText;

    [Tooltip("비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private NpcStatusScreen npcStatusScreen;

    private Action pendingAction;

  
    private bool npcHiddenByMenu;

    public bool isOpen => menuRoot.activeSelf;

    private void Awake()
    {
        if (npcStatusScreen == null)
        {
            npcStatusScreen = FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
        }

        menuRoot.SetActive(false);
        settingsPanel.SetActive(false);
        confirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        handleEscape();
    }

    private void handleEscape()
    {
        if (confirmPanel.activeSelf)
        {
            closeConfirm();
        }
        else if (settingsPanel.activeSelf)
        {
            closeSettings();
        }
        else if (isOpen)
        {
            resumeGame();
        }
        else
        {
            openMenu();
        }
    }

    public void openMenu()
    {
        hideNpcScreen();
        menuRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void resumeGame()
    {
        settingsPanel.SetActive(false);
        closeConfirm();
        menuRoot.SetActive(false);
        Time.timeScale = 1f;
        restoreNpcScreen();
    }

    public void openSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void closeSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void askGoToMainMenu()
    {
        askConfirm("메인메뉴로 돌아갈까요?", goToMainMenu);
    }

    public void askQuitGame()
    {
        askConfirm("게임을 종료할까요?", quitGame);
    }

    public void confirmYes()
    {
        Action action = pendingAction;
        closeConfirm();
        action?.Invoke();
    }

    public void closeConfirm()
    {
        pendingAction = null;
        confirmPanel.SetActive(false);
    }

    // 인물 화면은 OnGUI로 그려져 메뉴보다 항상 위에 보인다.
    // 오브젝트는 켜 둔 채 컴포넌트만 꺼서 그리기만 멈추고, 상단바 탭 상태와 선택한 NPC는 그대로 둔다.
    // TODO: 인물 화면이 Canvas(uGUI)로 바뀌면 hideNpcScreen / restoreNpcScreen 삭제
    private void hideNpcScreen()
    {
        if (npcStatusScreen != null && npcStatusScreen.isActiveAndEnabled)
        {
            npcStatusScreen.enabled = false;
            npcHiddenByMenu = true;
        }
    }

    private void restoreNpcScreen()
    {
        if (!npcHiddenByMenu)
        {
            return;
        }

        npcHiddenByMenu = false;
        if (npcStatusScreen != null)
        {
            npcStatusScreen.enabled = true;
        }
    }

    private void askConfirm(string message, Action onYes)
    {
        pendingAction = onYes;
        confirmText.text = message;
        confirmPanel.SetActive(true);
    }

    private void goToMainMenu()
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(mainMenuScenePath);
        if (buildIndex < 0)
        {
            Debug.LogWarning($"[EscMenu] Build Settings에 {mainMenuScenePath}가 없어서 이동하지 못했어요.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }

    private void quitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
