using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponBulletComponent : IComponentData
    {
        public Entity BulletEntity;
        public bool IsGhost;
    }
    
    

    [Serializable]
    public struct BulletSpawnRequestBuffer : IBufferElementData
    {
        public quaternion Rotation;
        public float3 Position;
        public Entity OwnerCharacter;
        public Entity OwnerPlayer;
        public Entity OwnerWeapon;
        public Entity BulletPrefab;
        public bool IsGhost;
    }
    
    
    
    
    public class WeaponBulletComponentAuthoring : MonoBehaviour
    {
        public GameObject BulletPrefab;

        public class WeaponBulletComponentAuthoringBaker : Baker<WeaponBulletComponentAuthoring>
        {
            public override void Bake(WeaponBulletComponentAuthoring authoring)
            {
                WeaponBulletComponent component = default(WeaponBulletComponent);
                component.BulletEntity = GetEntity(authoring.BulletPrefab);
                component.IsGhost = authoring.BulletPrefab.GetComponent<GhostAuthoringComponent>() != null;
                AddBuffer<BulletSpawnRequestBuffer>();
                AddComponent(component);
            }
        }
    }
}