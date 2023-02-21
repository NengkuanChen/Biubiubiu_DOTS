using Unity.Entities;
using UnityEngine;

namespace Game.Battle.TransformSynchronizer
{
    public struct TransformSyncComponent : IComponentData
    {
        public int UniqueId;
        public int Index;
    }
    
    public class TransformSyncComponentAuthoring : MonoBehaviour
    {
        public int Index;

        class TransformSyncComponentAuthoringBaker : Baker<TransformSyncComponentAuthoring>
        {
            public override void Bake(TransformSyncComponentAuthoring authoring)
            {
                TransformSyncComponent component = default(TransformSyncComponent);
                component.Index = authoring.Index;
                AddComponent(component);
            }
        }
    }
    
}