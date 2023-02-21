using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

public struct FastAnimatorParameter
{
	public FixedStringName paramName;
	public Hash128 hash;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public FastAnimatorParameter(FixedStringName name)
	{
		hash = name.CalculateHash128();
		paramName = name;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool GetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, out ParameterValue outData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			outData = runtimeParameters[paramIdx].value;
		}
		else
		{
			outData = default;
		}
		return isValid;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public bool SetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, in ParameterValue paramData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			var p = runtimeParameters[paramIdx];
			p.value = paramData;
			runtimeParameters[paramIdx] = p;
		}
		return isValid;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe int GetRuntimeParameterIndex(BlobAssetReference<ParameterPerfectHashTableBlob> cb, in DynamicBuffer<AnimatorControllerParameterComponent> acpc)
	{
		ref var seedTable = ref cb.Value.seedTable;
		var paramIdx = PerfectHash.QueryPerfectHashTable(ref seedTable, hash);
		
		if (paramIdx >= acpc.Length || acpc[paramIdx].hash != hash)
			return -1;

		return paramIdx;
	}

}
}
