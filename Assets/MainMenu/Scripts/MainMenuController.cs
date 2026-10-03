using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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
#endif
        }
    }
}
