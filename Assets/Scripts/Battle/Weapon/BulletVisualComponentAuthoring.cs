using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Battle.Weapon
{
    public struct BulletVisualComponent: IComponentData
    {
        public float MaxDistance;
        public float Speed;
    }

    public struct BulletVisualMovementDataComponent : IComponentData
    {
        public float MaxDistance;
        public float DistanceTraveled;
        public bool IsHit;
    }
    
    public struct BulletVisualCleanUp : ICleanupComponentData
    {
        public Entity HitVfx;
        public float3 HitPosition;
        public float3 HitNormal;
    }
    
    public class BulletVisualComponentAuthoring : MonoBehaviour
    {
        public float MaxDistance;
        public float Speed;
        public GameObject HitVfxPrefab;
        
        public class BulletVisualComponentAuthoringBaker : Baker<BulletVisualComponentAuthoring>
        {
            public override void Bake(BulletVisualComponentAuthoring authoring)
            {
                AddComponent(new BulletVisualComponent
                {
                    MaxDistance = authoring.MaxDistance,
                    Speed = authoring.Speed,
                });
                AddComponent(new BulletVisualMovementDataComponent
                {
                    MaxDistance = authoring.MaxDistance,
                    DistanceTraveled = 0,
                    IsHit = false,
                });
                // if (authoring.HitVfxPrefab != null)
                // {
                //     AddComponent(new BulletVisualCleanUp
                //     {
                //         HitVfx = GetEntity(authoring.HitVfxPrefab)
                //     });
                // }
            }
        }
    }
}