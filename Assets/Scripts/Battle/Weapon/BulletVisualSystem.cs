using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Battle.Weapon
{
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(PostPredictionPreTransformsECBSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    public partial struct BulletVisualSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponBulletVisualComponent>();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            new BulletVisualSpawnJob()
            {
                CommandBuffer = commandBuffer.AsParallelWriter()
            }.ScheduleParallel(state.Dependency).Complete();
            
            new BulletVisualMovementJob()
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer.AsParallelWriter()
            }.ScheduleParallel(state.Dependency).Complete();
        }


        [BurstCompile]
        public partial struct BulletVisualSpawnJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery,
                ref DynamicBuffer<BulletSpawnVisualRequestBuffer> spawnVisualRequestBuffer)
            {
                foreach (var spawnVisualRequest in spawnVisualRequestBuffer)
                {
                    if (spawnVisualRequest.BulletVisualPrefab == Entity.Null)
                    {
                        continue;
                    }

                    var bulletVisual =
                        CommandBuffer.Instantiate(chunkIndexInQuery, spawnVisualRequest.BulletVisualPrefab);
                    CommandBuffer.SetComponent(chunkIndexInQuery, bulletVisual, spawnVisualRequest.LocalTransform);
                    CommandBuffer.SetComponent(chunkIndexInQuery, bulletVisual, new BulletVisualMovementDataComponent
                    {
                        IsHit = spawnVisualRequest.IsHit,
                        DistanceTraveled = 0,
                        MaxDistance = math.distance(spawnVisualRequest.HitPosition,
                            spawnVisualRequest.LocalTransform.Position),
                    });
                }
                spawnVisualRequestBuffer.Clear();
            }
        }
        
        [BurstCompile]
        public partial struct BulletVisualMovementJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            
            
            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery,
                ref BulletVisualMovementDataComponent bulletVisualMovementDataComponent,
                ref BulletVisualComponent bulletVisualComponent, ref LocalTransform localTransform)
            {
                var maxDistance = bulletVisualMovementDataComponent.IsHit
                    ? bulletVisualMovementDataComponent.MaxDistance
                    : bulletVisualComponent.MaxDistance;
                bulletVisualMovementDataComponent.DistanceTraveled += bulletVisualComponent.Speed * DeltaTime;
                localTransform.Position += localTransform.Forward() * bulletVisualComponent.Speed * DeltaTime;
                if (bulletVisualMovementDataComponent.DistanceTraveled >= maxDistance)
                {
                    CommandBuffer.DestroyEntity(chunkIndexInQuery, entity);
                }
            }
        }
        
        
    }
}