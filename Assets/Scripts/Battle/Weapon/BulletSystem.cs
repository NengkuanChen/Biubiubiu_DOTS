using System.ComponentModel;
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
    public partial struct ServerBulletSystem : ISystem
    {
        private ComponentLookup<Bullet> bulletLookup;
        private ComponentLookup<Health> healthLookup;
        private ComponentLookup<NetworkIdComponent> networkIdLookup;
        private ComponentLookup<GhostOwnerComponent> ghostOwnerLookup;

        public void OnCreate(ref SystemState state)
        {
            //
            // state.RequireForUpdate<Bullet>();
            // bulletLookup = state.GetComponentLookup<Bullet>();
            state.RequireForUpdate<WeaponComponent>();
            bulletLookup = state.GetComponentLookup<Bullet>();
            healthLookup = state.GetComponentLookup<Health>();
            networkIdLookup = state.GetComponentLookup<NetworkIdComponent>(true);
            ghostOwnerLookup = state.GetComponentLookup<GhostOwnerComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            
            bulletLookup.Update(ref state);
            healthLookup.Update(ref state);
            networkIdLookup.Update(ref state);
            ghostOwnerLookup.Update(ref state);
            
            //Spawn Bullet
            var bulletSpawningJob = new BulletSpawningJob
            {
                CommandBuffer = commandBuffer.AsParallelWriter(),
                IsServer = state.WorldUnmanaged.IsServer(),
                GhostOwnerLookup = ghostOwnerLookup,
            };
            state.Dependency = bulletSpawningJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            
            
            
            //Initialize Bullet Physics Property
            var bulletInitializeJob = new BulletPhysicsPropertyInitializeJob();
            var isServer = state.WorldUnmanaged.IsServer();
            bulletInitializeJob.ScheduleParallel(state.Dependency).Complete();

            //Handle Bullet LifeTime
            var bulletLifeTimeHandler = new BulletLifeTimeHandler
            {
                commandBuffer = commandBuffer,
                DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
                IsServer = isServer,
            };
            bulletLifeTimeHandler.Schedule(state.Dependency).Complete();

            
            //Handle Bullet Collision
            new BulletCollisionEventHandle
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                bulletLookup = bulletLookup,
                healthLookup = healthLookup,
                IsServer = isServer,
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency).Complete();
            
        }
        
        
        public partial struct BulletSpawningJob : IJobEntity
        {
            public bool IsServer;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            
            [Unity.Collections.ReadOnly]
            public ComponentLookup<GhostOwnerComponent> GhostOwnerLookup;

            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, 
                ref DynamicBuffer<BulletSpawnRequestBuffer> spawnRequestBuffer)
            {
                for (int i = 0; i < spawnRequestBuffer.Length; i++)
                {
                    var bullet = CommandBuffer.Instantiate(chunkIndexInQuery, spawnRequestBuffer[i].BulletPrefab);
                    CommandBuffer.SetComponent(chunkIndexInQuery, bullet,
                        LocalTransform.FromPositionRotation(spawnRequestBuffer[i].Position,
                            spawnRequestBuffer[i].Rotation));
                    CommandBuffer.SetComponent(chunkIndexInQuery, bullet, new BulletOwner()
                    {
                        OwnerCharacter = spawnRequestBuffer[i].OwnerCharacter,
                        OwnerPlayer = spawnRequestBuffer[i].OwnerPlayer,
                        OwnerWeapon = spawnRequestBuffer[i].OwnerWeapon,
                    });
                    if (spawnRequestBuffer[i].IsGhost)
                    {
                        var NetworkId = -1;
                        if (IsServer)
                        {
                            NetworkId = GhostOwnerLookup[spawnRequestBuffer[i].OwnerCharacter].NetworkId;
                        }
                        CommandBuffer.SetComponent(chunkIndexInQuery, bullet, new GhostOwnerComponent
                        {
                            NetworkId = NetworkId,
                        });
                    }
                }
                spawnRequestBuffer.Clear();
            }
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
            public bool IsServer;

            void Execute(Entity entity, ref Bullet bullet)
            {
                bullet.LifeTime -= DeltaTime;
                if (bullet.LifeTime <= 0)
                {
                    //todo: Problem here
                    if (IsServer)
                    {
                        commandBuffer.DestroyEntity(entity);
                    }
                }
            }
        }
        
        
        [BurstCompile]
        public struct BulletCollisionEventHandle : ICollisionEventsJob
        {
            public EntityCommandBuffer commandBuffer;
            public ComponentLookup<Bullet> bulletLookup;
            public ComponentLookup<Health> healthLookup;
            // public ComponentLookup<FirstPersonPlayer> firstPersonPlayerLookup;
            // public ComponentLookup<FirstPersonCharacterComponent> firstPersonCharacterLookup;
            public bool IsServer;

            public void Execute(CollisionEvent collisionEvent)
            {
                if (bulletLookup.TryGetComponent(collisionEvent.EntityA, out var bullet))
                {
                    if (IsServer)
                    {
                        if (healthLookup.TryGetComponent(collisionEvent.EntityB, out var health))
                        {
                            health.CurrentHealth -= bullet.Damage;
                            healthLookup[collisionEvent.EntityB] = health;
                        }
                        commandBuffer.DestroyEntity(collisionEvent.EntityA);
                    }
                }
                else if (bulletLookup.TryGetComponent(collisionEvent.EntityB, out var bullet1))
                {
                    if (IsServer)
                    {
                        if (healthLookup.TryGetComponent(collisionEvent.EntityA, out var health))
                        {
                            health.CurrentHealth -= bullet1.Damage;
                            healthLookup[collisionEvent.EntityA] = health;
                        }
                        commandBuffer.DestroyEntity(collisionEvent.EntityB);
                    }
                }
            }
        }
        
    }
    
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct ClientBulletVisualSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
        }


        public void OnUpdate(ref SystemState state)
        {
            int localNetId = -1;
            if (SystemAPI.HasSingleton<NetworkIdComponent>())
            {
                localNetId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;
            }

            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            new ClientBulletSpawnJob
            {
                CommandBuffer = commandBuffer.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency).Complete();
        }
        
        [BurstCompile]
        public partial struct ClientBulletSpawnJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            
            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, 
                ref DynamicBuffer<BulletSpawnVisualRequestBuffer> spawnVisualRequestBuffer)
            {
                for (int i = 0; i < spawnVisualRequestBuffer.Length; i++)
                {
                    var bullet = CommandBuffer.Instantiate(chunkIndexInQuery, spawnVisualRequestBuffer[i].BulletVisualPrefab);
                    CommandBuffer.SetComponent(chunkIndexInQuery, bullet, LocalTransform.FromPositionRotation( 
                        spawnVisualRequestBuffer[i].Position, spawnVisualRequestBuffer[i].Rotation));
                    CommandBuffer.SetComponent(chunkIndexInQuery, bullet, new BulletOwner()
                    {
                        OwnerCharacter = spawnVisualRequestBuffer[i].OwnerCharacter,
                        OwnerPlayer = spawnVisualRequestBuffer[i].OwnerPlayer,
                        OwnerWeapon = spawnVisualRequestBuffer[i].OwnerWeapon,
                    });
                }
                spawnVisualRequestBuffer.Clear();
            }
        }
    }
}