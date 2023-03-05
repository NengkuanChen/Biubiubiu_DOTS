using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Game
{
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // AutoConnectPort = 7979;
            AutoConnectPort = 0;
#if UNITY_EDITOR
            Application.runInBackground = true;
#endif
            CreateLocalWorld(defaultWorldName);
            return true;
        }

        public static World CreateServerWorld(string name)
        {

            var world = new World(name, WorldFlags.GameServer);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

#if UNITY_DOTSRUNTIME
            AppendWorldToServerTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
        
        public static World CreateClientWorld(string name)
        {
            var world = new World(name, WorldFlags.GameClient);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);

#if UNITY_DOTSRUNTIME
            AppendWorldToClientTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
    }
}