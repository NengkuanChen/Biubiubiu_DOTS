
using Unity.Entities;
using Unity.Mathematics;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
	public struct DebugConfigurationComponent: IComponentData
	{
		public bool logRigDefinitionBaking;
		public bool logSkinnedMeshBaking;
		public bool logAnimatorBaking;
		public bool logClipBaking;

		public bool logAnimatorControllerProcesses;
		public bool logAnimationCalculationProcesses;

		public bool visualizeAllRigs;
		public float4 colorTri, colorLines;
	}
}

