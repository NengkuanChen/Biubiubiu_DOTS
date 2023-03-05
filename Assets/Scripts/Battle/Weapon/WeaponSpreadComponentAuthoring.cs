using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Battle.Weapon
{
    
    [Serializable]
    [GhostComponent]
    public struct WeaponSpreadComponent : IComponentData
    {
        public int SpreadTypeIndex;
        public float SpreadPercentage;
        [GhostField] 
        public Random Randomizer;
    }

    [GhostComponent]
    [Serializable]
    public struct SpreadInfoBuffer : IBufferElementData
    {
        public float SpreadAngleRotX;
        public float SpreadAngleRotZ;
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
                    SpreadTypeIndex = authoring.SpreadTypeIndex,
                    SpreadPercentage = 0f,
                    Randomizer = Random.CreateFromIndex(0),
                });
                AddBuffer<SpreadInfoBuffer>();
            }
        }
    }
}