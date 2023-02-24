using Unity.Entities;

namespace Battle.Weapon
{
    public struct ActiveWeaponComponent : IComponentData
    {
        public Entity WeaponEntity;
    }
}