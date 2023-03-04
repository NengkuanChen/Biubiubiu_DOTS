using Unity.Entities;
using UnityEngine;

namespace Battle.Character
{
    public struct CharacterHitBoxComponent : IComponentData
    {
        public float DamageMultiplier;
    }
    
    public struct CharacterHitBoxEntityBuffer : IBufferElementData
    {
        public Entity HitBoxEntity;
    }
    
    public class CharacterHitBoxComponentAuthoring : MonoBehaviour
    {
        [SerializeField]
        private float damageMultiplier = 1f;
        
        public class CharacterHitBoxComponentAuthoringBaker : Baker<CharacterHitBoxComponentAuthoring>
        {
            public override void Bake(CharacterHitBoxComponentAuthoring authoring)
            {
                AddComponent(new CharacterHitBoxComponent
                {
                    DamageMultiplier = authoring.damageMultiplier
                });
            }
        }
    }
}