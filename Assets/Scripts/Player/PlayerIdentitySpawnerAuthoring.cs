using System;
using Unity.Entities;
using UnityEngine;

namespace Player
{
    public struct PlayerIdentitySpawner : IComponentData
    {
        public Entity PlayerIdentitySpawnerPrefab;
    }
    
    [DisallowMultipleComponent]
    public class PlayerIdentitySpawnerAuthoring : MonoBehaviour
    {
        
        public GameObject PlayerIdentitySpawner;

        class PlayerIdentitySpawnerBaker : Baker<PlayerIdentitySpawnerAuthoring>
        {
            public override void Bake(PlayerIdentitySpawnerAuthoring authoring)
            {
                PlayerIdentitySpawner component = default(PlayerIdentitySpawner);
                component.PlayerIdentitySpawnerPrefab = GetEntity(authoring.PlayerIdentitySpawner);
                AddComponent(component);
            }
        }
    }
}