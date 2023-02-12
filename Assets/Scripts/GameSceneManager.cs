using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DefaultNamespace
{
    public static class GameSceneManager
    {
        public static float SceneLoadProgress { get; private set; }
        
        public static async Task SwitchSceneAsync(string loadScene, string unloadScene)
        {
            SceneLoadProgress = 0f;
            await SceneManager.UnloadSceneAsync(unloadScene);
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);
            while (!asyncOperation.isDone)
            {
                SceneLoadProgress = asyncOperation.progress;
            }
        }
    }
}