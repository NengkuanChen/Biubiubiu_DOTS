using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Battle.Weapon
{
    public struct Bullet: IComponentData
    {
        public float Damage;
        public float LifeTime;
    }

    public struct BulletOwner : IComponentData
    {
        public Entity OwnerPlayer;
        public Entity OwnerWeapon;
        public Entity OwnerCharacter;
        public int OwnerID;
    }
    
    public struct BulletServerCleanUp : ICleanupComponentData
    {
        
    }
    
    public struct BulletDamageCleanUp : ICleanupComponentData
    {
        public Entity CausedByWeapon;
        public Entity CausedByPlayer;
        public Entity CausedByCharacter;
        public float DamageCaused;
        public float DamageMultiplier;
        public Entity DamagedCharacter;
    }
    
    
    
    public struct BulletClientCleanUp : ICleanupComponentData
    {
        public Entity BulletHitVfx;
        public float3 HitPosition;
        public float3 HitNormal;
        
    }
    
    public struct BulletVisualCleanUp : ICleanupComponentData
    {
        
    }
    
    
    public class BulletAuthoring : MonoBehaviour
    {
        public float Damage;
        public float MaxLifeTime;
        public class BulletAuthoringBaker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                AddComponent(new Bullet
                {
                    Damage = authoring.Damage,
                    LifeTime = authoring.MaxLifeTime
                });
                AddComponent(new BulletOwner());
            }
        }
    }
}