using Lobby;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
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
            foreach (var serverReady in SystemAPI.Query<ServerReadyToStartTag>())
            {
                isServerReady = true;
            }

            foreach (var clientReady in SystemAPI.Query<ClientReadyToStartTag>())
            {
                isClientReady = true;
            }

            if (isClientReady && isServerReady)
            {
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<StartGameCommand>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                GameStart();
            }
            
            commandBuffer.Playback(state.EntityManager);
        }
        
        private void GameStart()
        {
            Debug.Log("Server: GameStart");
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
                GameStart(ref commandBuffer);
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }

        private void GameStart(ref EntityCommandBuffer commandBuffer)
        {
            commandBuffer.AddComponent(SystemAPI.GetSingletonEntity<NetworkIdComponent>(), new NetworkStreamInGame());
        }
    }
    
    public struct BattleStartTag : IComponentData
    {
        
    }
}