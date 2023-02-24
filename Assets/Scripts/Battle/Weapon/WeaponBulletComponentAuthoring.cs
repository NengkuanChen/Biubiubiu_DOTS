using Unity.Entities;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponBulletComponent : IComponentData
    {
        public Entity BulletEntity;
    }
    
    public class WeaponBulletComponentAuthoring : MonoBehaviour
    {
        public GameObject BulleftPrefab;
        
        public class WeaponBulletComponentAuthoringBaker : Baker<WeaponBulletComponentAuthoring>
        {
            public override void Bake(WeaponBulletComponentAuthoring authoring)
            {
                WeaponBulletComponent component = default(WeaponBulletComponent);
                component.BulletEntity = GetEntity(authoring.BulleftPrefab);
                AddComponent(component);
            }
        }
    }
}