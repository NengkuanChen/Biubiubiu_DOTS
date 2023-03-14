using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Player
{
    [GhostComponent]
    public struct PlayerScoreComponent : IComponentData
    {
        [GhostField]
        public int Score;
    }
    
    [DisallowMultipleComponent]
    public class PlayerScoreComponentAuthoring : MonoBehaviour
    {
        public int InitialScore = 0;
        
        public class PlayerScoreComponentBaker : Baker<PlayerScoreComponentAuthoring>
        {
            public override void Bake(PlayerScoreComponentAuthoring authoring)
            {
                AddComponent(new PlayerScoreComponent
                {
                    Score = authoring.InitialScore,
                });
            }
        }
    }
}