using Unity.Entities;

namespace Battle.Weapon
{
    public struct WeaponFiringComponent : IComponentData
    {
        public float BulletShot;
        public float NextFireTimeInterval;
        public bool IsFiring;
    }
    
}