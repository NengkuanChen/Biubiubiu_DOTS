using System;
using Game.Battle.TransformSynchronizer;
using UnityEngine;

namespace Battle.TransformSynchronizer
{

    [ExecuteInEditMode]
    public class TransformSyncQuickSetter: MonoBehaviour
    {
        public TransformSyncEntityAuthoring syncEntity;

        public bool AttachTransformSyncComponents = false;

#if UNITY_EDITOR

        private void Update()
        {
            if (AttachTransformSyncComponents)
            {
                AttachSyncComponentsFromSyncObject();
            }
            AttachTransformSyncComponents = false;
        }
        
        public void AttachSyncComponentsFromSyncObject()
        {
            var syncTransforms = syncEntity.SyncTransforms;
            for (int i = 0; i < syncTransforms.Count; i++)
            {
                var syncTransform = syncTransforms[i];
                var component = syncTransform.gameObject.AddComponent<TransformSyncComponentAuthoring>();
                component.Index = i;
            }
        }
#endif
        
        
    }
}