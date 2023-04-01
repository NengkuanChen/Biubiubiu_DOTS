using Battle.Misc;
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
        private ComponentLookup<HealthRecoveryComponent> healthRecoveryLookup;
        private BufferLookup<CharacterDamageSourceRecordBuffer> damageSourceRecordBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
            healthLookup = state.GetComponentLookup<Health>();
            healthRecoveryLookup = state.GetComponentLookup<HealthRecoveryComponent>();
            damageSourceRecordBufferLookup = state.GetBufferLookup<CharacterDamageSourceRecordBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            healthLookup.Update(ref state);
            healthRecoveryLookup.Update(ref state);
            damageSourceRecordBufferLookup.Update(ref state);
            new BulletDamageCleanUpJob()
            {
                HealthLookup = healthLookup,
                HealthRecoveryLookup = healthRecoveryLookup,
                DamageSourceRecordBufferLookup = damageSourceRecordBufferLookup,
                EntityCommandBuffer = commandBuffer.AsParallelWriter()
            }.Schedule(state.Dependency).Complete();
        }
        
        public void OnDestroy(ref SystemState state)
        {
            
        }
        
        public partial struct BulletDamageCleanUpJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<Health> HealthLookup;
           
            [ReadOnly]
            public ComponentLookup<HealthRecoveryComponent> HealthRecoveryLookup;
            
            [ReadOnly]
            public BufferLookup<CharacterDamageSourceRecordBuffer> DamageSourceRecordBufferLookup;
            
            

            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref BulletDamageCleanUp bulletDamage)
            {
                var damage = bulletDamage.DamageCaused * bulletDamage.DamageMultiplier;
                // Debug.Log($"Bullet Damage : {bulletDamage.DamageCaused}, Damage Multiplier : {bulletDamage.DamageMultiplier}");
                var damagedCharacter = bulletDamage.DamagedCharacter;
                if (HealthLookup.TryGetComponent(damagedCharacter, out Health health))
                {
                    health.CurrentHealth -= damage;
                    if (HealthRecoveryLookup.TryGetComponent(damagedCharacter, out HealthRecoveryComponent healthRecovery))
                    {
                        healthRecovery.RecoveryTimer = healthRecovery.RecoveryDelay;
                        EntityCommandBuffer.SetComponent(chunkIndexInQuery, damagedCharacter, healthRecovery);
                    }

                    if (DamageSourceRecordBufferLookup.TryGetBuffer(damagedCharacter,
                            out DynamicBuffer<CharacterDamageSourceRecordBuffer> damageSourceRecordBuffer))
                    {
                        // damageSourceRecordBuffer.Add();
                        EntityCommandBuffer.AppendToBuffer(chunkIndexInQuery,damagedCharacter, new CharacterDamageSourceRecordBuffer
                        {
                            Damage = damage,
                            SourceCharacter = bulletDamage.CausedByCharacter,
                            SourcePlayer = bulletDamage.CausedByPlayer,
                        });
                    }
                    EntityCommandBuffer.SetComponent(chunkIndexInQuery, damagedCharacter, health);
                    EntityCommandBuffer.RemoveComponent<BulletDamageCleanUp>(chunkIndexInQuery, entity);
                    
                    Debug.Log($"damaged character {damagedCharacter.Index} for {damage} damage, health is now {health.CurrentHealth}");
                }
            }
        }
    }



    public struct DamageInfoRPC : IRpcCommand
    {
        public int DamagedPlayerId;
        public int DamageFromPlayerId;
        public int WeaponId;
        
        public float DamageCaused;
        public float DamageMultiplier;
    }
}