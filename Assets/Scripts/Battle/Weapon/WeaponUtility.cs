using Battle.Character;
using NUnit.Framework.Internal;
using Rival;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Battle.Weapon
{
    public struct WeaponFiringDetail : IComponentData
    {
        
    }

    public class WeaponUtility
    {
        public static void ComputeRaycastShotDetail(ref RaycastWeaponComponent raycastWeaponComponent,
            ref WeaponOwnerComponent weaponOwnerComponent,
            ref WeaponBulletVisualComponent weaponBulletVisualComponent,
            in Entity shotOrigin,
            in Entity muzzleEntity,
            in float spreadAngleRotX,
            in float spreadAngleRotZ,
            in ComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<CharacterHitBoxComponent> hitBoxComponentLookup,
            in ComponentLookup<StoredKinematicCharacterData> StoredKinematicCharacterDataLookup,
            in BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPair, 
            in InterpolationDelay interpolationDelay,
            in CollisionWorld collisionWorld,
            ref NativeList<RaycastHit> hits,
            out bool hitFound,
            out RaycastHit closetValidHit,
            out BulletSpawnVisualRequestBuffer visualRequestBuffer,
            out float damageMultiplier
            )
        {
            hitFound = false;
            closetValidHit = default; 
            visualRequestBuffer = default; ;
            LocalToWorld shotLtW = localToWorldLookup[shotOrigin];
            LocalToWorld muzzleLtW = localToWorldLookup[muzzleEntity];
            visualRequestBuffer.BulletVisualPrefab = weaponBulletVisualComponent.BulletVisualEntity;
            visualRequestBuffer.IsHit = false;
            LocalTransform muzzleLocalTransform =
                LocalTransform.FromPositionRotation(muzzleLtW.Position, muzzleLtW.Rotation);
            ComputeRaycastSpread(ref shotLtW, spreadAngleRotX, spreadAngleRotZ, out float3 shotDirection,
                out float3 shotPosition);
            ComputeBulletSpreadRotation(ref muzzleLocalTransform, spreadAngleRotX, spreadAngleRotZ);
            visualRequestBuffer.LocalTransform = muzzleLocalTransform;
            
            
            RaycastInput raycastInput = new RaycastInput
            {
                Start = shotPosition,
                End = shotPosition + shotDirection * raycastWeaponComponent.MaxRange,
                Filter = raycastWeaponComponent.HitFilter
            };
            hits.Clear();
            collisionWorld.CastRay(raycastInput, ref hits);
            hitFound = GetClosestValidRaycastHit(in hits, in StoredKinematicCharacterDataLookup,
                in physicsColliderKeyEntityPair, in hitBoxComponentLookup,  in weaponOwnerComponent,
                out closetValidHit, out damageMultiplier);
            if (hitFound)
            {
                visualRequestBuffer.HitPosition = closetValidHit.Position;
                visualRequestBuffer.IsHit = true;
                muzzleLocalTransform.Rotation =
                    quaternion.LookRotation(closetValidHit.Position - muzzleLtW.Position, math.up());

            }
        }

        public static bool GetClosestValidRaycastHit(in NativeList<RaycastHit> hits,
            in ComponentLookup<StoredKinematicCharacterData> storedKinematicCharacterDataLookup,
            in BufferLookup<PhysicsColliderKeyEntityPair> physicsColliderKeyEntityPairLookup,
            in ComponentLookup<CharacterHitBoxComponent> hitBoxComponentLookup,
            in WeaponOwnerComponent weaponOwnerComponent,
            out RaycastHit closestValidHit,
            out float damageMultiplier)
        {
            closestValidHit = default;
            closestValidHit.Fraction = float.MaxValue;
            damageMultiplier = 0f;
            bool hitFound = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit tmpHit = hits[i];

                if (tmpHit.Fraction < closestValidHit.Fraction)
                {
                    if (storedKinematicCharacterDataLookup.HasComponent(tmpHit.Entity))
                    {
                        if (weaponOwnerComponent.OwnerCharacter.Equals(tmpHit.Entity))
                        {
                            continue;
                        }
                        if (physicsColliderKeyEntityPairLookup.TryGetBuffer(tmpHit.Entity, out var colliderKeyBuffer))
                        {
                            for (int j = 0; j < colliderKeyBuffer.Length; j++)
                            {
                                if (colliderKeyBuffer[j].Key == tmpHit.ColliderKey)
                                {
                                    damageMultiplier = hitBoxComponentLookup[colliderKeyBuffer[j].Entity]
                                        .DamageMultiplier;
                                    hitFound = true;
                                    closestValidHit = tmpHit;
                                    break;
                                }
                            }
                        }
                    }
                    else if (tmpHit.Material.CollisionResponse == CollisionResponsePolicy.Collide ||
                             tmpHit.Material.CollisionResponse == CollisionResponsePolicy.CollideRaiseCollisionEvents)
                    {
                        hitFound = true;
                        closestValidHit = tmpHit;
                    }
                }
            }

            return hitFound;
        }

        public static void ComputeRaycastSpread(ref LocalToWorld shotOrigin, float shotSpreadAngleRotX, float shotSpreadAngleRotZ,
            out float3 shotDirection, out float3 shotPosition)
        {
            var forward = shotOrigin.Forward;
            var right = shotOrigin.Right;
            shotDirection = forward;
            shotDirection = math.mul(quaternion.AxisAngle(right, shotSpreadAngleRotX), shotDirection);
            shotDirection = math.mul(quaternion.AxisAngle(forward, shotSpreadAngleRotZ), shotDirection);
            shotPosition = shotOrigin.Position;
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
        
        public static void ComputeBulletSpreadRotation(ref LocalTransform localTransform, float spreadAngleRotX, float spreadAngleRotZ)
        {
            var forward = localTransform.Forward();
            var up = localTransform.Up();
            up = math.mul(quaternion.AxisAngle(forward, spreadAngleRotZ), up);
            localTransform.Rotation = quaternion.LookRotation(forward, up);
            var right = localTransform.Right();
            forward = localTransform.Forward();
            forward = math.mul(quaternion.AxisAngle(right, spreadAngleRotX), forward);
            up = localTransform.Up();
            up = math.mul(quaternion.AxisAngle(right, spreadAngleRotX), up);
            localTransform.Rotation = quaternion.LookRotation(forward, up);
        }

    }
}