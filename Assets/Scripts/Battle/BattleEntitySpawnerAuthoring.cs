using Unity.Entities;
using UnityEngine;

namespace Game.Battle
{
    public struct BattleEntitySpawner : IComponentData
    {
        public Entity PlayerGhost;
        public Entity TestCharacterGhost;
    }
    
    public class BattleEntitySpawnerAuthoring: MonoBehaviour
    {
        public GameObject playerGhostPrefab;
        public GameObject TestCharacterGhost;
        
        public class BattleEntitySpawnerAuthoringBaker : Baker<BattleEntitySpawnerAuthoring>
        {
            public override void Bake(BattleEntitySpawnerAuthoring authoring)
            {
                BattleEntitySpawner component = default(BattleEntitySpawner);
                component.PlayerGhost = GetEntity(authoring.playerGhostPrefab);
                component.TestCharacterGhost = GetEntity(authoring.TestCharacterGhost);
                AddComponent(component);
                
            }
        }
    }
}