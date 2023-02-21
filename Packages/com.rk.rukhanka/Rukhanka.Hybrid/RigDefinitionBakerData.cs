using System;
using Unity.Collections.LowLevel.Unsafe;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
//	RTP - Ready to process
namespace RTP
{
public struct RigBoneInfo: IEquatable<Hash128>
{
	public FixedStringName name;
	public Hash128 hash;
	public int parentBoneIndex;
	public Rukhanka.RigBoneInfo.Type type;
	public BoneTransform refPose;
	public Entity boneObjectEntity;

	public bool Equals(Hash128 o) => o == hash;
}

////////////////////////////////////////////////////////////////////////////////////////

public struct RigDefinition: IDisposable
{
	public FixedStringName name;
	public UnsafeList<RigBoneInfo> rigBones;
	public bool applyRootMotion;
	public int rootBoneIndex;

	public void Dispose() => rigBones.Dispose();

	unsafe public override int GetHashCode()
	{
		var hh = new xxHash3.StreamingState();
		hh.Update(name.GetUnsafePtr(), name.Length);
		foreach (var b in rigBones)
		{
			hh.Update(b.hash.Value);
		}
		hh.Update(applyRootMotion);
		hh.Update(rootBoneIndex);

		var rv = math.hash(hh.DigestHash128());
		return (int)rv;
	}
}

} // RTP
}


