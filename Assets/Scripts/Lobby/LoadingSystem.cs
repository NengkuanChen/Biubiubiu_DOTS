using Cysharp.Threading.Tasks;
using DefaultNamespace;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

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
            foreach (var (StartLoadSceneComponent, entity) in SystemAPI.Query<RefRO<StartLoadSceneComponent>>().WithEntityAccess())
            {
                var loadSceneName = StartLoadSceneComponent.ValueRO.LoadSceneName;
                var unloadSceneName = StartLoadSceneComponent.ValueRO.UnloadSceneName;
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
                commandBuffer.DestroyEntity(entity);
                LoadSceneAsync(loadSceneName.ToString(), unloadSceneName.ToString()).Forget();
            }
            commandBuffer.Playback(state.EntityManager);
        }

        public async UniTaskVoid LoadSceneAsync(string loadScene, string unloadScene)
        {
            await GameSceneManager.SwitchSceneAsync(loadScene, unloadScene);
        }
    }
    
    [BurstCompile]
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
            foreach (var (request, entity) in SystemAPI.Query<RefRO<StartLoadingSceneCommand>>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                LoadSceneAsync(request.ValueRO.LoadSceneName.ToString(), request.ValueRO.UnloadSceneName.ToString()).Forget();
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
        public async UniTaskVoid LoadSceneAsync(string loadScene, string unloadScene)
        {
            await GameSceneManager.SwitchSceneAsync(loadScene, unloadScene);
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var finishedLoadingSceneRespond = new FinishedLoadingSceneRespond
            {
                LoadSceneName = loadScene,
                UnloadSceneName = unloadScene
            };
            var loadSceneRpc = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(loadSceneRpc, finishedLoadingSceneRespond);
            commandBuffer.AddComponent(loadSceneRpc, new SendRpcCommandRequestComponent
            {
                TargetConnection = default
            });
            commandBuffer.Playback(WorldGetter.GetClientWorld().EntityManager);
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
    
}