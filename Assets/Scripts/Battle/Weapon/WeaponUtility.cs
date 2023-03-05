using NUnit.Framework.Internal;
using Rival;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Battle.Weapon
{
    public struct WeaponFiringDetail : IComponentData
    {
        
    }

    public class WeaponUtility
    {
        public static void ComputeRaycastDetail(ref RaycastWeaponComponent raycastWeaponComponent,
            in float3 visualOriginPosition)
        {
            
        }

        public static void ComputeSpread(
            ref WeaponSpreadComponent spreadComponent,
            out float spreadAngleRotX,
            out float spreadAngleRotZ)
        {
            var spreadType = GameGlobalConfigs.SpreadConfig.SpreadTypes[spreadComponent.SpreadTypeIndex];
            var spreadAngleRotXMax = spreadType.SpreadCurve
                    .Evaluate(spreadComponent.SpreadPercentage) * spreadType.MaxSpreadAngle;
            var randomizer = spreadComponent.Randomizer;
            spreadAngleRotZ = randomizer.NextFloat(0, 360);
            spreadAngleRotX = randomizer.NextFloat(0, spreadAngleRotXMax);
            spreadComponent.Randomizer = randomizer;
            spreadComponent.SpreadPercentage += spreadType.SpreadPercetageIncreasePerShot;
        }
        
        public static void GetSpreadDecreaseRate(int spreadTypeIndex , out float spreadPercentageDecreasePerSecond)
        {
            spreadPercentageDecreasePerSecond = GameGlobalConfigs.SpreadConfig.SpreadTypes[spreadTypeIndex].SpreadPercentageDecreasePerSecond;
        }


    }
}