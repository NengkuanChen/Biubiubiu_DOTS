#if UNITY_EDITOR

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Unity.Assertions;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{ 
public class AnimationClipBaker
{
	enum BoneType
	{
		Generic,
		RootMotion
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static ValueTuple<string, string> SplitPath(string path)
	{
		var arr = path.Split('/');
		Assert.IsTrue(arr.Length > 0);
		var rv = (arr.Last(), arr.Length > 1 ? arr[arr.Length - 2] : "");
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static (BindingType, BoneType) PickBindingTypeByString(string bindingString) => bindingString switch
	{
		"m_LocalPosition" => (BindingType.Translation, BoneType.Generic),
		"MotionT" => (BindingType.Translation, BoneType.RootMotion),
		"RootT" => (BindingType.Translation, BoneType.RootMotion),
		"m_LocalRotation" => (BindingType.Quaternion, BoneType.Generic),
		"MotionQ" => (BindingType.Quaternion, BoneType.RootMotion),
		"RootQ" => (BindingType.Quaternion, BoneType.RootMotion),
		"localEulerAngles" => (BindingType.EulerAngles, BoneType.Generic),
		"localEulerAnglesRaw" => (BindingType.EulerAngles, BoneType.Generic),
		"m_LocalScale" => (BindingType.Scale, BoneType.Generic),
		_ => (BindingType.Unknown, BoneType.Generic)
	};

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static short ChannelIndexFromSymbol(char c) => c switch
	{
		'x' => 0,
		'y' => 1,
		'z' => 2,
		'w' => 3,
		_ => 999
	};

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static FixedStringName ConstructBoneClipName(ValueTuple<string, string> nameAndPath, BoneType bt)
	{
		FixedStringName rv;
		//	Empty name string is unnamed root bone
		if (nameAndPath.Item1.Length == 0 && nameAndPath.Item2.Length == 0)
		{
			rv = bt switch
			{
				BoneType.Generic => SpecialBones.unnamedRootBoneName,
				BoneType.RootMotion => SpecialBones.rootMotionBoneName,
				_ => SpecialBones.invalidBoneName
			};
		}
		else
		{
			rv = new FixedStringName(nameAndPath.Item1);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static RTP.AnimationCurve PrepareAnimationCurve(Keyframe[] keysArr, short channelIndex, BindingType bindingType)
	{
		var animCurve = new RTP.AnimationCurve();
		animCurve.channelIndex = channelIndex;
		animCurve.bindingType = bindingType;
		animCurve.keyFrames = new UnsafeList<KeyFrame>(keysArr.Length, Allocator.Persistent);

		foreach (var k in keysArr)
		{
			var kf = new KeyFrame()
			{
				time = k.time,
				inTan = k.inTangent,
				outTan = k.outTangent,
				v = k.value
			};
			animCurve.keyFrames.Add(kf);
		}
		return animCurve;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static int GetOrCreateBoneClipHolder(ref UnsafeList<RTP.BoneClip> clipsArr, in FixedStringName name)
	{
		var rv = clipsArr.IndexOf(name);
		if (rv < 0)
		{
			rv = clipsArr.Length;
			var bc = new RTP.BoneClip();
			bc.name = name;
			bc.animationCurves = new UnsafeList<RTP.AnimationCurve>(32, Allocator.Persistent);
			clipsArr.Add(bc);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static RTP.BoneClip MakeBoneClipCopy(in RTP.BoneClip bc)
	{
		var rv = bc;
		rv.animationCurves = new UnsafeList<RTP.AnimationCurve>(bc.animationCurves.Length, Allocator.Persistent);
		for (int i = 0; i < bc.animationCurves.Length; ++i)
		{
			var inKf = bc.animationCurves[i].keyFrames;
			var outKf = new UnsafeList<KeyFrame>(inKf.Length, Allocator.Persistent);
			for (int j = 0; j < inKf.Length; ++j)
			{
				outKf.Add(inKf[j]);
			}
			var ac = bc.animationCurves[i];
			ac.keyFrames = outKf;
			rv.animationCurves.Add(ac);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static void DebugLoggging(RTP.AnimationClip ac, bool hasRootCurves)
	{
#if RUKHANKA_DEBUG_INFO
		var dc = GameObject.FindObjectOfType<RukhankaDebugConfiguration>();
		var logClipBaking = dc != null && dc.logClipBaking;
		if (!logClipBaking) return;

		Debug.Log($"Baking animation clip '{ac.name}'. Tracks: {ac.bones.Length}. User curves: {ac.curves.Length}. Length: {ac.length}s. Looped: {ac.looped}. Has root curves: {hasRootCurves}");
#endif
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public RTP.AnimationClip PrepareAnimationComputeData(AnimationClip ac)
	{
		var acSettings = AnimationUtility.GetAnimationClipSettings(ac);

		var rv = new RTP.AnimationClip();
		rv.name = ac.name;
		rv.bones = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		rv.curves = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		rv.length = ac.length;
		rv.looped = ac.isLooping;
		rv.hash = ac.GetHashCode();
		rv.loopPoseBlend = acSettings.loopBlend;
		rv.cycleOffset = acSettings.cycleOffset;
		rv.additiveReferencePoseTime = acSettings.additiveReferencePoseTime;

		var bindings = AnimationUtility.GetCurveBindings(ac);
		bool hasRootCurves = false;
		int unnamedRootBoneIndex = -1;
		foreach (var b in bindings)
		{
			var ec = AnimationUtility.GetEditorCurve(ac, b);
			var t = b.propertyName.Split('.');

			var bindingType = PickBindingTypeByString(t[0]);
			short channelIndex = t.Length > 1 ? ChannelIndexFromSymbol(t[1][0]) : (short)0;
			var animCurve = PrepareAnimationCurve(ec.keys, channelIndex, bindingType.Item1);
			var isGenericCurve = bindingType.Item1 == BindingType.Unknown;
			hasRootCurves |= bindingType.Item2 == BoneType.RootMotion;

			if (isGenericCurve)
			{
				var curveId = GetOrCreateBoneClipHolder(ref rv.curves, t[0]);
				var curveStruct = rv.curves[curveId];
				curveStruct.animationCurves.Add(animCurve);
				rv.curves[curveId] = curveStruct;
			}
			else
			{
				var nameAndPath = SplitPath(b.path);
				var boneNameFixed = ConstructBoneClipName(nameAndPath, bindingType.Item2);
				var boneId = GetOrCreateBoneClipHolder(ref rv.bones, boneNameFixed);
				var boneStruct = rv.bones[boneId];
				boneStruct.animationCurves.Add(animCurve);
				rv.bones[boneId] = boneStruct;
				if (boneNameFixed == SpecialBones.unnamedRootBoneName)
					unnamedRootBoneIndex = boneId;
			}
		}

		//	Copy root bone track to the root motion bone
		if (!hasRootCurves && unnamedRootBoneIndex >= 0)
		{
			var rootMotionBone = MakeBoneClipCopy(rv.bones[unnamedRootBoneIndex]);
			rootMotionBone.name = SpecialBones.rootMotionBoneName;
			rv.bones.Add(rootMotionBone);
		}

		DebugLoggging(rv, hasRootCurves);

		return rv;
	}
}
}

#endif