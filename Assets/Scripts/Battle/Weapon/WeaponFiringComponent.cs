﻿using Unity.Entities;
using Unity.NetCode;

namespace Battle.Weapon
{
    [GhostComponent]
    public struct WeaponFiringComponent : IComponentData
    {
        public int RoundBulletsCounter;
        public float RoundShotTimer;
        public float TickBulletsCounter;
        public float TickShotTimer;
        public bool IsFiring;
    }
    
}