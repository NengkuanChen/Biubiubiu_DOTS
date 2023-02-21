using Battle.Input;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace Battle.Character
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct CharacterMovementSystem: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Simulate>()
                .WithAll<PlayerInputComponent>()
                .WithAllRW<PhysicsVelocity>();
            var query = state.GetEntityQuery(builder);
            state.RequireForUpdate(query);
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var moveJob = new CharacterMoveJob
            {
                maxSpeed = 5,
                acceleration = 10
            };
            state.Dependency = moveJob.ScheduleParallel(state.Dependency);
        }
        
        
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        partial struct CharacterMoveJob : IJobEntity
        {
            public float maxSpeed;
            public float acceleration;
            public void Execute(PlayerInputComponent playerInput, ref PhysicsVelocity physicsVelocity)
            {
                var moveInput = new float2(playerInput.MovementHorizontal, playerInput.MovementVertical);
                moveInput = math.normalizesafe(moveInput);
                var physicsVelocityValue = physicsVelocity.Linear;
                var targetVelocity = new float3(moveInput.x, 0, moveInput.y) * maxSpeed;
                var velocityChange = targetVelocity - physicsVelocityValue;
                velocityChange.y = 0;
                velocityChange = math.clamp(velocityChange, -acceleration, acceleration);
                physicsVelocity.Linear += velocityChange;
            }
        }
    }
}