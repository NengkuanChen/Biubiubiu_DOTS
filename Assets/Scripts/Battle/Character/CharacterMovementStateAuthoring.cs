using DefaultNamespace.Battle;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Character
{
    
    public struct CharacterMovementState: IComponentData
    {
        public bool IsGrounded;
        public bool IsCrouching;
        public bool IsSprinting;
        public float MovingSpeed;
        public float3 MovingDirection;
    }


    [DisallowMultipleComponent]
    public class CharacterMovementStateAuthoring : MonoBehaviour
    {
        public class CharacterMovementStateAuthoringBaker : Baker<CharacterMovementStateAuthoring>
        {
            public override void Bake(CharacterMovementStateAuthoring componentAuthoring)
            {
                CharacterMovementState component = default(CharacterMovementState);
                component.IsGrounded = true;
                component.IsCrouching = false;
                component.IsSprinting = false;
                component.MovingSpeed = 0.0f;
                component.MovingDirection = float3.zero;
                AddComponent(component);
            }
        }
    }
    
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public struct CharacterGroundedState: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSpawner>();
            state.RequireForUpdate<NetworkIdComponent>();
            state.RequireForUpdate<CharacterMovementState>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            // foreach (var VARIABLE in COLLECTION)
            // {
            //     
            // }
        }
    }
}