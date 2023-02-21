
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public static class MathUtils
{
	public static quaternion ShortestRotation(quaternion from, quaternion to)
	{
		uint4 sign = math.asuint(math.dot(from, to)) & 0x80000000;
		var rv = math.asfloat(sign ^ math.asuint(to.value));
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	//	Shuffle array according to given indices
	public static void ShuffleList<T>(UnsafeList<T> arr, in NativeList<int> shuffleIndices) where T: unmanaged
	{
		if (arr.Length < 2) return;
		if (arr.Length != shuffleIndices.Length) return;
	
		Span<T> scatterArr = stackalloc T[arr.Length];
		for (int i = 0; i < arr.Length; ++i)
		{
			scatterArr[i] = arr[shuffleIndices[i]];
		}

		for (int i = 0; i < arr.Length; ++i)
		{
			arr[i] = scatterArr[i];
		}
	}
}
}
