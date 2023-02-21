using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Battle.Input
{
    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct PlayerInputComponent : IInputComponentData
    {
        public float MovementHorizontal;
        public float MovementVertical;
        public bool Crouch;
        public bool Jump;
        public bool Fire;
        public bool Aim;
        public bool Reload;
        public bool Sprint;
        public float MouseDeltaX;
        public float MouseDeltaY;
        public float Sensitivity;
    }

    [DisallowMultipleComponent]
    public class PlayerInputComponentAuthoring : MonoBehaviour
    {
        public class PlayerInputAuthoringBaker : Baker<PlayerInputComponentAuthoring>
        {
            public override void Bake(PlayerInputComponentAuthoring componentAuthoring)
            {
                PlayerInputComponent component = default(PlayerInputComponent);
                AddComponent(component);
            }
        }
    }
    
    
}