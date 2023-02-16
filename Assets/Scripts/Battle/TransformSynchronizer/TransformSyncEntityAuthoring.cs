using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    public struct TransformSyncEntity: IComponentData
    {
        public int UniqueId;
    }
    
    public struct TransformSyncEntityInitializerComponent : IComponentData
    {
        public int UniqueId;
    }
    
    public struct TransformSyncComponent : IComponentData
    {
        public int UniqueId;
    }


    public struct SyncWithGameObjectTag : IEnableableComponent
    {
        
    }
    
    [DisallowMultipleComponent]
    public class TransformSyncEntityAuthoring : MonoBehaviour
    {
        public List<Transform> SyncTransforms;
        
        class TransformSyncEntityAuthoringBaker : Baker<TransformSyncEntityAuthoring>
        {
            public override void Bake(TransformSyncEntityAuthoring authoring)
            {
                TransformSyncEntity component = default(TransformSyncEntity);
                
                AddComponent(component);
            }
        }
    }
}