using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DefaultNamespace.Battle
{
    public struct PlayerSpawner: IComponentData
    {
        public Entity playerPrefab;
    }

    

    [DisallowMultipleComponent]
    public class PlayerSpawnerAuthoring : MonoBehaviour
    {
        public GameObject playerPrefab;
        
        class PlayerSpawnerBaker : Baker<PlayerSpawnerAuthoring>
        {
            public override void Bake(PlayerSpawnerAuthoring authoring)
            {
                PlayerSpawner component = default(PlayerSpawner);
                component.playerPrefab = GetEntity(authoring.playerPrefab);
                AddComponent(component);
            }
        }
    }

}