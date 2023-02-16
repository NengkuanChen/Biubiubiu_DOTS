using Unity.Entities;
using Unity.NetCode;

namespace Battle
{
    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct PlayerMovementInput : IInputComponentData
    {
        public float Horizontal;
        public float Vertical;
    }
        
    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct PlayerMouseDeltaInput : IInputComponentData
    {
        public float Horizontal;
        public float Vertical;
        public float Sensitivity;
    }

    [GhostComponent(PrefabType=GhostPrefabType.None)]
    public struct PlayerJumpInput : IInputComponentData, IEnableableComponent
    {
        
    }
        
    [GhostComponent(PrefabType=GhostPrefabType.None)]
    public struct PlayerFireInput : IInputComponentData, IEnableableComponent
    {
        
    }
}