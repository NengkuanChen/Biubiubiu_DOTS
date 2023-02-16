using DefaultNamespace.Battle.TransformSynchronizer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Battle.TransformSynchronizer
{
    [BurstCompile]
    public partial struct TransformSyncSystem: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformSyncEntity>()
                .WithNone<TransformSyncEntityInitializerComponent>()
                .WithAll<EntitySyncFromGameObjectTag>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            // var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (transformSyncComponent, entity) in SystemAPI.Query<RefRO<TransformSyncComponent>>()
                         .WithAll<TransformSyncEntity>()
                         .WithNone<TransformSyncEntityInitializerComponent>()
                         .WithAll<EntitySyncFromGameObjectTag>()
                         .WithEntityAccess())
            {
                var transform = TransformSyncManager.GetSyncTransform(transformSyncComponent.ValueRO.UniqueId, transformSyncComponent.ValueRO.Index);
                if (transform == null)
                {
                    continue;
                }
                var globalPosition = transform.position;
                var globalRotation = transform.rotation;
                var transformComponent = state.EntityManager.GetComponentData<WorldTransform>(entity);
                transformComponent.Position = globalPosition;
                transformComponent.Rotation = globalRotation;
                state.EntityManager.SetComponentData(entity, transformComponent);
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
                .WithAll<TransformSyncEntityInitializerComponent>();
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
                         .WithAll<TransformSyncEntityInitializerComponent>()
                         .WithEntityAccess())
            {
                // var entityGroup = state.EntityManager.GetComponentData<LinkedEntityGroup>(entity);
                var uniqueId = state.EntityManager.GetComponentData<TransformSyncEntityInitializerComponent>(entity)
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
                    }
                }
                commandBuffer.RemoveComponent<TransformSyncEntityInitializerComponent>(entity);
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
    }
}