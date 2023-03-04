using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;
using UnityEngine;

namespace Battle.Weapon
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    
    public partial class DamageRegistrationSystemGroup : ComponentSystemGroup
    {
        
    }
    
    
    [UpdateInGroup(typeof(DamageRegistrationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DamageRegistrationSystemServer : ISystem
    {
        private ComponentLookup<Health> healthLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            healthLookup = state.GetComponentLookup<Health>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            healthLookup.Update(ref state);
            new BulletDamageCleanUpJob()
            {
                HealthLookup = healthLookup,
                EntityCommandBufferParallelWriter = commandBuffer.AsParallelWriter()
            }.ScheduleParallel(state.Dependency).Complete();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            
        }
        
        public partial struct BulletDamageCleanUpJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<Health> HealthLookup;
            public EntityCommandBuffer.ParallelWriter EntityCommandBufferParallelWriter;
            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref BulletDamageCleanUp bulletDamage)
            {
                var damage = bulletDamage.DamageCaused * bulletDamage.DamageMultiplier;
                var damagedCharacter = bulletDamage.DamagedCharacter;
                if (HealthLookup.TryGetComponent(damagedCharacter, out Health health))
                {
                    health.CurrentHealth -= damage;
                    EntityCommandBufferParallelWriter.SetComponent(chunkIndexInQuery, damagedCharacter, health);
                    EntityCommandBufferParallelWriter.RemoveComponent<BulletDamageCleanUp>(chunkIndexInQuery, entity);
                }
            }
        }
    }
    
}