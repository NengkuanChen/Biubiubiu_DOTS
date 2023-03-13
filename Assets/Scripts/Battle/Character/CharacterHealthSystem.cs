using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Battle.Character
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(FirstPersonCharacterVariableUpdateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct CharacterHealthSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent>().Build());
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }
        
        public void OnUpdate(ref SystemState state)
        {
            new CharacterHealthRecoveryJob()
            {
                DeltaTime = state.WorldUnmanaged.Time.DeltaTime
            }.Schedule(state.Dependency).Complete();
        }
        
        public partial struct CharacterHealthRecoveryJob : IJobEntity
        {
            public float DeltaTime;
            void Execute(Entity entity, ref Health health, ref HealthRecoveryComponent recovery)
            {
                recovery.RecoveryTimer -= DeltaTime;
                if (recovery.RecoveryTimer <= 0f)
                {
                    health.CurrentHealth = math.min(health.CurrentHealth + recovery.RecoveryRate * DeltaTime,
                        health.MaxHealth);
                }

                if (health.CurrentHealth >= health.MaxHealth)
                {
                    recovery.RecoveryTimer = recovery.RecoveryDelay;
                }
            }
        }
        
    }
}