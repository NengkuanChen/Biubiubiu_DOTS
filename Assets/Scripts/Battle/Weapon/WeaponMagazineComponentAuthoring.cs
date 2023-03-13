using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Weapon
{
    [GhostComponent]
    public struct WeaponMagazineComponent : IComponentData
    {
        [GhostField]
        public int MagazineSize;
        [GhostField]
        public int MagazineRestBullet;
        [GhostField]
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
    
    [GhostComponent]
    public struct WeaponReloadComponent : IComponentData
    {
        [GhostField]
        public float ReloadTimeLeft;
        [GhostField]
        public bool IsReloading;
    }
}