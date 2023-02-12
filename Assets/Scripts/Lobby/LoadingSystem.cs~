using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using DefaultNamespace.Battle;
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
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LoadingSystemServer: ISystem
    {

        
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StartLoadSceneComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var loadSceneName = new FixedString32Bytes();
            var unloadSceneName = new FixedString32Bytes();
            foreach (var (StartLoadSceneComponent, entity) in SystemAPI.Query<RefRO<StartLoadSceneComponent>>().WithEntityAccess())
            {
                loadSceneName = StartLoadSceneComponent.ValueRO.LoadSceneName;
                unloadSceneName = StartLoadSceneComponent.ValueRO.UnloadSceneName;
                var startLoadingSceneCommand = new StartLoadingSceneCommand
                {
                    LoadSceneName = loadSceneName,
                    UnloadSceneName = unloadSceneName
                };
                var loadSceneRpc = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(loadSceneRpc, startLoadingSceneCommand);
                commandBuffer.AddComponent(loadSceneRpc, new SendRpcCommandRequestComponent
                {
                    TargetConnection = Entity.Null
                });
                Debug.Log("Server: Loading Scene Command Sent");
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            LoadSceneAsync(loadSceneName.ToString(), unloadSceneName.ToString());
        }

        
        private async void LoadSceneAsync(string loadScene, string unloadScene)
        {
            Debug.Log("Server: LoadSceneAsync");
            UIManager.Singleton.CloseForm<LobbyForm>();
            UIManager.Singleton.ShowForm<LoadingForm>();
            if (WorldGetter.GetClientWorld() != null)
            {
                Debug.Log("Host mode, no need to load server scene");
            }
            else
            {
                SceneManager.LoadScene(loadScene, LoadSceneMode.Additive);
                SceneManager.UnloadScene(unloadScene);

            }
            // await GameSceneManager.SwitchSceneAsync(loadScene, unloadScene);
            
            Debug.Log("Server: FinishedLoadScene");
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var serverFinishedLoad = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(serverFinishedLoad, new ServerReadyToStartTag());
            commandBuffer.Playback(WorldGetter.GetServerWorld().EntityManager);
            // Debug.Log("Server Finished Loading");
        }
    }
    
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct LoadingSystemClient: ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<StartLoadingSceneCommand>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var loadSceneName = new FixedString32Bytes();
            var unloadSceneName = new FixedString32Bytes();
            foreach (var (request, entity) in SystemAPI.Query<RefRO<StartLoadingSceneCommand>>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                loadSceneName = request.ValueRO.LoadSceneName;
                unloadSceneName = request.ValueRO.UnloadSceneName;
                Debug.Log("Loading Command Received");
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            LoadSceneAsync(loadSceneName.ToString(), unloadSceneName.ToString());
        }
        
        
        public async void LoadSceneAsync(string loadScene, string unloadScene)
        {
            UIManager.Singleton.CloseForm<LobbyForm>();
            UIManager.Singleton.ShowForm<LoadingForm>();
            Debug.Log("Client: LoadSceneAsync");
            await SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);
            // await GameSceneManager.SwitchSceneAsync(loadScene, unloadScene)
            await SceneManager.UnloadSceneAsync(unloadScene);

            Debug.Log("Client: FinishedLoadScene");
            var entityManager = WorldGetter.GetClientWorld().EntityManager;
            var finishedLoadingSceneRespond = new FinishedLoadingSceneRespond
            {
                LoadSceneName = loadScene,
                UnloadSceneName = unloadScene
            };
            var loadSceneRpc = entityManager.CreateEntity();
            entityManager.AddComponentData(loadSceneRpc, finishedLoadingSceneRespond);
            entityManager.AddComponentData(loadSceneRpc, new SendRpcCommandRequestComponent
            {
                TargetConnection = default
            });
            // entityManager.Playback(WorldGetter.GetClientWorld().EntityManager);
        }
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ReceiveFinishedLoadingSceneRespondSystem: ISystem
    {
        
        private ComponentLookup<NetworkIdComponent> networkIdFromEntity;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<FinishedLoadingSceneRespond>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state);
            var hasFinishedLoading = false;
            foreach (var (request, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<FinishedLoadingSceneRespond>().WithEntityAccess())
            {
                hasFinishedLoading = true;
                
                foreach (var finishedLoadingComponent in SystemAPI.Query<RefRW<ClientFinishedLoadingSceneComponent>>())
                {
                    if (finishedLoadingComponent.ValueRW.PlayerID == networkIdFromEntity[request.ValueRO.SourceConnection].Value)
                    {
                        finishedLoadingComponent.ValueRW.HasFinishedLoading = true;
                        commandBuffer.AddComponent(request.ValueRO.SourceConnection, new NetworkStreamInGame());
                        Debug.Log($"Client {networkIdFromEntity[request.ValueRO.SourceConnection].Value} Finished Loading");
                    }
                    hasFinishedLoading &= finishedLoadingComponent.ValueRW.HasFinishedLoading;
                }
                commandBuffer.DestroyEntity(entity);
            }
            if (hasFinishedLoading)
            {
                var clientReadyComponent = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(clientReadyComponent, new ClientReadyToStartTag());   
                Debug.Log("All Clients Finished Loading");
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
        
        
        
    }

    public struct StartLoadingSceneCommand : IRpcCommand
    {
        public FixedString32Bytes LoadSceneName;
        public FixedString32Bytes UnloadSceneName;
    }
    
    public struct FinishedLoadingSceneRespond : IRpcCommand
    {
        public FixedString32Bytes LoadSceneName;
        public FixedString32Bytes UnloadSceneName;
    }
    
    public struct StartLoadSceneComponent : IComponentData
    {
        public FixedString32Bytes LoadSceneName;
        public FixedString32Bytes UnloadSceneName;
    }
    
    public struct StartGameCommand : IRpcCommand
    {
        
    }
    
    public struct SceneLoadCompleteComponent : IComponentData
    {
        
    }
    
    public struct ClientFinishedLoadingSceneComponent : IComponentData
    {
        public int PlayerID;
        public bool HasFinishedLoading;
    }
    
    public struct ServerReadyToStartTag : IComponentData
    {
    }
    
    public struct ClientReadyToStartTag : IComponentData
    {
    }
    
}