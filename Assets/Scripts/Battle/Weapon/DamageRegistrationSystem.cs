using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;

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
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            
        }
        
        public void OnDestroy(ref SystemState state)
        {
            
        }
    }
    
    public struct DamageRegistrationComponent : IComponentData
    {
        public float Damage;
        public Entity DamageWeapon;
        public Entity DamageSourcePlayer;
        public Entity DamageSourceCharacter;
        public Entity DamagedPlayer;
    }
}