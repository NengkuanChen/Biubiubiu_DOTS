using System;
using Unity.NetCode;

namespace DefaultNamespace
{
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            AutoConnectPort = 0;
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }
}