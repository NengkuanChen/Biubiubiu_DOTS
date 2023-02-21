using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{ 
[UpdateInGroup(typeof(RukhankaAnimationSystemGroup))]
[RequireMatchingQueriesForUpdate]
[BurstCompile]
public partial struct AnimatorControllerSystem: ISystem
{
	NativeHashMap<Hash128, UnsafeHashMap<Hash128, int>> internalBoneMapsCache;
	EntityQuery animatorControllerQuery;

	BufferTypeHandle<AnimatorControllerLayerComponent> controllerLayersBufferHandle;
	BufferTypeHandle<AnimatorControllerParameterComponent> controllerParametersBufferHandle;
	EntityTypeHandle entityTypeHandle;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
		internalBoneMapsCache = new (100, Allocator.Persistent);

		var eqBuilder0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAllRW<AnimatorControllerLayerComponent>();
		animatorControllerQuery = ss.GetEntityQuery(eqBuilder0);

		controllerLayersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerLayerComponent>();
		controllerParametersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		if (internalBoneMapsCache.IsCreated)
		{
			foreach (var kv in internalBoneMapsCache)
			{
				kv.Value.Dispose();
			}
			internalBoneMapsCache.Dispose();
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		var dt = SystemAPI.Time.DeltaTime;
		var frameCount = Time.frameCount;
		var ecb = new EntityCommandBuffer(Allocator.TempJob);
		var ecbp = ecb.AsParallelWriter();

		controllerLayersBufferHandle.Update(ref ss);
		controllerParametersBufferHandle.Update(ref ss);
		entityTypeHandle.Update(ref ss);

	#if RUKHANKA_DEBUG_INFO
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
	#endif

		var stateMachineProcessJob = new StateMachineProcessJob()
		{
			controllerLayersBufferHandle = controllerLayersBufferHandle,
			controllerParametersBufferHandle = controllerParametersBufferHandle,
			dt = dt,
			frameIndex = frameCount,
			ecbp = ecbp,
			entityTypeHandle = entityTypeHandle,
		#if RUKHANKA_DEBUG_INFO
			doLogging = dc.logAnimatorControllerProcesses,
		#endif
		};

		var jh = stateMachineProcessJob.ScheduleParallel(animatorControllerQuery, ss.Dependency);
		jh.Complete();

		//	Need to apply animations buffer changes here, because if we do this in any available sync point (end frame for example) we will lag our animations by one frame behind
		ecb.Playback(ss.EntityManager);
		ecb.Dispose();

		var createBoneRemapTablesJob = new CreateBoneRemapTablesJob()
		{
			internalBoneMapsCache = internalBoneMapsCache
		};

		createBoneRemapTablesJob.Run();
	}
}
}
