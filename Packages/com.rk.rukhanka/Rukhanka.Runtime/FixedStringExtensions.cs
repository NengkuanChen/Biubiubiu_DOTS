using Unity.Burst;
using Unity.Collections;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;

////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public static class FixedStringExtensions
{
	public unsafe static Hash128 CalculateHash128(in this FixedStringName s)
	{
		if (s.IsEmpty)
			return default;

		var hasher = new xxHash3.StreamingState();
		hasher.Update(s.GetUnsafePtr(), s.Length);
		var rv = new Hash128(hasher.DigestHash128());
		return rv;
	}
}
}

