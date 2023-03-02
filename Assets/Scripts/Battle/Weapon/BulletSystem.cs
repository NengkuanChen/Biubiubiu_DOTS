using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Battle.Weapon
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [UpdateAfter(typeof(WeaponSystem))]
    [BurstCompile]
    public partial struct BulletSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Bullet>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            //Initialize Bullet Physics Property
            var bulletInitializeJob = new BulletPhysicsPropertyInitializeJob();
            var isServer = state.WorldUnmanaged.IsServer();
            state.Dependency = bulletInitializeJob.Schedule(state.Dependency);
            state.Dependency.Complete();
            
            //Handle Bullet LifeTime
            var bulletLifeTimeHandler = new BulletLifeTimeHandler
            {
                commandBuffer = commandBuffer.AsParallelWriter(),
                DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
                IsServer = isServer,
            };
            state.Dependency = bulletLifeTimeHandler.Schedule(state.Dependency);
            state.Dependency.Complete();
            //Handle Bullet Collision
            state.Dependency = new BulletCollisionEventHandle
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                bulletLookup = state.GetComponentLookup<Bullet>(),
                IsServer = isServer,
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
            state.Dependency.Complete();
        }
        
        public partial struct BulletPhysicsPropertyInitializeJob : IJobEntity
        {
            void Execute(Entity entity, ref BulletInitialPhysicsPropertyComponent physicsProperty, 
                ref LocalTransform localTransform, ref PhysicsVelocity physicsVelocity)
            {
                if (!physicsProperty.HasInitialized)
                {
                    physicsVelocity.Linear = localTransform.Forward() * physicsProperty.InitialSpeed;
                    physicsProperty.HasInitialized = true;
                }
            }
        }
        
        public partial struct BulletLifeTimeHandler : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter commandBuffer;
            public float DeltaTime;
            public bool IsServer;

            void Execute(Entity entity,[ChunkIndexInQuery] int chunkIndexInQuery, ref Bullet bullet)
            {
                bullet.LifeTime -= DeltaTime;
                if (bullet.LifeTime <= 0)
                {
                    if (IsServer)
                    {
                        commandBuffer.DestroyEntity(chunkIndexInQuery, entity);
                    }
                }
            }
        }
        
        
        [BurstCompile]
        public struct BulletCollisionEventHandle : ICollisionEventsJob
        {
            public EntityCommandBuffer commandBuffer;
            public ComponentLookup<Bullet> bulletLookup;
            public bool IsServer;

            public void Execute(CollisionEvent collisionEvent)
            {
                if (bulletLookup.HasComponent(collisionEvent.EntityA))
                {
                    if (IsServer)
                    {
                        commandBuffer.DestroyEntity(collisionEvent.EntityA);

                    }
                }
                if (bulletLookup.HasComponent(collisionEvent.EntityB))
                {
                    if (IsServer)
                    {
                        commandBuffer.DestroyEntity(collisionEvent.EntityB);

                    }
                }
            }
        }

        
    }
    
    // [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    // [BurstCompile]
    // [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    // [UpdateAfter(typeof(WeaponSystem))]
    // public partial struct BulletServerSystem: ISystem
    // {
    //     public void OnCreate(ref SystemState state)
    //     {
    //         state.RequireForUpdate<Bullet>();
    //     }
    //
    //     public void OnDestroy(ref SystemState state)
    //     {
    //         
    //     }
    //
    //     public void OnUpdate(ref SystemState state)
    //     {
    //         var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
    //             .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
    //         var bulletLifeTimeHandler = new BulletLifeTimeHandler
    //         {
    //             commandBuffer = commandBuffer,
    //             DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
    //         };
    //         state.Dependency = bulletLifeTimeHandler.ScheduleParallel(state.Dependency);
    //         state.Dependency.Complete();
    //     }
    //
    //    
    // }

    // [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    // [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    // [UpdateAfter(typeof(WeaponSystem))]
    // public partial struct BulletCollisionSystem : ISystem
    // {
    //     public void OnCreate(ref SystemState state)
    //     {
    //         state.RequireForUpdate<Bullet>();
    //     }
    //
    //     
    //     [BurstCompile]
    //     public void OnUpdate(ref SystemState state)
    //     {
    //         state.Dependency.Complete();
    //         
    //     }
    // }
}