using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

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
            var bulletInitializeJob = new BulletPhysicsPropertyInitializeJob();
            state.Dependency = bulletInitializeJob.Schedule(state.Dependency);
            state.Dependency.Complete();
            // var bulletLifeTimeHandler = new BulletLifeTimeHandler
            // {
            //     commandBuffer = commandBuffer,
            //     DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
            // };
            // state.Dependency = bulletLifeTimeHandler.Schedule(state.Dependency);
            // state.Dependency.Complete();
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
            public EntityCommandBuffer commandBuffer;
            public float DeltaTime;

            void Execute(Entity entity, ref Bullet bullet)
            {
                bullet.LifeTime -= DeltaTime;
                if (bullet.LifeTime <= 0)
                {
                    commandBuffer.DestroyEntity(entity);
                }
            }
        }
    }
    
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [UpdateAfter(typeof(WeaponSystem))]
    public partial struct BulletServerSystem: ISystem
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
            var bulletLifeTimeHandler = new BulletLifeTimeHandler
            {
                commandBuffer = commandBuffer,
                DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
            };
            state.Dependency = bulletLifeTimeHandler.Schedule(state.Dependency);
            state.Dependency.Complete();
        }

        public partial struct BulletLifeTimeHandler : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            public float DeltaTime;

            void Execute(Entity entity, ref Bullet bullet)
            {
                bullet.LifeTime -= DeltaTime;
                if (bullet.LifeTime <= 0)
                {
                    commandBuffer.DestroyEntity(entity);
                }
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct BulletCollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Bullet>();
        }
    
        [BurstCompile]
        public struct BulletCollisionEventHandle : ICollisionEventsJob
        {
            public EntityCommandBuffer commandBuffer;
            public ComponentLookup<Bullet> bulletLookup;
            public PhysicsWorld physicsWorld;
            public PhysicsWorldHistorySingleton physicsWorldHistory;

            public void Execute(CollisionEvent collisionEvent)
            {
                if (bulletLookup.HasComponent(collisionEvent.EntityA))
                {
                    commandBuffer.DestroyEntity(collisionEvent.EntityA);
                }
                if (bulletLookup.HasComponent(collisionEvent.EntityB))
                {
                    commandBuffer.DestroyEntity(collisionEvent.EntityB);
                }
            }
        }
    
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
    
            var entityQuery = state.EntityManager.CreateEntityQuery(typeof(Bullet));
            var networkTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            // state.Dependency = new BulletCollisionEventHandle
            // {
            //     commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
            //         .CreateCommandBuffer(state.WorldUnmanaged),
            //     bulletLookup = state.GetComponentLookup<Bullet>(),
            //     physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            //     physicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
            // }.Schedule(entityQuery, state.Dependency);

        }
    }
}