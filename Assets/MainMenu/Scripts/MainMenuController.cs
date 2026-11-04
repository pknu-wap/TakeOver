using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace TakeOver.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string nextScenePath = "Assets/Scenes/Game.unity";

        public void startNewGame()
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
