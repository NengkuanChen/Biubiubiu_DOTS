using Unity.Entities;
using UnityEngine;

namespace Battle.Weapon
{
    
    public struct WeaponSpreadComponent : IComponentData
    {
        public int SpreadTypeIndex;
    }
    
    public class WeaponSpreadComponentAuthoring : MonoBehaviour
    {
        public int SpreadTypeIndex;
        
        public class WeaponSpreadComponentBaker : Baker<WeaponSpreadComponentAuthoring>
        {
            public override void Bake(WeaponSpreadComponentAuthoring authoring)
            {
                AddComponent(new WeaponSpreadComponent
                {
                    SpreadTypeIndex = authoring.SpreadTypeIndex
                });
            }
        }
    }
}