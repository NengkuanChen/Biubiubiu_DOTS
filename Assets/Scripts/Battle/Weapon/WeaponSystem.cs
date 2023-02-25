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
    public class WeaponPredictionUpdateGroup : ComponentSystemGroup
    { }

    
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct WeaponSystem : ISystem
    {

        private ComponentLookup<WorldTransform> worldTransformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            state.RequireForUpdate<WeaponOwnerComponent>();
            worldTransformLookup = state.GetComponentLookup<WorldTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            worldTransformLookup.Update(ref state);
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
            
            WeaponBulletSpawningJob bulletSpawningJob = new WeaponBulletSpawningJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer,
                WorldTransformLookup = worldTransformLookup
            };
            state.Dependency = bulletSpawningJob.Schedule(state.Dependency);
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
        public partial struct WeaponBulletSpawningJob : IJobEntity
        {
            [ReadOnly]
            public float DeltaTime;
            [ReadOnly]
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly]
            public ComponentLookup<WorldTransform> WorldTransformLookup;

            [BurstCompile]
            void Execute(Entity entity, ref WeaponFiringComponent weaponFiringComponent,
                ref WeaponBulletComponent weaponBulletComponent, ref WeaponComponent weaponComponent, 
                in GhostOwnerComponent ghostOwner)
            {
                if (weaponFiringComponent.TickBulletsCounter > 0)
                {
                    for (int i = 0; i < weaponFiringComponent.TickBulletsCounter; i++)
                    {
                        var bulletEntity = CommandBuffer.Instantiate(weaponBulletComponent.BulletEntity);
                        var muzzlePosition = WorldTransformLookup[weaponComponent.MuzzleSocket].Position;
                        var muzzleRotation = WorldTransformLookup[weaponComponent.MuzzleSocket].Rotation;
                        CommandBuffer.SetComponent(bulletEntity,
                            LocalTransform.FromPositionRotation(muzzlePosition, muzzleRotation));
                        CommandBuffer.SetComponent(bulletEntity, new GhostOwnerComponent
                        {
                            NetworkId = ghostOwner.NetworkId
                        });
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
        public void OnCreate(ref SystemState state)
        {
            
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            WeaponSetupJob weaponSetupJob = new WeaponSetupJob
            {
                commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                WeaponControlLookUp = SystemAPI.GetComponentLookup<WeaponControlComponent>(true),
                FirstPersonCharacterComponentLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterComponent>(true),
                LinkedEntityGroupLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(false),
                OwningPlayerLookup = SystemAPI.GetComponentLookup<OwningPlayer>(false),
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