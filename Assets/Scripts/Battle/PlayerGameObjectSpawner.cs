using System;
using Battle.TransformSynchronizer;
using UnityEngine;

namespace Battle
{
    public class PlayerGameObjectSpawner : MonoBehaviour
    {
        public static PlayerGameObjectSpawner Singleton;

        public TransformSyncGameObject PlayerGameObjectPrefab;

        private void Awake()
        {
            Singleton = this;
        }

        public TransformSyncGameObject SpawnPlayerGameObject(Vector3 position, Quaternion rotation)
        {
            var playerGameObject = Instantiate(PlayerGameObjectPrefab, position, rotation);
            playerGameObject.Initialize();
            return playerGameObject;
        }
    }
}