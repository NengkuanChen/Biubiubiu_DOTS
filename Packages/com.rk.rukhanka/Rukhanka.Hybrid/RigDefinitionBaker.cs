using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Rukhanka.Editor;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[TemporaryBakingType]
public struct RigDefinitionBakerComponent: IComponentData
{
	public RTP.RigDefinition rigDefData;
	public Entity targetEntity;
	public UnsafeList<Entity> boneHierarchyEntities;
	public int hash;
#if RUKHANKA_DEBUG_INFO
	public FixedStringName name;
#endif
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public class RigDefinitionBaker: Baker<RigDefinitionAuthoring>
{
	public override void Bake(RigDefinitionAuthoring a)
	{
		if (a.rigDefinition == null)
		{
			Debug.LogError($"RigDefinitionAuthoring '{a.name}' error: Rig Definition is not set!");
			return;
		}

		var processedRig = CreateRigDefinitionFromRigAuthoring(a);

		//	Create additional "bake-only" entity that will be removed from live world
		var be = CreateAdditionalEntity(TransformUsageFlags.None, true);
		var acbd = new RigDefinitionBakerComponent
		{
			rigDefData = processedRig,
			targetEntity = GetEntity(),
			hash = processedRig.GetHashCode(),
		#if RUKHANKA_DEBUG_INFO
			name = a.name
		#endif
		};

		DependsOn(a.transform);
		AddComponent(be, acbd);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigDefinition CreateRigDefinitionFromRigAuthoring(RigDefinitionAuthoring rigDef)
	{
		var rv = new RTP.RigDefinition();
		rv.rigBones = new UnsafeList<RTP.RigBoneInfo>(30, Allocator.Persistent);

		if (rigDef.rigDefinition == null)
		{
			Debug.LogError($"Rig definition is missing in authoring animator '{rigDef.name}'!");
			return rv;
		}

		rv.name = rigDef.gameObject.name;
		rv.applyRootMotion = false;

		//	Iterate over all mask paths and build rig tree
		var rd = rigDef.rigDefinition;
		using var parentBoneHashes = new NativeList<Hash128>(Allocator.Temp);

		for (int i = 0; i < rd.transformCount; ++i)
		{
			var isActive = rd.GetTransformActive(i);
			if (!isActive) continue;

			var bonePath = rd.GetTransformPath(i);
			string boneName;
			FixedStringName parentBoneName;

			// This is unnamed root bone, handle it a little differently
			if (bonePath.Length == 0)
			{
				boneName =  SpecialBones.unnamedRootBoneName.ToString();
				parentBoneName = "";
			}
			else
			{
				var boneNames = bonePath.Split('/');
				parentBoneName = new FixedStringName(boneNames.Length > 1 ? boneNames[boneNames.Length - 2] : SpecialBones.unnamedRootBoneName);
				boneName = boneNames[boneNames.Length - 1];
			}

			var parentBoneNameHash = parentBoneName.CalculateHash128();
			parentBoneHashes.Add(parentBoneNameHash);

			var ab = CreateRigBoneInfo(rigDef, boneName, bonePath);
			if (ab.type == RigBoneInfo.Type.RootBone) 
				rv.rootBoneIndex = rv.rigBones.Length;
			rv.rigBones.Add(ab);
		}

		//	Second pass fill parent indices and bone entities
		for (int i = 0; i < rv.rigBones.Length; ++i)
		{
			var rbi = rv.rigBones[i];
			var idx = rv.rigBones.IndexOf(parentBoneHashes[i]);
			rbi.parentBoneIndex = idx;
			rv.rigBones[i] = rbi;
		}

		if (NeedRootMotionBone(rigDef))
		{
			var rootMotionBone = CreateRootMotionBone();
			rv.rigBones.Add(rootMotionBone);
			rv.applyRootMotion = true;
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	bool NeedRootMotionBone(RigDefinitionAuthoring rigDef)
	{
		var animator = rigDef.GetComponent<Animator>();
		if (animator != null)
		{
			return animator.applyRootMotion;
		}
		return false;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigBoneInfo CreateRootMotionBone()
	{
		var rv = new RTP.RigBoneInfo()
		{
			name = SpecialBones.rootMotionBoneName,
			hash = SpecialBones.rootMotionBoneName.CalculateHash128(),
			parentBoneIndex = -1,
			type = RigBoneInfo.Type.RootMotionBone,
			refPose = BoneTransform.Identity()
		};
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.RigBoneInfo CreateRigBoneInfo(RigDefinitionAuthoring rda, string name, string bonePath)
	{
		Transform t = bonePath.Length == 0 ? rda.transform : rda.transform.Find(bonePath);

		if (t == null)
		{
			Debug.LogError($"Cannot find bone with name '{bonePath}' in object skeleton!");
			return new RTP.RigBoneInfo();
		}

		var pose = new BoneTransform()
		{
			pos = t.localPosition,
			rot = t.localRotation,
			scale = t.localScale
		};

		var isRootBone = rda.rootBone == null && t == rda.transform || rda.rootBone == t;
		var boneName = new FixedStringName(name);
		var boneHash = boneName.CalculateHash128();
		var ab = new RTP.RigBoneInfo()
		{
			name = boneName,
			hash = boneHash,
			parentBoneIndex = -1,
			type = isRootBone ? RigBoneInfo.Type.RootBone : RigBoneInfo.Type.GenericBone,
			refPose = pose,
			boneObjectEntity = GetEntity(t)
		};
		return ab;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	Transform FindChildRecursively(Transform root, string name)
	{
		var rv = root.Find(name);
		if (rv != null)
			return rv;

		var childCount = root.childCount;
		for (int i = 0; i < childCount; ++i)
		{
			 var c = root.GetChild(i);
			 var crv = FindChildRecursively(c, name);
			 if (crv != null)
				return crv;
		}
		return null;
	}
}
}
