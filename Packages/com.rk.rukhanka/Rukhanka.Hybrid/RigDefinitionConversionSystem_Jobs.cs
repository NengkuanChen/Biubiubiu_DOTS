using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public partial class RigDefinitionConversionSystem
{

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	struct CreateBlobAssetsJob: IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeSlice<BlobAssetReference<RigDefinitionBlob>> outBlobAssets;
		public RTP.RigDefinition inData;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		public void Execute()
		{
			var data = inData;
			var bb = new BlobBuilder(Allocator.Temp);
			ref var c = ref bb.ConstructRoot<RigDefinitionBlob>();

#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref c.name, ref data.name);
#endif
			var hasher = new xxHash3.StreamingState();

			var bonesArr = bb.Allocate(ref c.bones, data.rigBones.Length);
			for (int l = 0; l < bonesArr.Length; ++l)
			{
				var db = data.rigBones[l];
				ref var rbi = ref bonesArr[l];
				rbi.hash = db.hash;
				rbi.parentBoneIndex = db.parentBoneIndex;
				rbi.type = db.type;
				rbi.refPose = db.refPose;

#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref rbi.name, ref db.name);
#endif
				hasher.Update(rbi.hash);
			}
			c.hash = new Hash128(hasher.DigestHash128());
			c.rootBoneIndex = data.rootBoneIndex;
			c.applyRootMotion = data.applyRootMotion;

			var rv = bb.CreateBlobAssetReference<RigDefinitionBlob>(Allocator.Persistent);

			for (int i = 0; i < outBlobAssets.Length; ++i)
			{
				outBlobAssets[i] = rv;
			}
		}
	}

//=================================================================================================================//

	[BurstCompile]
	struct CreateComponentDatasJob: IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<RigDefinitionBakerComponent> bakerData;
		[ReadOnly]
		public NativeArray<BlobAssetReference<RigDefinitionBlob>> blobAssets;

		public EntityCommandBuffer.ParallelWriter ecb;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		public void Execute(int startIndex, int count)
		{
			for (int i = startIndex; i < startIndex + count; ++i)
			{
				var rigBlob = blobAssets[i];
				var rd = bakerData[i];

				var rdc = new RigDefinitionComponent()
				{
					rigBlob = rigBlob
				};

				ecb.AddComponent(startIndex, rd.targetEntity, rdc);

				for (int l = 0; l < rd.rigDefData.rigBones.Length; ++l)
				{
					var rb = rd.rigDefData.rigBones[l];

					var boneEntity = rb.boneObjectEntity;
					if (boneEntity != Entity.Null)
					{
						var animatorEntityRefComponent = new AnimatorEntityRefComponent()
						{
							animatorEntity = rd.targetEntity,
							boneIndexInAnimationRig = l
						};
						ecb.AddComponent(startIndex, boneEntity, animatorEntityRefComponent);
					}
				}

				if (rd.rigDefData.applyRootMotion)
				{
					ecb.AddBuffer<RootMotionStateComponent>(startIndex, rd.targetEntity);
				}
			}
		}
	}
} 
}
