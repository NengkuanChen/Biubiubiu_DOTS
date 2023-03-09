using Battle.Character;
using Rival;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Battle.Weapon
{


    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PredictedFixedStepTransformsUpdateSystem))]
    public partial class WeaponPredictionUpdateGroup : ComponentSystemGroup
    {
    }



    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct WeaponSystem : ISystem
    {

        private ComponentLookup<LocalToWorld> localToWorldLookup;

        private ComponentLookup<FirstPersonCharacterComponent> characterComponentLookup;

        private BufferLookup<SpreadInfoBuffer> spreadInfoBufferLookup;

        private ComponentLookup<StoredKinematicCharacterData> storedKinematicCharacterDataLookup;

        private BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPair;

        private ComponentLookup<CharacterHitBoxComponent> characterHitBoxComponentLookup;

        private BufferLookup<BulletSpawnVisualRequestBuffer> bulletSpawnVisualRequestBufferLookup;

        private NativeList<RaycastHit> hits;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            state.RequireForUpdate<WeaponOwnerComponent>();
            localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            characterComponentLookup = state.GetComponentLookup<FirstPersonCharacterComponent>(true);
            spreadInfoBufferLookup = state.GetBufferLookup<SpreadInfoBuffer>();
            storedKinematicCharacterDataLookup = state.GetComponentLookup<StoredKinematicCharacterData>(true);
            physicsColliderKeyEntityPair = state.GetBufferLookup<PhysicsColliderKeyEntityPair>();
            characterHitBoxComponentLookup = state.GetComponentLookup<CharacterHitBoxComponent>(true);
            bulletSpawnVisualRequestBufferLookup = state.GetBufferLookup<BulletSpawnVisualRequestBuffer>();
            hits = new NativeList<RaycastHit>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            hits.Dispose();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            characterComponentLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
            spreadInfoBufferLookup.Update(ref state);
            characterHitBoxComponentLookup.Update(ref state);
            bulletSpawnVisualRequestBufferLookup.Update(ref state);
            storedKinematicCharacterDataLookup.Update(ref state);
            physicsColliderKeyEntityPair.Update(ref state);
            WeaponFiringRegistrationJob registrationJob = new WeaponFiringRegistrationJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            state.Dependency = registrationJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            WeaponMagazineHandlingComponentJob magazineHandlingJob = new WeaponMagazineHandlingComponentJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            state.Dependency = magazineHandlingJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();


            // WeaponSpreadJob spreadJob = new WeaponSpreadJob()
            // {
            //     DeltaTime = SystemAPI.Time.DeltaTime
            // };
            // spreadJob.ScheduleParallel(state.Dependency).Complete();
            foreach (var (weaponFiringComponent, spreadComponent,
                         spreadInfoBuffer,
                         entity) in SystemAPI.Query<RefRW<WeaponFiringComponent>,
                         RefRW<WeaponSpreadComponent>, DynamicBuffer<SpreadInfoBuffer>>().WithEntityAccess())
            {
                // spreadInfoBufferLookup.TryGetBuffer(entity, out DynamicBuffer<SpreadInfoBuffer> spreadInfoBuffer);
                spreadInfoBuffer.Clear();
                for (int i = 0; i < weaponFiringComponent.ValueRW.TickBulletsCounter; i++)
                {
                    WeaponUtility.ComputeSpread(ref spreadComponent.ValueRW, out float spreadX, out float spreadY);
                    spreadInfoBuffer.Add(new SpreadInfoBuffer
                    {
                        SpreadAngleRotX = spreadX,
                        SpreadAngleRotZ = spreadY
                    });
                }

                WeaponUtility.GetSpreadDecreaseRate(spreadComponent.ValueRW.SpreadTypeIndex, out var decreaseRate);
                spreadComponent.ValueRW.SpreadPercentage -=
                    SystemAPI.Time.DeltaTime * decreaseRate;
                spreadComponent.ValueRW.SpreadPercentage = math.clamp(spreadComponent.ValueRW.SpreadPercentage, 0, 1);
            }



            WeaponBulletSpawningRequestJob bulletSpawningRequestJob = new WeaponBulletSpawningRequestJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer.AsParallelWriter(),
                LocalToWorldLookup = localToWorldLookup,
                CharacterComponentLookUp = characterComponentLookup,
                IsServer = state.WorldUnmanaged.IsServer(),
                SpreadInfoBufferLookup = spreadInfoBufferLookup
            };
            state.Dependency = bulletSpawningRequestJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();


            // HandleRaycastWeaponShot(state.WorldUnmanaged.IsServer(),
            //     SystemAPI.GetSingleton<NetworkTime>(), SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            //     SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(), localToWorldLookup,
            //     storedKinematicCharacterDataLookup,
            //     characterComponentLookup, characterHitBoxComponentLookup, physicsColliderKeyEntityPair,
            //     bulletSpawnVisualRequestBufferLookup,
            //     ref hits, commandBuffer, ref state);
            new RaycastWeaponShotJob()
            {
                RaycastHits = hits,
                CommandBuffer = commandBuffer,
                LocalToWorldLookup = localToWorldLookup,
                CharacterComponentLookUp = characterComponentLookup,
                IsServer = state.WorldUnmanaged.IsServer(),
                StoredKinematicCharacterDataLookup = storedKinematicCharacterDataLookup,
                PhysicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
                PhysicsColliderKeyEntityPairBufferLookup = physicsColliderKeyEntityPair,
                CharacterHitBoxComponentLookUp = characterHitBoxComponentLookup,
                PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                NetworkTime = SystemAPI.GetSingleton<NetworkTime>(),
            }.Schedule(state.Dependency).Complete();
            
        }

        [BurstCompile]
        public partial struct WeaponFiringRegistrationJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            [BurstCompile]
            void Execute(Entity entity, ref WeaponFiringComponent firingComponent,
                ref WeaponControlComponent weaponControl, ref WeaponComponent weaponComponent,
                in GhostOwnerComponent ghostOwner)
            {
                firingComponent.TickShotTimer += DeltaTime;
                firingComponent.TickBulletsCounter = 0;
                if (weaponControl.FirePressed)
                {
                    // Detect starting to fire, reset round shot timer
                    if (!firingComponent.IsFiring)
                    {
                        firingComponent.RoundBulletsCounter = 0;
                        firingComponent.RoundShotTimer = 0;
                    }

                    firingComponent.IsFiring = true;
                }

                if (weaponComponent.FireInterval > 0)
                {
                    firingComponent.TickShotTimer = math.clamp(firingComponent.TickShotTimer, 0,
                        math.max(weaponComponent.FireInterval + 0.01f, DeltaTime));
                    while (firingComponent.IsFiring && firingComponent.TickShotTimer > weaponComponent.FireInterval)
                    {
                        firingComponent.TickBulletsCounter++;
                        firingComponent.RoundBulletsCounter++;
                        firingComponent.RoundShotTimer += weaponComponent.FireInterval;

                        // Consume shoot time
                        firingComponent.TickShotTimer -= weaponComponent.FireInterval;

                        // Stop firing after initial shot for non-auto fire
                        if (!weaponComponent.FullAuto)
                        {
                            firingComponent.IsFiring = false;
                        }
                    }
                }

                if (weaponControl.FireReleased || !weaponComponent.FullAuto)
                {
                    firingComponent.IsFiring = false;
                }
            }
        }

        [BurstCompile]
        public partial struct WeaponMagazineHandlingComponentJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            [BurstCompile]
            void Execute(Entity entity, ref WeaponFiringComponent weaponFiringComponent,
                ref WeaponMagazineComponent magazine, ref WeaponReloadComponent reloadComponent,
                ref WeaponControlComponent weaponControl, in GhostOwnerComponent ghostOwner)
            {
                if (reloadComponent.IsReloading)
                {
                    weaponFiringComponent.IsFiring = false;
                    weaponFiringComponent.TickBulletsCounter = 0;
                    weaponFiringComponent.RoundShotTimer = 0;
                    weaponFiringComponent.RoundBulletsCounter = 0;
                    reloadComponent.ReloadTimeLeft -= DeltaTime;
                    if (reloadComponent.ReloadTimeLeft <= 0)
                    {
                        FinishedReload(entity, ref magazine, ref reloadComponent);
                    }
                }
                else
                {
                    if (magazine.MagazineRestBullet <= 0 || (weaponControl.ReloadPressed &&
                                                             magazine.MagazineRestBullet < magazine.MagazineSize))
                    {
                        Reload(entity, ref magazine, ref reloadComponent);
                    }

                    weaponFiringComponent.TickBulletsCounter = math.min(weaponFiringComponent.TickBulletsCounter,
                        magazine.MagazineRestBullet);
                    magazine.MagazineRestBullet -= weaponFiringComponent.TickBulletsCounter;
                }


            }

            private void Reload(Entity entity, ref WeaponMagazineComponent magazine,
                ref WeaponReloadComponent reloadComponent)
            {
                reloadComponent.IsReloading = true;
                reloadComponent.ReloadTimeLeft = magazine.ReloadTime;
            }

            private void FinishedReload(Entity entity, ref WeaponMagazineComponent magazine,
                ref WeaponReloadComponent reloadComponent)
            {
                reloadComponent.IsReloading = false;
                magazine.MagazineRestBullet = magazine.MagazineSize;
            }
        }


        [BurstCompile]
        public partial struct WeaponSpreadJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(Entity entity, ref WeaponFiringComponent weaponFiringComponent,
                ref WeaponSpreadComponent spreadComponent, ref DynamicBuffer<SpreadInfoBuffer> spreadInfoBuffer)
            {
                // spreadInfoBuffer.Clear();
                // for (int i = 0; i < weaponFiringComponent.TickBulletsCounter; i++)
                // {
                //     WeaponUtility.CompoteSpread(ref spreadComponent, out float spreadX, out float spreadY);
                //     spreadInfoBuffer.Add(new SpreadInfoBuffer
                //     {
                //         SpreadAngleRotX = spreadX,
                //         SpreadAngleRotZ = spreadY
                //     });
                // }
                //
                // spreadComponent.SpreadPercentage -=
                //     DeltaTime * WeaponUtility.GetSpreadDecreaseRate(spreadComponent.SpreadTypeIndex);
                // spreadComponent.SpreadPercentage = math.clamp(spreadComponent.SpreadPercentage, 0, 1);
            }
        }


        [BurstCompile]
        public partial struct WeaponBulletSpawningRequestJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            [ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> CharacterComponentLookUp;

            [ReadOnly] public BufferLookup<SpreadInfoBuffer> SpreadInfoBufferLookup;

            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            [ReadOnly] public bool IsServer;

            [BurstCompile]
            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery,
                ref WeaponFiringComponent weaponFiringComponent,
                ref WeaponBulletComponent weaponBulletComponent,
                ref WeaponOwnerComponent ownerComponent,
                ref DynamicBuffer<BulletSpawnRequestBuffer> bulletSpawnRequestBuffer,
                ref WeaponComponent weaponComponent,
                in GhostOwnerComponent ghostOwner)
            {
                if (weaponFiringComponent.IsFiring)
                {
                    var ownerViewPosition =
                        LocalToWorldLookup[CharacterComponentLookUp[ownerComponent.OwnerCharacter].ViewEntity].Position;
                    var ownerViewRotation =
                        LocalToWorldLookup[CharacterComponentLookUp[ownerComponent.OwnerCharacter].ViewEntity].Rotation;
                    var ownerMuzzlePosition = LocalToWorldLookup[weaponComponent.MuzzleSocket].Position;
                    var ownerMuzzleRotation = LocalToWorldLookup[weaponComponent.MuzzleSocket].Rotation;
                    for (int i = 0; i < weaponFiringComponent.TickBulletsCounter; i++)
                    {
                        var spreadX = 0f;
                        var spreadZ = 0f;
                        if (SpreadInfoBufferLookup.TryGetBuffer(entity, out var spreadInfoBuffer))
                        {
                            spreadX = spreadInfoBuffer[i].SpreadAngleRotX;
                            spreadZ = spreadInfoBuffer[i].SpreadAngleRotZ;
                        }

                        if (weaponBulletComponent.IsGhost || IsServer)
                        {
                            bulletSpawnRequestBuffer.Add(new BulletSpawnRequestBuffer
                            {
                                OwnerCharacter = ownerComponent.OwnerCharacter,
                                OwnerPlayer = ownerComponent.OwnerPlayer,
                                OwnerWeapon = entity,
                                BulletPrefab = weaponBulletComponent.BulletEntity,
                                Position = ownerViewPosition,
                                Rotation = ownerViewRotation,
                                IsGhost = weaponBulletComponent.IsGhost,
                                SpreadAngleRotX = spreadX,
                                SpreadAngleRotZ = spreadZ,
                            });
                        }
                    }
                }
            }
        }



        public void HandleRaycastWeaponShot(bool isServer, in NetworkTime networkTime,
            PhysicsWorld physicsWorld, in PhysicsWorldHistorySingleton physicsWorldHistory,
            in ComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<StoredKinematicCharacterData> storedKinematicCharacterDataLookup,
            in ComponentLookup<FirstPersonCharacterComponent> characterComponentLookUp,
            in ComponentLookup<CharacterHitBoxComponent> characterHitBoxComponentLookUp,
            in BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPairBufferLookup,
            in BufferLookup<BulletSpawnVisualRequestBuffer> bulletSpawnVisualRequestBufferLookup,
            ref NativeList<RaycastHit> raycastHits, EntityCommandBuffer commandBuffer,
            ref SystemState state)
        {
            foreach (var (raycastWeaponComponent, ownerComponent,
                         weaponComponent, spreadComponent,
                         bulletVisualComponent, interpolationDelay,
                         weaponFiringComponent, entity)
                     in SystemAPI.Query<RefRW<RaycastWeaponComponent>,
                         RefRW<WeaponOwnerComponent>, RefRW<WeaponComponent>,
                         RefRW<WeaponSpreadComponent>, RefRW<WeaponBulletVisualComponent>,
                         RefRW<InterpolationDelay>, RefRW<WeaponFiringComponent>>().WithEntityAccess())
            {
                physicsWorldHistory.GetCollisionWorldFromTick(networkTime.ServerTick, interpolationDelay.ValueRO.Value,
                    ref physicsWorld, out var collisionWorld);
                for (int i = 0; i < weaponFiringComponent.ValueRW.TickBulletsCounter; i++)
                {
                    WeaponUtility.ComputeSpread(ref spreadComponent.ValueRW, out var spreadX, out var spreadZ);
                    WeaponUtility.ComputeRaycastShotDetail(ref raycastWeaponComponent.ValueRW,
                        ref ownerComponent.ValueRW,
                        ref bulletVisualComponent.ValueRW,
                        characterComponentLookUp[ownerComponent.ValueRW.OwnerCharacter].ViewEntity,
                        weaponComponent.ValueRW.MuzzleSocket,
                        spreadX, spreadZ, localToWorldLookup, characterHitBoxComponentLookUp,
                        storedKinematicCharacterDataLookup,
                        physicsColliderKeyEntityPairBufferLookup, interpolationDelay.ValueRW, collisionWorld,
                        ref raycastHits,
                        out bool hitFound,
                        out RaycastHit closetValidHit, out BulletSpawnVisualRequestBuffer visualRequestBuffer,
                        out float damageMultiplier, out Entity hitCharacter);
                    if (!isServer)
                    {
                        bulletSpawnVisualRequestBufferLookup[entity].Add(visualRequestBuffer);
                    }
                    else if (hitCharacter != Entity.Null)
                    {
                        var damageInfo = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent(damageInfo, new BulletDamageCleanUp
                        {
                            CausedByPlayer = ownerComponent.ValueRW.OwnerPlayer,
                            CausedByCharacter = ownerComponent.ValueRW.OwnerCharacter,
                            DamagedCharacter = hitCharacter,
                            CausedByWeapon = entity,
                            DamageCaused = raycastWeaponComponent.ValueRW.Damage,
                            DamageMultiplier = damageMultiplier,
                        });
                        commandBuffer.DestroyEntity(damageInfo);
                    }

                    // bulletSpawnVisualRequestBuffer.Add(visualRequestBuffer);
                    Debug.Log(
                        $"Hit found: {hitFound}, Distance: {closetValidHit.Fraction * raycastWeaponComponent.ValueRW.MaxRange}");

                }
            }
        }


        public partial struct RaycastWeaponShotJob : IJobEntity
        {
            [ReadOnly] public bool IsServer;

            [ReadOnly] public NetworkTime NetworkTime;

            [ReadOnly] public PhysicsWorld PhysicsWorld;

            [ReadOnly] public PhysicsWorldHistorySingleton PhysicsWorldHistory;

            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            [ReadOnly] public ComponentLookup<StoredKinematicCharacterData> StoredKinematicCharacterDataLookup;

            [ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> CharacterComponentLookUp;

            [ReadOnly] public ComponentLookup<CharacterHitBoxComponent> CharacterHitBoxComponentLookUp;

            [ReadOnly] public BufferLookup<PhysicsColliderKeyEntityPair> PhysicsColliderKeyEntityPairBufferLookup;
            
            public EntityCommandBuffer CommandBuffer;

            public NativeList<RaycastHit> RaycastHits;




            void Execute(Entity entity, ref RaycastWeaponComponent raycastWeaponComponent,
                ref WeaponComponent weaponComponent,
                ref WeaponOwnerComponent ownerComponent,
                ref WeaponSpreadComponent spreadComponent,
                ref WeaponBulletVisualComponent bulletVisualComponent,
                ref DynamicBuffer<BulletSpawnVisualRequestBuffer> bulletSpawnVisualRequestBuffer,
                in InterpolationDelay interpolationDelay,
                in WeaponFiringComponent weaponFiringComponent)
            {
                PhysicsWorldHistory.GetCollisionWorldFromTick(NetworkTime.ServerTick, interpolationDelay.Value,
                    ref PhysicsWorld, out var collisionWorld);
                for (int i = 0; i < weaponFiringComponent.TickBulletsCounter; i++)
                {
                    WeaponUtility.ComputeSpread(ref spreadComponent, out var spreadX, out var spreadZ);
                    WeaponUtility.ComputeRaycastShotDetail(ref raycastWeaponComponent, ref ownerComponent,
                        ref bulletVisualComponent,
                        CharacterComponentLookUp[ownerComponent.OwnerCharacter].ViewEntity,
                        weaponComponent.MuzzleSocket,
                        spreadX, spreadZ, LocalToWorldLookup, CharacterHitBoxComponentLookUp,
                        StoredKinematicCharacterDataLookup,
                        PhysicsColliderKeyEntityPairBufferLookup, interpolationDelay, collisionWorld, ref RaycastHits,
                        out bool hitFound,
                        out RaycastHit closetValidHit, out BulletSpawnVisualRequestBuffer visualRequestBuffer,
                        out float damageMultiplier, out Entity hitCharacter);
                    if (!IsServer)
                    {
                        bulletSpawnVisualRequestBuffer.Add(visualRequestBuffer);
                    }
                    else if (hitCharacter != Entity.Null)
                    {
                        var damageInfo = CommandBuffer.CreateEntity();
                        CommandBuffer.AddComponent(damageInfo, new BulletDamageCleanUp
                        {
                            CausedByPlayer = ownerComponent.OwnerPlayer,
                            CausedByCharacter = ownerComponent.OwnerCharacter,
                            DamagedCharacter = hitCharacter,
                            CausedByWeapon = entity,
                            DamageCaused = raycastWeaponComponent.Damage,
                            DamageMultiplier = damageMultiplier,
                        });
                        CommandBuffer.DestroyEntity(damageInfo);
                    }
                    Debug.Log(
                        $"Hit found: {hitFound}, Distance: {closetValidHit.Fraction * raycastWeaponComponent.MaxRange}");
                }
            }
        }
    }



    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedFixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct WeaponActiveSystem : ISystem
    {

        private ComponentLookup<WeaponControlComponent> weaponControlLookUp;
        private ComponentLookup<FirstPersonCharacterComponent> firstPersonCharacterComponentLookup;
        private BufferLookup<LinkedEntityGroup> linkedEntityGroupLookup;
        private ComponentLookup<OwningPlayer> owningPlayerLookup;
        // private ComponentLookup<RenderFilterSettings> renderFilterSettingsLookup;
        private BufferLookup<Child> childBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            weaponControlLookUp = state.GetComponentLookup<WeaponControlComponent>(true);
            firstPersonCharacterComponentLookup = state.GetComponentLookup<FirstPersonCharacterComponent>(true);
            linkedEntityGroupLookup = state.GetBufferLookup<LinkedEntityGroup>(false);
            owningPlayerLookup = state.GetComponentLookup<OwningPlayer>(false);
            childBufferLookup = state.GetBufferLookup<Child>(false);
        }

        public void OnDestroy(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            weaponControlLookUp.Update(ref state);
            firstPersonCharacterComponentLookup.Update(ref state);
            linkedEntityGroupLookup.Update(ref state);
            owningPlayerLookup.Update(ref state);
            childBufferLookup.Update(ref state);
            var isServer = state.WorldUnmanaged.IsServer();
            var localNetworkId = -1;
            if (!isServer)
            {
                localNetworkId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;
            }
            WeaponSetupJob weaponSetupJob = new WeaponSetupJob
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                WeaponControlLookUp = weaponControlLookUp,
                FirstPersonCharacterComponentLookup = firstPersonCharacterComponentLookup,
                LinkedEntityGroupLookup = linkedEntityGroupLookup,
                OwningPlayerLookup = owningPlayerLookup,
                EntityManager = state.EntityManager,
                ChildBufferLookup = childBufferLookup,
                LocalNetworkId = localNetworkId,
                IsServer = isServer
            };
            weaponSetupJob.Schedule(state.Dependency).Complete();
        }

        [BurstCompile]
        public partial struct WeaponSetupJob : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            [ReadOnly] public ComponentLookup<WeaponControlComponent> WeaponControlLookUp;
            [ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterComponentLookup;
            public BufferLookup<LinkedEntityGroup> LinkedEntityGroupLookup;
            public ComponentLookup<OwningPlayer> OwningPlayerLookup;
            public BufferLookup<Child> ChildBufferLookup;
            public EntityManager EntityManager;
            public int LocalNetworkId;
            public bool IsServer;

            void Execute(Entity entity, ref ActiveWeaponComponent activeWeapon, GhostOwnerComponent ghostOwnerComponent)
            {
                if (activeWeapon.WeaponEntity != activeWeapon.PreviousWeaponEntity)
                {
                    if (WeaponControlLookUp.HasComponent(activeWeapon.WeaponEntity))
                    {
                        if (FirstPersonCharacterComponentLookup.TryGetComponent(entity,
                                out FirstPersonCharacterComponent character))
                        {
                            commandBuffer.AddComponent(activeWeapon.WeaponEntity,
                                new Parent { Value = character.WeaponAnimationSocketEntity });
                            commandBuffer.AddComponent(activeWeapon.WeaponEntity, new WeaponOwnerComponent
                            {
                                OwnerCharacter = entity,
                                OwnerPlayer = OwningPlayerLookup[entity].Entity
                            });
                            DynamicBuffer<LinkedEntityGroup> linkedEntityBuffer = LinkedEntityGroupLookup[entity];
                            linkedEntityBuffer.Add(new LinkedEntityGroup { Value = activeWeapon.WeaponEntity });
                            if (LocalNetworkId == ghostOwnerComponent.NetworkId && !IsServer)
                            {
                                MiscUtilities.SetLayerInHierarchy(EntityManager, commandBuffer, activeWeapon.WeaponEntity,
                                    ChildBufferLookup, 0);
                            }
                        }
                    }

                    activeWeapon.PreviousWeaponEntity = activeWeapon.WeaponEntity;
                }
            }
        }
    }
}