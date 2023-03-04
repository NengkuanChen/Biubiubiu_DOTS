﻿using System;
using Unity.Entities;
using Unity.Mathematics;
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
        public quaternion Rotation;
        public float3 Position;
        public Entity OwnerCharacter;
        public Entity OwnerWeapon;
        public Entity OwnerPlayer;
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