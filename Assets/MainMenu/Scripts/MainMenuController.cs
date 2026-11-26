using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Linq;
using TakeOver.SaveSystem;
using TakeOver.Events;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace TakeOver.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string nextScenePath = "Assets/Scenes/Game.unity";
        [SerializeField] private Toggle skipIntroToggle;
        [SerializeField] private GameObject tutorialPrompt;
        [SerializeField] private EventNegotiationView eventUi;
        [SerializeField] private EventDetailView informationStyle;
        [SerializeField] private Button settingsButton;
        public Toggle SkipIntroToggle => skipIntroToggle;
        private bool starting;
        private EscMenu settingsMenu;

        private void Awake()
        {
            if (skipIntroToggle != null) skipIntroToggle.SetIsOnWithoutNotify(false);
            tutorialPrompt.SetActive(false);
            var continueButton = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .FirstOrDefault(button => button.name == "ContinueButton");
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(startContinue);
                continueButton.interactable = eventUi != null;
            }

            settingsMenu = FindFirstObjectByType<EscMenu>(FindObjectsInactive.Include);
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(openSettings);
                settingsButton.interactable = settingsMenu != null;
            }
        }

        public void openSettings()
        {
            if (starting || tutorialPrompt.activeSelf || settingsMenu == null) return;
            settingsMenu.openSettingsOnly();
        }

        public void showNewGamePrompt()
        {
            if (!starting) tutorialPrompt.SetActive(true);
        }

        private void Update()
        {
            if (tutorialPrompt.activeSelf && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
                closeTutorialPrompt();
        }

        public void closeTutorialPrompt()
        {
            if (!starting) tutorialPrompt.SetActive(false);
        }

        public void startWithTutorialChoice(bool skipIntro)
        {
            if (starting) return;
            skipIntroToggle.SetIsOnWithoutNotify(skipIntro);
            startNewGame();
        }

        public void startNewGame()
        {
            if (starting) return;
            starting = true;
            TakeOver.Story.StoryLaunch.RequestNewGame(skipIntroToggle != null && skipIntroToggle.isOn);
            LoadGameScene();
        }

        public void startContinue()
        {
            if (starting || tutorialPrompt.activeSelf) return;
            SaveSlotWindow.Show(eventUi, true, informationStyle);
        }

        private void LoadGameScene()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(nextScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            // 빌드에서는 확장자 없는 경로("Assets/Scenes/Game")를 씬으로 인식하지 못한다.
            // 빌드 목록의 인덱스로 불러오고, 목록에서 못 찾으면 씬 이름으로 불러온다.
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(nextScenePath);
            if (buildIndex >= 0)
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
            else
                SceneManager.LoadScene(System.IO.Path.GetFileNameWithoutExtension(nextScenePath), LoadSceneMode.Single);
#endif
        }
    }
}
