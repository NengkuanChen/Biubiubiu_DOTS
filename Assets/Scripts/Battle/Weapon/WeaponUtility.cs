using Rival;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Battle.Weapon
{
    public struct WeaponFiringDetail : IComponentData
    {
        
    }

    public class WeaponUtility
    {
        // public static void ComputeFiringDetail(ref WeaponComponent weaponComponent,
        //     
        //     in ComponentLookup<LocalToWorld> localToWorldLookup,
        //     out bool hitFound,
        //     out RaycastHit closestValidHit)
        // {
        //     Entity shotSimulationOriginEntity = localToWorldLookup.HasComponent(weaponComponent.MuzzleSocket) ? weaponComponent.MuzzleSocket : ;
        //     LocalToWorld shotSimulationOriginLtW = localToWorldLookup[shotSimulationOriginEntity];
        //     
        // }
    }
}