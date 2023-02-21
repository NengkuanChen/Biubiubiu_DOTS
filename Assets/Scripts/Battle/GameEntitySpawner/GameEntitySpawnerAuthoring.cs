using Unity.Entities;
using UnityEngine;

namespace Battle.GameEntitySpawner
{
    public struct GameEntitySpawnerComponent : IComponentData
    {
        public Entity PlayerGhost;
        public Entity MechBotGhost;
    }
    
    public class GameEntitySpawnerAuthoring: MonoBehaviour
    {
        public GameObject playerGhostPrefab;
        public GameObject MechBotPrefab;
        
        public class GameEntitySpawnerAuthoringBaker : Baker<GameEntitySpawnerAuthoring>
        {
            public override void Bake(GameEntitySpawnerAuthoring authoring)
            {
                GameEntitySpawnerComponent component = default(GameEntitySpawnerComponent);
                component.PlayerGhost = GetEntity(authoring.playerGhostPrefab);
                component.MechBotGhost = GetEntity(authoring.MechBotPrefab);
                AddComponent(component);
            }
        }
    }
}