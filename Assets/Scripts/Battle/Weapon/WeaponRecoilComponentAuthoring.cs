using Unity.Entities;
using UnityEngine;

namespace Battle.Weapon
{
    
    public struct WeaponRecoilComponent : IComponentData
    {
        public int RecoilTypeIndex;
    }
    public class WeaponRecoilComponentAuthoring : MonoBehaviour
    {
        public int RecoilTypeIndex;
        
        public class WeaponRecoilComponentBaker : Baker<WeaponRecoilComponentAuthoring>
        {
            public override void Bake(WeaponRecoilComponentAuthoring authoring)
            {
                AddComponent(new WeaponRecoilComponent
                {
                    RecoilTypeIndex = authoring.RecoilTypeIndex
                });
            }
        }
    }
    
    public struct FiringRecoilComponent : IComponentData
    {
        public float OffsetHorizontal;
        public float OffsetVertical;
        
    }
}