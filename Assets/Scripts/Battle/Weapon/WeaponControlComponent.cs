using Unity.Entities;

namespace Battle.Weapon
{
    public struct WeaponControlComponent: IComponentData
    {
        public bool FirePressed;
        public bool ReloadPressed;
        public bool FireReleased;
        public bool AimHeld;
    }
}