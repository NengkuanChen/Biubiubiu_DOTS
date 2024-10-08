﻿using System.ComponentModel;
using Battle.Character;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
        private ComponentLookup<CharacterHitBoxComponent> hitBoxLookup;
        private ComponentLookup<NetworkId> networkIdLookup;
        private ComponentLookup<GhostOwner> ghostOwnerLookup;
        private BufferLookup<CharacterHitBoxEntityBuffer> hitBoxEntityBufferLookup;
        private BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPairBufferLookup;
        private ComponentLookup<BulletOwner> bulletOwnerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            bulletLookup = state.GetComponentLookup<Bullet>();
            hitBoxLookup = state.GetComponentLookup<CharacterHitBoxComponent>();
            networkIdLookup = state.GetComponentLookup<NetworkId>(true);
            ghostOwnerLookup = state.GetComponentLookup<GhostOwner>(true);
            hitBoxEntityBufferLookup = state.GetBufferLookup<CharacterHitBoxEntityBuffer>(true);
            physicsColliderKeyEntityPairBufferLookup = state.GetBufferLookup<PhysicsColliderKeyEntityPair>(true);
            bulletOwnerLookup = state.GetComponentLookup<BulletOwner>(true);
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
            hitBoxLookup.Update(ref state);
            networkIdLookup.Update(ref state);
            ghostOwnerLookup.Update(ref state);
            hitBoxEntityBufferLookup.Update(ref state);
            physicsColliderKeyEntityPairBufferLookup.Update(ref state);
            bulletOwnerLookup.Update(ref state);
            
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
            // bulletLifeTimeHandler.Schedule(state.Dependency).Complete();
            bulletLifeTimeHandler.Schedule(state.Dependency).Complete();
            
            //Handle Bullet Collision
            new BulletCollisionEventHandle
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                bulletLookup = bulletLookup,
                hitBoxLookup = hitBoxLookup,
                hitBoxEntityBufferLookup = hitBoxEntityBufferLookup,
                physicsColliderKeyEntityPairBufferLookup = physicsColliderKeyEntityPairBufferLookup,
                bulletOwnerLookup = bulletOwnerLookup,
                IsServer = isServer,
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency).Complete();
            
        }
        
        
        public partial struct BulletSpawningJob : IJobEntity
        {
            public bool IsServer;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            
            [Unity.Collections.ReadOnly]
            public ComponentLookup<GhostOwner> GhostOwnerLookup;

            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, 
                ref DynamicBuffer<BulletSpawnRequestBuffer> spawnRequestBuffer)
            {
                for (int i = 0; i < spawnRequestBuffer.Length; i++)
                {
                    
                    var bullet = CommandBuffer.Instantiate(chunkIndexInQuery, spawnRequestBuffer[i].BulletPrefab);
                    var localTransform = LocalTransform.FromPositionRotation(spawnRequestBuffer[i].Position,
                        spawnRequestBuffer[i].Rotation);
                    //Compute Spread
                    WeaponUtility.ComputeBulletSpreadRotation(ref localTransform, spawnRequestBuffer[i].SpreadAngleRotX,
                        spawnRequestBuffer[i].SpreadAngleRotZ);
                    CommandBuffer.SetComponent(chunkIndexInQuery, bullet, localTransform);
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
                        CommandBuffer.SetComponent(chunkIndexInQuery, bullet, new GhostOwner
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
        public struct BulletCollisionEventHandle : ITriggerEventsJob
        {
            public EntityCommandBuffer commandBuffer;
            public ComponentLookup<Bullet> bulletLookup;
            [Unity.Collections.ReadOnly]
            public ComponentLookup<BulletOwner> bulletOwnerLookup;
            public ComponentLookup<CharacterHitBoxComponent> hitBoxLookup;
            public BufferLookup<CharacterHitBoxEntityBuffer> hitBoxEntityBufferLookup;
            public BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPairBufferLookup;
            // public ComponentLookup<FirstPersonPlayer> firstPersonPlayerLookup;
            // public ComponentLookup<FirstPersonCharacterComponent> firstPersonCharacterLookup;
            public bool IsServer;

            // public void Execute(CollisionEvent collisionEvent)
            // {
            //     
            //     if (bulletLookup.TryGetComponent(collisionEvent.EntityA, out var bullet))
            //     {
            //         if (IsServer)
            //         {
            //             if (hitBoxEntityBufferLookup.TryGetBuffer(collisionEvent.EntityB, out var hitBoxEntityBuffers))
            //             {
            //                 if (physicsColliderKeyEntityPairBufferLookup.TryGetBuffer(collisionEvent.EntityB, 
            //                         out var physicsColliderKeyEntityPairBuffers))
            //                 {
            //                     foreach (var keyEntityPair in physicsColliderKeyEntityPairBuffers)
            //                     {
            //                         if (keyEntityPair.Key.Value == collisionEvent.ColliderKeyB)
            //                         {
            //                             if (hitBoxLookup.TryGetComponent(keyEntityPair.Entity, out var hitBox))
            //                             {
            //                                 var bulletOwner = bulletOwnerLookup[collisionEvent.EntityA];
            //                                 commandBuffer.AddComponent(collisionEvent.EntityB, new BulletDamageCleanUp
            //                                 {
            //                                     DamageCaused = bullet.Damage,
            //                                     DamageMultiplier = hitBox.DamageMultiplier,
            //                                     CausedByCharacter = bulletOwner.OwnerCharacter,
            //                                     CausedByPlayer = bulletOwner.OwnerPlayer,
            //                                     CausedByWeapon = bulletOwner.OwnerWeapon,
            //                                     DamagedCharacter = collisionEvent.EntityB
            //                                 });
            //                             }
            //                             break;
            //                         }
            //                     }
            //                 }
            //             }
            //             
            //             commandBuffer.DestroyEntity(collisionEvent.EntityA);
            //         }
            //     }
            //     else if (bulletLookup.TryGetComponent(collisionEvent.EntityB, out var bullet1))
            //     {
            //         if (IsServer)
            //         {
            //             if (hitBoxEntityBufferLookup.TryGetBuffer(collisionEvent.EntityA, out var hitBoxBuffer))
            //             {
            //                 if (physicsColliderKeyEntityPairBufferLookup.TryGetBuffer(collisionEvent.EntityA,
            //                         out var physicsColliderKeyEntityPairBuffers))
            //                 {
            //                     foreach (var keyEntityPair in physicsColliderKeyEntityPairBuffers)
            //                     {
            //                         if (keyEntityPair.Key.Value == collisionEvent.ColliderKeyA)
            //                         {
            //                             if (hitBoxLookup.TryGetComponent(keyEntityPair.Entity, out var hitBox))
            //                             {
            //                                 var bulletOwner = bulletOwnerLookup[collisionEvent.EntityB];
            //                                 commandBuffer.AddComponent(collisionEvent.EntityA, new BulletDamageCleanUp
            //                                 {
            //                                     DamageCaused = bullet.Damage,
            //                                     DamageMultiplier = hitBox.DamageMultiplier,
            //                                     CausedByCharacter = bulletOwner.OwnerCharacter,
            //                                     CausedByPlayer = bulletOwner.OwnerPlayer,
            //                                     CausedByWeapon = bulletOwner.OwnerWeapon,
            //                                     DamagedCharacter = collisionEvent.EntityA
            //                                 });
            //                             }
            //                             break;
            //                         }
            //                     }
            //                 }
            //             }
            //             commandBuffer.DestroyEntity(collisionEvent.EntityB);
            //         }
            //     }
            // }

            public void Execute(TriggerEvent triggerEvent)
            {
                if (!IsServer)
                {
                    return;
                }
                if (bulletLookup.TryGetComponent(triggerEvent.EntityA, out var bullet))
                {
                    bool isSelfCollider = false;
                    if (hitBoxEntityBufferLookup.HasBuffer(triggerEvent.EntityB))
                    {
                        if (physicsColliderKeyEntityPairBufferLookup.TryGetBuffer(triggerEvent.EntityB, 
                                out var physicsColliderKeyEntityPairBuffers))
                        {

                            if (triggerEvent.EntityB.Index == bulletOwnerLookup[triggerEvent.EntityA].OwnerCharacter.Index)
                            {
                                isSelfCollider = true;
                            }
                            else
                            {
                                foreach (var keyEntityPair in physicsColliderKeyEntityPairBuffers)
                                {
                                    if (keyEntityPair.Key == triggerEvent.ColliderKeyB)
                                    {
                                        if (hitBoxLookup.TryGetComponent(keyEntityPair.Entity, out var hitBox))
                                        {
                                            var bulletOwner = bulletOwnerLookup[triggerEvent.EntityA];
                                            commandBuffer.AddComponent(triggerEvent.EntityB, new BulletDamageCleanUp
                                            {
                                                DamageCaused = bullet.Damage,
                                                DamageMultiplier = hitBox.DamageMultiplier,
                                                CausedByCharacter = bulletOwner.OwnerCharacter,
                                                CausedByPlayer = bulletOwner.OwnerPlayer,
                                                CausedByWeapon = bulletOwner.OwnerWeapon,
                                                DamagedCharacter = triggerEvent.EntityB 
                                            });
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!isSelfCollider)
                    {
                        commandBuffer.DestroyEntity(triggerEvent.EntityA);
                    }
                }
                else if (bulletLookup.TryGetComponent(triggerEvent.EntityB, out var bullet1))
                {
                    bool isSelfCollider = false;
                    if (hitBoxEntityBufferLookup.HasBuffer(triggerEvent.EntityA))
                    {
                        if (physicsColliderKeyEntityPairBufferLookup.TryGetBuffer(triggerEvent.EntityA,
                                out var physicsColliderKeyEntityPairBuffers))
                        {
                            if (triggerEvent.EntityA.Index == bulletOwnerLookup[triggerEvent.EntityB].OwnerCharacter.Index)
                            {
                                isSelfCollider = true;
                            }
                            else
                            {
                                foreach (var keyEntityPair in physicsColliderKeyEntityPairBuffers)
                                {
                                    if (keyEntityPair.Key == triggerEvent.ColliderKeyA)
                                    {
                                        if (hitBoxLookup.TryGetComponent(keyEntityPair.Entity, out var hitBox))
                                        {
                                            var bulletOwner = bulletOwnerLookup[triggerEvent.EntityB];
                                            commandBuffer.AddComponent(triggerEvent.EntityA, new BulletDamageCleanUp
                                            {
                                                DamageCaused = bullet1.Damage,
                                                DamageMultiplier = hitBox.DamageMultiplier,
                                                CausedByCharacter = bulletOwner.OwnerCharacter,
                                                CausedByPlayer = bulletOwner.OwnerPlayer,
                                                CausedByWeapon = bulletOwner.OwnerWeapon,
                                                DamagedCharacter = triggerEvent.EntityA
                                            });
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!isSelfCollider)
                    {
                        commandBuffer.DestroyEntity(triggerEvent.EntityB);
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
            if (SystemAPI.HasSingleton<NetworkId>())
            {
                localNetId = SystemAPI.GetSingleton<NetworkId>().Value;
            }

            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            // new ClientBulletSpawnJob
            // {
            //     CommandBuffer = commandBuffer.AsParallelWriter(),
            // }.ScheduleParallel(state.Dependency).Complete();
        }
        
        // [BurstCompile]
        // public partial struct ClientBulletSpawnJob : IJobEntity
        // {
        //     public EntityCommandBuffer.ParallelWriter CommandBuffer;
        //     
        //     void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, 
        //         ref DynamicBuffer<BulletSpawnVisualRequestBuffer> spawnVisualRequestBuffer)
        //     {
        //         for (int i = 0; i < spawnVisualRequestBuffer.Length; i++)
        //         {
        //             var bullet = CommandBuffer.Instantiate(chunkIndexInQuery, spawnVisualRequestBuffer[i].BulletVisualPrefab);
        //             CommandBuffer.SetComponent(chunkIndexInQuery, bullet, LocalTransform.FromPositionRotation( 
        //                 spawnVisualRequestBuffer[i].Position, spawnVisualRequestBuffer[i].Rotation));
        //             CommandBuffer.SetComponent(chunkIndexInQuery, bullet, new BulletOwner()
        //             {
        //                 OwnerCharacter = spawnVisualRequestBuffer[i].OwnerCharacter,
        //                 OwnerPlayer = spawnVisualRequestBuffer[i].OwnerPlayer,
        //                 OwnerWeapon = spawnVisualRequestBuffer[i].OwnerWeapon,
        //             });
        //         }
        //         spawnVisualRequestBuffer.Clear();
        //     }
        // }
    }
}