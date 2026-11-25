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

    [Tooltip("esc로 켜고 끄는 메뉴 전체 (어두운 배경 + 패널)")]
    [SerializeField] private GameObject menuRoot;

    [Tooltip("첫 화면의 버튼 4개 묶음. 설정을 열면 숨기고, 닫으면 다시 보인다")]
    [SerializeField] private GameObject mainButtons;

    [Tooltip("설정 버튼을 누르면 버튼 묶음 자리에 대신 보이는 설정 화면")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("메인메뉴로 / 게임 끄기 전에 한 번 더 묻는 창")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private Text confirmText;

    [Tooltip("비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private NpcStatusScreen npcStatusScreen;

    [Tooltip("끄면 esc 키로 메뉴가 열리지 않는다 (메인메뉴에서는 꺼서 설정 화면만 사용)")]
    [SerializeField] private bool openWithEsc = true;

    private Action pendingAction;

    private bool settingsOnly;

    private bool npcHiddenByMenu;

    public bool isOpen => menuRoot.activeSelf;

    private void Awake()
    {
        if (npcStatusScreen == null)
        {
            npcStatusScreen = FindFirstObjectByType<NpcStatusScreen>(FindObjectsInactive.Include);
        }

        menuRoot.SetActive(false);
        mainButtons.SetActive(true);
        settingsPanel.SetActive(false);
        confirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (isOpen)
        {
            closeTopLayer();
        }
        else if (openWithEsc)
        {
            openMenu();
        }
    }

    // esc 키와 패널 오른쪽 위 X 버튼이 같이 쓰는 "한 단계 닫기".
    // 확인창 → 설정 화면 → 메뉴 전체 순서로, 맨 위에 떠 있는 것 하나만 닫는다.
    public void closeTopLayer()
    {
        if (confirmPanel.activeSelf)
        {
            closeConfirm();
        }
        else if (settingsPanel.activeSelf && !settingsOnly)
        {
            closeSettings();
        }
        else
        {
            resumeGame();
        }
    }

    public void openMenu()
    {
        hideNpcScreen();
        menuRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void openSettingsOnly()
    {
        settingsOnly = true;
        menuRoot.SetActive(true);
        openSettings();
    }

    public void resumeGame()
    {
        settingsOnly = false;
        closeConfirm();
        // 설정 화면을 연 채로 메뉴를 닫아도, 다음에 열 때는 버튼 4개 화면부터 보이게 한다.
        closeSettings();
        menuRoot.SetActive(false);
        Time.timeScale = 1f;
        restoreNpcScreen();
    }

    public void openSettings()
    {
        mainButtons.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void closeSettings()
    {
        settingsPanel.SetActive(false);
        mainButtons.SetActive(true);
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
