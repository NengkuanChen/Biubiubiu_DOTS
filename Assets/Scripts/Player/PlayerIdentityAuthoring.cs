﻿using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Player
{
    [GhostComponent(PrefabType=GhostPrefabType.None)]
    public struct PlayerIdentity : IComponentData
    {
        [GhostField]
        public FixedString32Bytes PlayerNickname;

        // [GhostField]
        // public Byte[] PlayerNicknameBytes;

        
        public int TeamId;

        
        public int InGameID;

        
        public int LobbyPositionID;
    }
    
    public struct PlayerReadyComponent : IComponentData
    {
        public bool IsReady;
    }
    
    [DisallowMultipleComponent]
    public class PlayerIdentityAuthoring : MonoBehaviour
    {
        class PlayerIdentityComponentBaker : Baker<PlayerIdentityAuthoring>
        {
            public override void Bake(PlayerIdentityAuthoring authoring)
            {
                PlayerIdentity component = default(PlayerIdentity);
                // component.PlayerNicknameBytes = "Unnamed".to;
                component.TeamId = -1;
                component.InGameID = -1;
            }
        }
    }
}