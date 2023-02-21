#if UNITY_EDITOR

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[RequireMatchingQueriesForUpdate]
public partial class FixDeformedEntityChildLocalTransformConversionSystem: SystemBase
{
	EntityQuery deformedEntityChildrenQuery;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	
	protected override void OnCreate()
	{
		base.OnCreate();
		
		using var eqb0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<AdditionalEntityParent>()
		.WithOptions(EntityQueryOptions.IncludePrefab);

		deformedEntityChildrenQuery = GetEntityQuery(eqb0);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	protected override void OnUpdate()
	{
		var potentialRenderMeshes = deformedEntityChildrenQuery.ToComponentDataArray<AdditionalEntityParent>(Allocator.Temp);
		var entityArr = deformedEntityChildrenQuery.ToEntityArray(Allocator.Temp);

		var animatedSkinnedMeshComponentLookup = GetComponentLookup<AnimatedSkinnedMeshComponent>(true);
		var localTransformComponentLookup = GetComponentLookup<LocalTransform>(true);

		var ecb = new EntityCommandBuffer(Allocator.Temp);

		for (int i = 0; i < potentialRenderMeshes.Length; ++i)
		{
			var e = entityArr[i];
			var prm = potentialRenderMeshes[i];
			if (animatedSkinnedMeshComponentLookup.HasComponent(prm.Parent) && localTransformComponentLookup.HasComponent(prm.Parent))
			{
				var lt = LocalTransform.Identity;
				ecb.AddComponent(e, lt);
			}
		}

		ecb.Playback(EntityManager);
	}
}
}

#endif
