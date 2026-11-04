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
            SceneManager.LoadScene(System.IO.Path.ChangeExtension(nextScenePath, null), LoadSceneMode.Single);
#endif
        }
    }
}
