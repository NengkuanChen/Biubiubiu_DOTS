using System.Collections.Generic;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    public static class TransformSyncManager
    {

        private static Dictionary<int, TransformSyncGameObject> syncTransformGameObjects = new Dictionary<int, TransformSyncGameObject>();
        public static Dictionary<int, TransformSyncGameObject> SyncTransformGameObjects => syncTransformGameObjects;


        public static int RegisterTransformSyncGameObject(TransformSyncGameObject transformSyncGameObject)
        {
            var uniqueId = UniqueIdGenerator.GetUniqueId();
            syncTransformGameObjects.Add(uniqueId, transformSyncGameObject);
            return uniqueId;
        }


    }
    
    public static class UniqueIdGenerator
    {
        private static int UniqueId = 0;
        public static int GetUniqueId()
        {
            return UniqueId++;
        }
    }
}