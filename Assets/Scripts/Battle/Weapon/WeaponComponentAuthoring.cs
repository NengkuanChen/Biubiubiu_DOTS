using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponComponent : IComponentData
    {
        public bool FullAuto;
        public float FireInterval;
        public Entity MuzzleSocket;
    }
    
    
    [DisallowMultipleComponent]
    public class WeaponComponentAuthoring : MonoBehaviour
    {
        public bool FullAuto = true;
        public float FireRate = 5f;
        public GameObject MuzzleSocket;
        public class WeaponComponentBaker : Baker<WeaponComponentAuthoring>
        {
            public override void Bake(WeaponComponentAuthoring authoring)
            {
                AddComponent(new InterpolationDelay());
                AddComponent(new WeaponComponent
                {
                    FullAuto = authoring.FullAuto,
                    FireInterval = 1f / authoring.FireRate,
                    MuzzleSocket = GetEntity(authoring.MuzzleSocket),
                });
                AddComponent(new WeaponControlComponent());
                AddComponent(new WeaponFiringComponent());
            }
        }
    }
    
    [Serializable]
    [GhostComponent]
    public struct WeaponOwnerComponent : IComponentData
    {
        public Entity OwnerPlayer;
        public Entity OwnerCharacter;
    }
}