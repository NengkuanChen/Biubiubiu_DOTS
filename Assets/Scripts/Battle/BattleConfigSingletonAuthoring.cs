using Unity.Entities;
using UnityEngine;

namespace Battle
{
    public struct BattleConfigSingleton : IComponentData
    {
        public float RespawnTime;
    }
    
    [DisallowMultipleComponent]
    public class BattleConfigSingletonAuthoring : MonoBehaviour
    {
        [SerializeField]
        private float respawnTime = 5f;
        
        
        public class BattleConfigSingletonBaker : Baker<BattleConfigSingletonAuthoring>
        {
            public override void Bake(BattleConfigSingletonAuthoring authoring)
            {
                AddComponent(new BattleConfigSingleton
                {
                    RespawnTime = authoring.respawnTime,
                });
            }
        }
    }
}