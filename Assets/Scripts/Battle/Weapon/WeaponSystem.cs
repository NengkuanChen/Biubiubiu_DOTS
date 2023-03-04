using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Battle.Weapon
{
    
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PredictedFixedStepTransformsUpdateSystem))]
    public partial class WeaponPredictionUpdateGroup : ComponentSystemGroup
    { }

    
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct WeaponSystem : ISystem
    {

        private ComponentLookup<LocalToWorld> localToWorldLookup;

        private ComponentLookup<FirstPersonCharacterComponent> characterComponentLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            state.RequireForUpdate<WeaponOwnerComponent>();
            localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            characterComponentLookup = state.GetComponentLookup<FirstPersonCharacterComponent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            characterComponentLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
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
            
            WeaponBulletSpawningRequestJob bulletSpawningRequestJob = new WeaponBulletSpawningRequestJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer.AsParallelWriter(),
                LocalToWorldLookup = localToWorldLookup,
                CharacterComponentLookUp = characterComponentLookup,
                IsServer = state.WorldUnmanaged.IsServer()
            };
            state.Dependency = bulletSpawningRequestJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            
        }
        
        [BurstCompile]
        public partial struct WeaponFiringRegistrationJob : IJobEntity
        {
            [ReadOnly]
            public float DeltaTime;

            [BurstCompile]
            void Execute(Entity entity, ref WeaponFiringComponent firingComponent,
                ref WeaponControlComponent weaponControl, ref WeaponComponent weaponComponent, in GhostOwnerComponent ghostOwner)
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
            [ReadOnly]
            public float DeltaTime;
            
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
                    if (magazine.MagazineRestBullet <= 0 || (weaponControl.ReloadPressed && magazine.MagazineRestBullet < magazine.MagazineSize))
                    {
                        Reload(entity, ref magazine, ref reloadComponent);
                    }

                    weaponFiringComponent.TickBulletsCounter = math.min(weaponFiringComponent.TickBulletsCounter,
                        magazine.MagazineRestBullet);
                    magazine.MagazineRestBullet -= weaponFiringComponent.TickBulletsCounter;
                }

                
            }
            
            private void Reload(Entity entity, ref WeaponMagazineComponent magazine, ref WeaponReloadComponent reloadComponent)
            {
                reloadComponent.IsReloading = true;
                reloadComponent.ReloadTimeLeft = magazine.ReloadTime;
            }
            
            private void FinishedReload(Entity entity, ref WeaponMagazineComponent magazine, ref WeaponReloadComponent reloadComponent)
            {
                reloadComponent.IsReloading = false;
                magazine.MagazineRestBullet = magazine.MagazineSize;
            }
        }
        
        [BurstCompile]
        public partial struct WeaponBulletSpawningRequestJob : IJobEntity
        {
            [ReadOnly]
            public float DeltaTime;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            [ReadOnly] 
            public ComponentLookup<FirstPersonCharacterComponent> CharacterComponentLookUp;
            
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            
            [ReadOnly]
            public bool IsServer;

            [BurstCompile]
            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref WeaponFiringComponent weaponFiringComponent,
                ref WeaponBulletComponent weaponBulletComponent, 
                ref WeaponOwnerComponent ownerComponent, 
                ref DynamicBuffer<BulletSpawnRequestBuffer> bulletSpawnRequestBuffer,
                ref WeaponComponent weaponComponent,
                in GhostOwnerComponent ghostOwner)
            {
                if (weaponFiringComponent.IsFiring)
                {
                    var ownerViewPosition = LocalToWorldLookup[CharacterComponentLookUp[ownerComponent.OwnerCharacter].ViewEntity].Position;
                    var ownerViewRotation = LocalToWorldLookup[CharacterComponentLookUp[ownerComponent.OwnerCharacter].ViewEntity].Rotation;
                    var ownerMuzzlePosition = LocalToWorldLookup[weaponComponent.MuzzleSocket].Position;
                    var ownerMuzzleRotation = LocalToWorldLookup[weaponComponent.MuzzleSocket].Rotation;
                    for (int i = 0; i < weaponFiringComponent.TickBulletsCounter; i++)
                    {
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
                            });
                        }
                        
                        // else if (weaponBulletComponent.BulletVisualEntity != Entity.Null)
                        // {
                        //     bulletSpawnVisualRequestBuffer.Add(new BulletSpawnVisualRequestBuffer
                        //     {
                        //         OwnerCharacter = ownerComponent.OwnerCharacter,
                        //         OwnerPlayer = ownerComponent.OwnerPlayer,
                        //         OwnerWeapon = entity,
                        //         BulletVisualPrefab = weaponBulletComponent.BulletVisualEntity,
                        //         Position = ownerMuzzlePosition,
                        //         Rotation = ownerMuzzleRotation,
                        //     });
                        // }
                    }
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

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            weaponControlLookUp = state.GetComponentLookup<WeaponControlComponent>(true);
            firstPersonCharacterComponentLookup = state.GetComponentLookup<FirstPersonCharacterComponent>(true);
            linkedEntityGroupLookup = state.GetBufferLookup<LinkedEntityGroup>(false);
            owningPlayerLookup = state.GetComponentLookup<OwningPlayer>(false);
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
            WeaponSetupJob weaponSetupJob = new WeaponSetupJob
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                WeaponControlLookUp = weaponControlLookUp,
                FirstPersonCharacterComponentLookup = firstPersonCharacterComponentLookup,
                LinkedEntityGroupLookup = linkedEntityGroupLookup,
                OwningPlayerLookup = owningPlayerLookup,
            };
            weaponSetupJob.Schedule();
        }
        
        [BurstCompile]
        public partial struct WeaponSetupJob : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            [ReadOnly]
            public ComponentLookup<WeaponControlComponent> WeaponControlLookUp; 
            [ReadOnly]
            public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterComponentLookup;
            public BufferLookup<LinkedEntityGroup> LinkedEntityGroupLookup;
            public ComponentLookup<OwningPlayer> OwningPlayerLookup;

            void Execute(Entity entity, ref ActiveWeaponComponent activeWeapon)
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
                        }
                    }
                    activeWeapon.PreviousWeaponEntity = activeWeapon.WeaponEntity;
                }
            }
        }
    }
    
    
}