using Unity.Entities;
using UnityEngine;

namespace Battle.Weapon
{
    
    public struct BulletInitialPhysicsPropertyComponent : IComponentData
    {
        public float InitialSpeed;
        public float GravityMultiplier;
    }
    
    public class BulletInitialPhysicsPropertyComponentAuthoring: MonoBehaviour
    {
        public float InitialSpeed;
        public float GravityMultiplier;
        
        public class BulletInitialPhysicsPropertyComponentBaker : Baker<BulletInitialPhysicsPropertyComponentAuthoring>
        {
            public override void Bake(BulletInitialPhysicsPropertyComponentAuthoring authoring)
            {
                AddComponent(new BulletInitialPhysicsPropertyComponent
                {
                    InitialSpeed = authoring.InitialSpeed,
                    GravityMultiplier = authoring.GravityMultiplier
                });
            }
        }
    }
}