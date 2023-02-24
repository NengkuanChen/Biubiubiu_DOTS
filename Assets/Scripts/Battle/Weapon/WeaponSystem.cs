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
            state.RequireForUpdate<SetUpWeaponRequestComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
            foreach (var (setUpWeaponRequestComponent, entity) in SystemAPI.Query<RefRO<SetUpWeaponRequestComponent>>().WithEntityAccess())
            {
                var characterEntity = SystemAPI.GetComponent<FirstPersonPlayer>(setUpWeaponRequestComponent.ValueRO.ForPlayer).ControlledCharacter;
                if (characterEntity != Entity.Null)
                {
                    var weaponEntity = commandBuffer.Instantiate(setUpWeaponRequestComponent.ValueRO.WeaponEntity);
                    commandBuffer.AddComponent(weaponEntity, new WeaponOwnerComponent
                    {
                        OwnerPlayer = setUpWeaponRequestComponent.ValueRO.ForPlayer,
                        OwnerCharacter = characterEntity
                    });
                    commandBuffer.SetComponent(weaponEntity, new GhostOwnerComponent
                    {
                        NetworkId = networkIdFromEntity[setUpWeaponRequestComponent.ValueRO.ForConnection].Value
                    });
                    commandBuffer.AddComponent(weaponEntity, new Parent
                    {
                        Value = SystemAPI.GetComponent<FirstPersonCharacterComponent>(characterEntity).WeaponAnimationSocketEntity,
                    });
                    // var linkedEntityGroups = SystemAPI.GetBuffer<LinkedEntityGroup>(characterEntity);
                    commandBuffer.AppendToBuffer<LinkedEntityGroup>(characterEntity, new LinkedEntityGroup
                    {
                        Value = weaponEntity
                    });
                    commandBuffer.SetComponent(weaponEntity, new LocalTransform
                    {
                        Position = float3.zero,
                        Rotation = quaternion.identity,
                        Scale = 1,
                    });
                    
                    commandBuffer.DestroyEntity(entity);
                }
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
    
    
    public struct SetUpWeaponRequestComponent : IComponentData
    {
        public Entity ForPlayer;
        public Entity ForConnection;
        public Entity WeaponEntity;
    }
    
    public struct CharacterActiveWeaponComponent : IComponentData
    {
        public Entity WeaponEntity;
    }
}