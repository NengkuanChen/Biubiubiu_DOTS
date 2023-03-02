using Unity.Entities;
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