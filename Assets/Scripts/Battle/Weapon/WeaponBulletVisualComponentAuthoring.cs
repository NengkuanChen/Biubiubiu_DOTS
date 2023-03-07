using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponBulletVisualComponent : IComponentData
    {
        public Entity BulletVisualEntity;
    }
    
    [Serializable]
    public struct BulletSpawnVisualRequestBuffer : IBufferElementData
    {
        // public quaternion Rotation;
        public LocalTransform LocalTransform;
        public float3 HitPosition;
        public bool IsHit;
        public Entity BulletVisualPrefab;
    }

    
    public class WeaponBulletVisualComponentAuthoring : MonoBehaviour
    {
        public GameObject BulletVisualPrefab;
        
        public class WeaponBulletVisualComponentAuthoringBaker : Baker<WeaponBulletVisualComponentAuthoring>
        {
            public override void Bake(WeaponBulletVisualComponentAuthoring authoring)
            {
                WeaponBulletVisualComponent component = default(WeaponBulletVisualComponent);
                component.BulletVisualEntity = GetEntity(authoring.BulletVisualPrefab);
                AddComponent(component);
                AddBuffer<BulletSpawnVisualRequestBuffer>();
            }
        }
    }
}