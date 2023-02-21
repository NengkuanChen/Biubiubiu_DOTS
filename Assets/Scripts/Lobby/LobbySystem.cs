using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game;
using Player;
using UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct LobbySystemClient: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkIdComponent>()
                .WithNone<NetworkStreamInLobby>()
                .WithAll<NetworkStreamConnection>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        
        public void OnDestroy(ref SystemState state)
        {
            
        }

        
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkIdComponent>>().WithEntityAccess().WithNone<NetworkStreamInLobby>())
            {
                commandBuffer.AddComponent<NetworkStreamInLobby>(entity);
                
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<PlayerJoinRequest>(req, new PlayerJoinRequest()
                {
                    PlayerNickname = MainMenuForm.Singleton<MainMenuForm>().NickName
                });
                MainMenuForm.Singleton<MainMenuForm>().OnConnectedToServer();
                
                commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = entity });
            }
            commandBuffer.Playback(state.EntityManager);
        }

        
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LobbySystemServer: ISystem
    {
        private ComponentLookup<NetworkIdComponent> networkIdFromEntity;
        // private Dictionary<int, PlayerIdentity> playerIdentityDictionary;


        public void OnCreate(ref SystemState state)
        {
            // state.RequireForUpdate<PlayerIdentitySpawner>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerJoinRequest>()
                .WithAll<ReceiveRpcCommandRequestComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            // var prefab = SystemAPI.GetSingleton<PlayerIdentitySpawner>().PlayerIdentitySpawnerPrefab;
            // state.EntityManager.GetName(prefab, out var prefabName);
            var worldName = new FixedString32Bytes(state.WorldUnmanaged.Name);

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state); 
            

            foreach (var (reqSrc,reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<PlayerJoinRequest>().WithEntityAccess())
            {
                // commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
                var networkIdComponent = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];
                var playerNickname = state.EntityManager.GetComponentData<PlayerJoinRequest>(reqEntity).PlayerNickname;
                Debug.Log($"'{worldName}' setting connection '{networkIdComponent.Value} : {playerNickname}' to in game");


                LobbyForm.Singleton<LobbyForm>().OnPlayerJoin(playerNickname.ToString(), networkIdComponent.Value,
                    out int teamId, out int positionID);
                
                var playerIdentity = commandBuffer.CreateEntity();
                // var player = commandBuffer.Instantiate(prefab);
                commandBuffer.SetName(playerIdentity, $"PlayerIdentity{playerNickname}");
                Debug.Log($"Player{playerNickname} assigned to team {teamId}");
                // UnityEngine.Debug.Log($"'{worldName}' setting connection '{networkIdComponent.Value}' to in game, spawning a Ghost '{prefabName}' for them!");
                
                commandBuffer.AddComponent(playerIdentity, new PlayerIdentity
                {
                    PlayerNickname = playerNickname,
                    TeamId = teamId,
                    InGameID = networkIdComponent.Value,
                    LobbyPositionID = positionID,
                    IsReady = false,
                    SourceConnection = reqSrc.ValueRO.SourceConnection
                });
                foreach (var value in SystemAPI.Query<RefRO<PlayerIdentity>>())
                {
                    var playerInfoUpdate = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(playerInfoUpdate, new PlayerIdentityUpdate
                    {
                        InGameID = value.ValueRO.InGameID,
                        PlayerNickname = value.ValueRO.PlayerNickname,
                        TeamID = value.ValueRO.TeamId,
                        LobbyPositionID = value.ValueRO.LobbyPositionID,
                        IsReady = value.ValueRO.IsReady
                    });
                    commandBuffer.AddComponent(playerInfoUpdate, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                }
                var selfIdentityUpdate = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(selfIdentityUpdate, new PlayerIdentityUpdate
                {
                    InGameID = networkIdComponent.Value,
                    PlayerNickname = playerNickname,
                    TeamID = teamId,
                    LobbyPositionID = positionID,
                    IsReady = false
                });
                commandBuffer.AddComponent(selfIdentityUpdate, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                // commandBuffer.SetComponent(player, new GhostOwnerComponent { NetworkId = networkIdComponent.Value});
                // Add the player to the linked entity group so it is destroyed automatically on disconnect
                // commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }


    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerReadySystemServer : ISystem
    {
        private ComponentLookup<NetworkIdComponent> networkIdFromEntity;
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerReadyRequest>()
                .WithAll<ReceiveRpcCommandRequestComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            networkIdFromEntity.Update(ref state);
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<PlayerReadyRequest>().WithEntityAccess())
            {
                var playerID = networkIdFromEntity[request.ValueRO.SourceConnection].Value;
                bool allReady = true;
                int readyCount = 0;
                foreach (var (playerIdentity, e) in SystemAPI.Query<RefRW<PlayerIdentity>>().WithEntityAccess())
                {
                    if (playerIdentity.ValueRO.InGameID == playerID)
                    {
                        playerIdentity.ValueRW.IsReady = !playerIdentity.ValueRW.IsReady;
                        
                        
                        var playerIdentityUpdate = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent(playerIdentityUpdate, new PlayerIdentityUpdate()
                        {
                            InGameID = playerID,
                            PlayerNickname = playerIdentity.ValueRO.PlayerNickname,
                            TeamID = playerIdentity.ValueRO.TeamId,
                            LobbyPositionID = playerIdentity.ValueRO.LobbyPositionID,
                            IsReady = playerIdentity.ValueRO.IsReady
                        });
                        Debug.Log($"Player {playerIdentity.ValueRO.PlayerNickname} is ready: {playerIdentity.ValueRO.IsReady}");
                        LobbyForm.Singleton<LobbyForm>().OnPlayerReady(playerID, playerIdentity.ValueRW.IsReady);
                        commandBuffer.AddComponent(playerIdentityUpdate, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                    }
                    if (!playerIdentity.ValueRO.IsReady)
                    {
                        allReady = false;
                    }
                    else
                    {
                        readyCount++;
                    }
                }
                if (allReady && readyCount > 0)
                {
                    StartLoadingBattleScene(ref state, ref commandBuffer);
                }
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }

        public void StartLoadingBattleScene(ref SystemState state, ref EntityCommandBuffer commandBuffer)
        {
            commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var sceneLoadRequest = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(sceneLoadRequest, new StartLoadingSceneCommand()
            {
                LoadSceneName = "Battle",
                UnloadSceneName = "Lobby",
            });
            commandBuffer.AddComponent(sceneLoadRequest, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
            var loadingSceneEntity = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(loadingSceneEntity, new StartLoadSceneComponent()
            {
                LoadSceneName = "Battle",
                UnloadSceneName = "Lobby",
            });
            foreach (var playerIdentity in SystemAPI.Query<RefRO<PlayerIdentity>>())
            {
                var entity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(entity, new ClientFinishedLoadingSceneComponent
                {
                    PlayerID = playerIdentity.ValueRO.InGameID,
                    HasFinishedLoading = false
                });
            }
            Debug.Log("All players are ready, starting loading battle scene");
            // commandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
        }
    }
    

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct OnPlayerIdentityUpdateSystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerIdentityUpdate>()
                .WithAll<ReceiveRpcCommandRequestComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (playerIdentity, e) in SystemAPI.Query<PlayerIdentityUpdate>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                LobbyForm.Singleton<LobbyForm>().OnPlayerInfoUpdate(playerIdentity.InGameID,
                    playerIdentity.PlayerNickname.ToString(), playerIdentity.TeamID, playerIdentity.LobbyPositionID,
                    playerIdentity.IsReady);
                commandBuffer.DestroyEntity(e);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }

    public struct NetworkStreamInLobby : IComponentData
    {
    }
    
    public struct PlayerJoinRequest : IRpcCommand
    {
        public FixedString32Bytes PlayerNickname;
    }
    
    public struct PlayerReadyRequest : IRpcCommand
    {
        public int InGameID;
        public bool IsReady;
    }
    
    public struct PlayerIdentityUpdate : IRpcCommand
    {
        public int InGameID;
        public int TeamID;
        public int LobbyPositionID;
        public bool IsReady;
        public FixedString32Bytes PlayerNickname;
    }
    
    public struct ClearOriginalButtonRequest : IRpcCommand
    {
        public int PositionID;
        public int TeamID;
    }
    
   
    
}