using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponBulletComponent : IComponentData
    {
        public Entity BulletEntity;
        public Entity BulletVisualEntity;

    }
    
    public struct BulletSpawnRequest : IComponentData
    {
        public Entity BulletEntity;
        public Entity BulletVisualEntity;
        public float3 WorldPosition;
        public float3 Direction;
    }
    
    
    
    
    public class WeaponBulletComponentAuthoring : MonoBehaviour
    {
        public GameObject BulleftPrefab;
        public GameObject BulletVisualPrefab;
        
        public class WeaponBulletComponentAuthoringBaker : Baker<WeaponBulletComponentAuthoring>
        {
            public override void Bake(WeaponBulletComponentAuthoring authoring)
            {
                WeaponBulletComponent component = default(WeaponBulletComponent);
                component.BulletEntity = GetEntity(authoring.BulleftPrefab);
                component.BulletVisualEntity = GetEntity(authoring.BulletVisualPrefab);
                AddComponent(component);
            }
        }
    }
}