using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Battle.Weapon
{
    [Serializable]
    [GhostComponent()]
    public struct RaycastWeaponComponent : IComponentData
    {
        public float MaxRange;
        public float Damage;
        public CollisionFilter HitFilter;
        public Entity ShotOrigin;
        public Entity VisualOrigin;
    }
    
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponBulletVisualComponentAuthoring))]
    public class RaycastWeaponComponentAuthoring : MonoBehaviour
    {
        public float MaxRange;
        public float Damage;
        public PhysicsCategoryTags HitFilter;
        
        public class RaycastWeaponComponentAuthoringBaker : Baker<RaycastWeaponComponentAuthoring>
        {
            public override void Bake(RaycastWeaponComponentAuthoring authoring)
            {
                AddComponent(new RaycastWeaponComponent
                {
                    MaxRange = authoring.MaxRange,
                    Damage = authoring.Damage,
                    HitFilter = new CollisionFilter
                    {
                        BelongsTo = CollisionFilter.Default.BelongsTo,
                        CollidesWith = authoring.HitFilter.Value
                    }
                });
                // AddComponent<InterpolationDelay>();
            }
        }
    }
}