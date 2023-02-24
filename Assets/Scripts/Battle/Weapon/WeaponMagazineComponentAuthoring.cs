using Unity.Entities;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponMagazineComponent : IComponentData
    {
        public int MagazineSize;
        public int MagazineRestBullet;
        public float ReloadTime;
    }
    
    public class WeaponMagazineComponentAuthoring: MonoBehaviour
    {
        public int MagazineSize;
        public float ReloadTime;
        
        public class WeaponMagazineComponentBaker : Baker<WeaponMagazineComponentAuthoring>
        {
            public override void Bake(WeaponMagazineComponentAuthoring authoring)
            {
                AddComponent(new WeaponMagazineComponent
                {
                    MagazineSize = authoring.MagazineSize,
                    MagazineRestBullet = authoring.MagazineSize,
                    ReloadTime = authoring.ReloadTime
                });
                AddComponent(new WeaponReloadComponent
                {
                    IsReloading = false,
                    ReloadTimeLeft = 0f
                });
            }
        }
    }
    
    public struct WeaponReloadComponent : IComponentData
    {
        public float ReloadTimeLeft;
        public bool IsReloading;
    }
}