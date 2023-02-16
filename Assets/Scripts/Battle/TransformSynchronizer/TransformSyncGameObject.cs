using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    public class TransformSyncGameObject : MonoBehaviour
    {
        [SerializeField]
        private List<Transform> syncTransforms;
        public List<Transform> SyncTransforms => syncTransforms;

        // private Dictionary<int, Transform> syncTransformsDictionary = new Dictionary<int, Transform>();
        // public Dictionary<int, Transform> SyncTransformsDictionary => syncTransformsDictionary;
        
        private int uniqueId = 0;
        public int UniqueId => uniqueId;
        
        

        public int Initialize()
        {
            uniqueId = TransformSyncManager.RegisterTransformSyncGameObject(this);
            return uniqueId;
        }
        
        public Transform GetSyncTransform(int index)
        {
            return syncTransforms[index];
        }
        
        public int GetSyncTransformIndex(Transform transform)
        {
            return syncTransforms.IndexOf(transform);
        }
    }
    
    
}