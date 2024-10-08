﻿using System;
using Unity.Scenes;
using UnityEngine;

namespace Game
{
    public class EntitySpawnerManager: MonoBehaviour
    {
        public static EntitySpawnerManager Singleton;
        
        [SerializeField]
        private SubScene playerIdentitySpawnerSubScene;

        private void Awake()
        {
            Singleton = this;
        }
        
        public void LoadPlayerIdentitySpawnerSubScene()
        {
            // playerIdentitySpawnerSubScene.
        }
    }
}