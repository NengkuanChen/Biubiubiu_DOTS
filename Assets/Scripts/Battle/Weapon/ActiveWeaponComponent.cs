using System;
using Unity.Entities;
using Unity.NetCode;

namespace Battle.Weapon
{
    [Serializable]
    [GhostComponent]
    public struct ActiveWeaponComponent : IComponentData
    {
        [GhostField]
        public Entity WeaponEntity;

        public Entity PreviousWeaponEntity;
    }
}