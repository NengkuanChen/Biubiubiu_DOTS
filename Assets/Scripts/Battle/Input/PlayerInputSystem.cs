using DefaultNamespace.Battle;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Battle.Input
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSpawner>();
            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<NetworkIdComponent>();
            // var playerInput = new PlayerInput();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var horizontal = UnityEngine.Input.GetAxis("Horizontal");
            var vertical = UnityEngine.Input.GetAxis("Vertical");
            var mouseDeltaX = UnityEngine.Input.GetAxis("Mouse X");
            var mouseDeltaY = UnityEngine.Input.GetAxis("Mouse Y");
            var isJump = UnityEngine.Input.GetButtonDown("Jump");
            var isFire = UnityEngine.Input.GetButtonDown("Fire1");
            var isAim = UnityEngine.Input.GetButtonDown("Fire2");
            var isReload = UnityEngine.Input.GetKeyDown(KeyCode.R);
            var isSprint = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            var isCrouch = UnityEngine.Input.GetKey(KeyCode.LeftControl);
            
            foreach (var playerInput in SystemAPI.Query<RefRW<PlayerInputComponent>>().WithAll<GhostOwnerIsLocal>())
            {
                playerInput.ValueRW.MovementHorizontal = horizontal;
                playerInput.ValueRW.MovementVertical = vertical;
                playerInput.ValueRW.MouseDeltaX = mouseDeltaX;
                playerInput.ValueRW.MouseDeltaY = mouseDeltaY;
                playerInput.ValueRW.Jump = isJump;
                playerInput.ValueRW.Fire = isFire;
                playerInput.ValueRW.Aim = isAim;
                playerInput.ValueRW.Reload = isReload;
                playerInput.ValueRW.Sprint = isSprint;
                playerInput.ValueRW.Crouch = isCrouch;
            }
        }
        
    }
}