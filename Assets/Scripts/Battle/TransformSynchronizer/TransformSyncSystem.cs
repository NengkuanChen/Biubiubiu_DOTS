using DefaultNamespace.Battle.TransformSynchronizer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct TransformSyncSystem: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformSyncEntity>()
                .WithNone<TransformSyncEntityInitializeComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            // var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (transformSyncComponent, entity) in SystemAPI.Query<RefRO<TransformSyncComponent>>()
                         .WithNone<TransformSyncEntityInitializeComponent>()
                         .WithAll<EntitySyncFromGameObjectTag>()
                         .WithEntityAccess())
            {
                var uid = transformSyncComponent.ValueRO.UniqueId;
                var index = transformSyncComponent.ValueRO.Index;
                var transform = TransformSyncManager.GetSyncTransform(uid, index);
                if (transform == null)
                {
                    continue;
                }
                var worldPosition = transform.position;
                var worldRotation = transform.rotation;
                var transformComponent = state.EntityManager.GetComponentData<WorldTransform>(entity);
                transformComponent.Position = worldPosition;
                transformComponent.Rotation = worldRotation;
                state.EntityManager.SetComponentData(entity, transformComponent);
            }

            foreach (var (transformSyncComponent, entity) in SystemAPI.Query<RefRO<TransformSyncComponent>>()
                         .WithNone<TransformSyncEntityInitializeComponent>()
                         .WithAll<GameObjectSyncFromEntityTag>()
                         .WithEntityAccess())
            {
                var uid = transformSyncComponent.ValueRO.UniqueId;
                var index = transformSyncComponent.ValueRO.Index;
                var transform = TransformSyncManager.GetSyncTransform(uid, index);
                if (transform == null)
                {
                    continue;
                }
                var worldTransform = state.EntityManager.GetComponentData<WorldTransform>(entity);
                transform.position = worldTransform.Position;
                transform.rotation = worldTransform.Rotation;
            }
        }
    }
    
    
    
    [BurstCompile]
    public partial struct TransformSyncInitializeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformSyncEntity>()
                .WithAll<TransformSyncEntityInitializeComponent>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (syncComponent, entity) 
                     in SystemAPI.Query<RefRW<TransformSyncEntity>>()
                         .WithAll<TransformSyncEntityInitializeComponent>()
                         .WithEntityAccess())
            {
                // var entityGroup = state.EntityManager.GetComponentData<LinkedEntityGroup>(entity);
                var uniqueId = state.EntityManager.GetComponentData<TransformSyncEntityInitializeComponent>(entity)
                    .UniqueId;
                syncComponent.ValueRW.UniqueId = uniqueId;
                var linkedEntityGroup = state.EntityManager.GetBuffer<LinkedEntityGroup>(entity);
                foreach (var linkedEntity in linkedEntityGroup)
                {
                    if (state.EntityManager.HasComponent<TransformSyncComponent>(linkedEntity.Value))
                    {
                        var transformSyncComponent = state.EntityManager.GetComponentData<TransformSyncComponent>(linkedEntity.Value);
                        transformSyncComponent.UniqueId = uniqueId;
                        commandBuffer.SetComponent(linkedEntity.Value, transformSyncComponent);
                        commandBuffer.AddComponent(linkedEntity.Value, new EntitySyncFromGameObjectTag());
                    }
                }
                commandBuffer.RemoveComponent<TransformSyncEntityInitializeComponent>(entity);
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
    }
}