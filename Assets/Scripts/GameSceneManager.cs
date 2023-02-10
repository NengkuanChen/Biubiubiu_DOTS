using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DefaultNamespace
{
    public static class GameSceneManager
    {
        public static float SceneLoadProgress { get; private set; }
        
        public static async UniTask SwitchSceneAsync(string loadScene, string unloadScene)
        {
            SceneLoadProgress = 0f;
            SceneManager.UnloadSceneAsync(unloadScene).ToUniTask().Forget();
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);
            while (!asyncOperation.isDone)
            {
                SceneLoadProgress = asyncOperation.progress;
            }
        }
    }
}