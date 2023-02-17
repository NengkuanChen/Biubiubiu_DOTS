using Battle;
using Battle.TransformSynchronizer;
using Lobby;
using UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;

namespace DefaultNamespace.Battle
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct BattleStartSystemServer: ISystem
    {
        private ComponentLookup<NetworkIdComponent> networkIdFromEntity;
        
        
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAny<ServerReadyToStartTag, ClientReadyToStartTag>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state);
            var isServerReady = false;
            var isClientReady = false;
            var serverReadyEntity = Entity.Null;
            var clientReadyEntity = Entity.Null;
            foreach (var (serverReady, e) in SystemAPI.Query<ServerReadyToStartTag>().WithEntityAccess())
            {
                isServerReady = true;
                serverReadyEntity = e;
            }

            foreach (var (clientReady, e) in SystemAPI.Query<ClientReadyToStartTag>().WithEntityAccess())
            {
                isClientReady = true;
                clientReadyEntity = e;
            }

            if (isClientReady && isServerReady)
            {
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<StartGameCommand>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                commandBuffer.DestroyEntity(serverReadyEntity);
                commandBuffer.DestroyEntity(clientReadyEntity);
                GameStart(commandBuffer, ref state);
            }
            
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
        
        private void GameStart(EntityCommandBuffer commandBuffer, ref SystemState state)
        {
            UIManager.Singleton.CloseForm<LoadingForm>();
            Debug.Log("Server: GameStart");
            //Test
            foreach (var (networkID, entity) in SystemAPI.Query<RefRO<NetworkIdComponent>>().WithEntityAccess())
            {
                // var playerGameObjectSpawner =
                //     PlayerGameObjectSpawner.Singleton.SpawnServerPlayerGameObject(Vector3.zero, quaternion.identity);
                // var uniqueId = playerGameObjectSpawner.UniqueId;
                // var syncEntityPrefab = SystemAPI.GetSingleton<PlayerSpawner>().playerPrefab;
                // var syncEntity = commandBuffer.Instantiate(syncEntityPrefab);
                // commandBuffer.AddComponent(syncEntity, new TransformSyncEntityInitializeComponent {UniqueId = uniqueId});
                // commandBuffer.AddComponent<EntitySyncFromGameObjectTag>(syncEntity);
                // commandBuffer.AddComponent(entity, new NetworkStreamInGame());
                // commandBuffer.SetComponent(syncEntity, new GhostOwnerComponent { NetworkId = networkID.ValueRO.Value});
                
                var playerPrefab = SystemAPI.GetSingleton<PlayerSpawner>().playerPrefab;
                var player = commandBuffer.Instantiate(playerPrefab);
                commandBuffer.AddComponent(entity, new NetworkStreamInGame());
                commandBuffer.AddComponent(entity, new GhostPresentationGameObjectPrefab
                {
                    Server = PlayerGameObjectSpawner.Singleton.ServerGameObjectPrefab,
                    Client = PlayerGameObjectSpawner.Singleton.ClientGameObjectPrefab
                });
                commandBuffer.SetComponent(player, new GhostOwnerComponent { NetworkId = networkID.ValueRO.Value});
            }
            
            
        }
    }
    
    
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct BattleStartSystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<StartGameCommand>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, entity) in SystemAPI.Query<RefRO<StartGameCommand>>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                GameStart(ref commandBuffer, ref state);
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }

        private void GameStart(ref EntityCommandBuffer commandBuffer, ref SystemState state)
        {
            UIManager.Singleton.CloseForm<LoadingForm>();
            commandBuffer.AddComponent(SystemAPI.GetSingletonEntity<NetworkIdComponent>(), new NetworkStreamInGame());
            
        }
    }
    
    public struct BattleStartTag : IComponentData
    {
        
    }
}