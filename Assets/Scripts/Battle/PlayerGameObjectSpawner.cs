using System;
using Battle.TransformSynchronizer;
using UnityEngine;

namespace Battle
{
    public class PlayerGameObjectSpawner : MonoBehaviour
    {
        public static PlayerGameObjectSpawner Singleton;

        public GameObject ServerGameObjectPrefab;
        
        public GameObject ClientGameObjectPrefab;

        private void Awake()
        {
            Singleton = this;
        }

        public GameObject SpawnServerPlayerGameObject(Vector3 position, Quaternion rotation)
        {
            var playerGameObject = Instantiate(ServerGameObjectPrefab, position, rotation);
            // playerGameObject.Initialize();
            return playerGameObject;
        }
        
        public GameObject SpawnClientPlayerGameObject(Vector3 position, Quaternion rotation)
        {
            var playerGameObject = Instantiate(ClientGameObjectPrefab, position, rotation);
            // playerGameObject.Initialize();
            return playerGameObject;
        }
    }
}