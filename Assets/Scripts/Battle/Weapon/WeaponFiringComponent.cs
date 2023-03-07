using Unity.Entities;
using Unity.NetCode;

namespace Battle.Weapon
{
    [GhostComponent]
    public struct WeaponFiringComponent : IComponentData
    {
        public int RoundBulletsCounter;
        public float RoundShotTimer;
        [GhostField]
        public int TickBulletsCounter;
        [GhostField]
        public float TickShotTimer;
        [GhostField]
        public bool IsFiring;
    }
    
}