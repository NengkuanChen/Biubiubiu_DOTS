﻿using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    public struct TransformSyncEntity: IComponentData
    {
        public int UniqueId;
    }
    
    public struct TransformSyncEntityInitializeComponent : IComponentData
    {
        public int UniqueId;
    }
    

    public struct GameObjectSyncFromEntityTag : IEnableableComponent, IComponentData
    {
        
    }
    
    public struct EntitySyncFromGameObjectTag : IEnableableComponent, IComponentData
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