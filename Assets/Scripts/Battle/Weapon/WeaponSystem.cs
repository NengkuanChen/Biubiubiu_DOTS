using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Battle.Weapon
{
    
    public struct WeaponSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            state.RequireForUpdate<WeaponOwnerComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            
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
                            // commandBuffer.SetComponent(activeWeapon.WeaponEntity, new LocalTransform
                            // {
                            //     Position = float3.zero,
                            //     Rotation = quaternion.identity,
                            // });
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