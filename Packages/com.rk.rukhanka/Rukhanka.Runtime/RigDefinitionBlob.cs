
using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

public struct RigBoneInfo
{
#if RUKHANKA_DEBUG_INFO
	public BlobString name;
#endif

	public enum Type
	{
		GenericBone,
		RootBone,
		RootMotionBone
	};

	public Hash128 hash;
	public int parentBoneIndex;
	public Type type;
	public BoneTransform refPose;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct RigDefinitionBlob
{
#if RUKHANKA_DEBUG_INFO
	public BlobString name;
#endif
	public Hash128 hash;
	public BlobArray<RigBoneInfo> bones;
	public bool applyRootMotion;
	public int rootBoneIndex;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct RigDefinitionComponent: IComponentData
{
	public BlobAssetReference<RigDefinitionBlob> rigBlob;
}
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public struct BoneRemapTableBlob
{
	public BlobArray<int> rigBoneToSkinnedMeshBoneRemapIndices;
}

}
