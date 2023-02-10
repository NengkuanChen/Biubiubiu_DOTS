using DefaultNamespace;
using Player;
using UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

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
                .WithNone<NetworkStreamInGame>()
                .WithAll<NetworkStreamConnection>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        
        public void OnDestroy(ref SystemState state)
        {
            
        }

        
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkIdComponent>>().WithEntityAccess().WithNone<NetworkStreamInGame>())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<PlayerJoinRequest>(req, new PlayerJoinRequest()
                {
                    PlayerNickname = MainMenuForm.Singleton<MainMenuForm>().NickName
                });
                
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
        
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerIdentitySpawner>();
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
            var prefab = SystemAPI.GetSingleton<PlayerIdentitySpawner>().PlayerIdentitySpawnerPrefab;
            state.EntityManager.GetName(prefab, out var prefabName);
            var worldName = new FixedString32Bytes(state.WorldUnmanaged.Name);

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state); 
            

            foreach (var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<PlayerJoinRequest>().WithEntityAccess())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
                var networkIdComponent = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];
                var playerNickname = state.EntityManager.GetComponentData<PlayerJoinRequest>(reqEntity).PlayerNickname;
                UnityEngine.Debug.Log($"'{worldName}' setting connection '{networkIdComponent.Value} : {playerNickname}' to in game");
                
                
                LobbyForm.Singleton<LobbyForm>().OnPlayerJoin(playerNickname.ToString(), networkIdComponent.Value,out int teamId, out int positionID);
                
                
                var player = commandBuffer.Instantiate(prefab);
                
                commandBuffer.SetName(player, $"PlayerIdentity{playerNickname}");
                Debug.Log($"Player{playerNickname} assigned to team {teamId}");
                UnityEngine.Debug.Log($"'{worldName}' setting connection '{networkIdComponent.Value}' to in game, spawning a Ghost '{prefabName}' for them!");
                
                commandBuffer.AddComponent(player, new PlayerIdentity
                {
                    PlayerNickname = playerNickname,
                    TeamId = teamId,
                    InGameID = networkIdComponent.Value,
                    LobbyPositionID = positionID,
                    IsReady = false
                });
                
                commandBuffer.SetComponent(player, new GhostOwnerComponent { NetworkId = networkIdComponent.Value});
                // Add the player to the linked entity group so it is destroyed automatically on disconnect
                commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});
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
                foreach (var (playerIdentity, e) in SystemAPI.Query<PlayerIdentity>().WithEntityAccess())
                {
                    if (playerIdentity.InGameID == playerID)
                    {
                        commandBuffer.SetComponent(e, new PlayerIdentity()
                        {
                            PlayerNickname = playerIdentity.PlayerNickname,
                            TeamId = playerIdentity.TeamId,
                            InGameID = playerIdentity.InGameID,
                            LobbyPositionID = playerIdentity.LobbyPositionID,
                            IsReady = !playerIdentity.IsReady
                        });
                        var readyConfirm = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent(readyConfirm, new PlayerReadyRequest()
                        {
                            InGameID = playerID,
                            IsReady = !playerIdentity.IsReady
                        });
                        Debug.Log($"Player {playerIdentity.PlayerNickname} is ready: {!playerIdentity.IsReady}");
                        commandBuffer.AddComponent(readyConfirm, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                        break;
                    }
                }
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct PlayerReadySystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerReadyRequest>()
                .WithAll<ReceiveRpcCommandRequestComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (playerReady, e) in SystemAPI.Query<PlayerReadyRequest>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                LobbyForm.Singleton<LobbyForm>().OnPlayerReady(playerReady.InGameID, playerReady.IsReady);
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
    
    
}