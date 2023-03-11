using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

public static class MiscUtilities
{
    public static void SetShadowModeInHierarchy(EntityManager entityManager, EntityCommandBuffer ecb, Entity onEntity, BufferLookup<Child> childBufferFromEntity, UnityEngine.Rendering.ShadowCastingMode mode)
    {
        if (entityManager.HasComponent<RenderFilterSettings>(onEntity))
        {
            RenderFilterSettings renderFilterSettings = entityManager.GetSharedComponent<RenderFilterSettings>(onEntity);
            renderFilterSettings.ShadowCastingMode = mode;
            ecb.SetSharedComponent(onEntity, renderFilterSettings);
        }

        if (childBufferFromEntity.HasBuffer(onEntity))
        {
            DynamicBuffer<Child> childBuffer = childBufferFromEntity[onEntity];
            for (int i = 0; i < childBuffer.Length; i++)
            {
                SetShadowModeInHierarchy(entityManager, ecb, childBuffer[i].Value, childBufferFromEntity, mode);
            }
        }
    }
    
    public static void SetLayerInHierarchy(EntityManager entityManager, EntityCommandBuffer ecb, Entity onEntity, BufferLookup<Child> childBufferFromEntity, int layer)
    {
        if (entityManager.HasComponent<RenderFilterSettings>(onEntity))
        {
            RenderFilterSettings renderFilterSettings = entityManager.GetSharedComponent<RenderFilterSettings>(onEntity);
            renderFilterSettings.Layer = layer;
            ecb.SetSharedComponent(onEntity, renderFilterSettings);
        }

        if (childBufferFromEntity.HasBuffer(onEntity))
        {
            DynamicBuffer<Child> childBuffer = childBufferFromEntity[onEntity];
            for (int i = 0; i < childBuffer.Length; i++)
            {
                SetLayerInHierarchy(entityManager, ecb, childBuffer[i].Value, childBufferFromEntity, layer);
            }
        }
    }

    public static bool HasSingleton<T>(ref SystemState state) where T : unmanaged, IComponentData
    {
        return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).HasSingleton<T>();
    }

    public static T GetSingleton<T>(ref SystemState state) where T : unmanaged, IComponentData
    {
        return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).GetSingleton<T>();
    }

    public static Entity GetSingletonEntity<T>(ref SystemState state) where T : unmanaged, IComponentData
    {
        return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).GetSingletonEntity();
    }
}
