#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[TemporaryBakingType]
public struct AnimatorControllerBakerData: IComponentData
{
	public RTP.Controller controllerData;
	public Entity targetEntity;
	public int hash;
#if RUKHANKA_DEBUG_INFO
	public FixedStringName name;
#endif
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public class AnimatorControllerBaker: Baker<Animator>
{
	public override void Bake(Animator a)
	{
		if (a.runtimeAnimatorController == null)
		{
			Debug.LogWarning($"There is no controller attached to '{a.name}' animator. Skipping this object");
			return;
		}

		var rac = GetRuntimeAnimatorController(a);
		var ac = GetAnimatorControllerFromRuntime(rac);
		var animationHashCodes = GatherUnityAnimationsHashCodes(rac.animationClips);
		var cd = GenerateControllerComputationData(ac, animationHashCodes);

		//	If AnimatorOverrideController is used, substitute animations
		var aoc = a.runtimeAnimatorController as AnimatorOverrideController;
		var animClipsWithOverride = aoc != null ? aoc.animationClips : rac.animationClips;
		var	animationClips = ConvertAllControllerAnimations(animClipsWithOverride);
		cd.animationClips = animationClips;

		//	Create additional "bake-only" entity that will be removed from live world
		var be = CreateAdditionalEntity(TransformUsageFlags.None, true);
		var acbd = new AnimatorControllerBakerData
		{
			controllerData = cd,
			targetEntity = GetEntity(),
			hash = ac.GetHashCode(),
		#if RUKHANKA_DEBUG_INFO
			name = a.name
		#endif
		};

		DependsOn(a.runtimeAnimatorController);
		AddComponent(be, acbd);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RuntimeAnimatorController GetRuntimeAnimatorController(Animator a)
	{
		var rv = a.runtimeAnimatorController;
		//	Check for animator override controller
		var aoc = rv as AnimatorOverrideController;
		if (aoc != null)
		{
			rv = aoc.runtimeAnimatorController;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	AnimatorController GetAnimatorControllerFromRuntime(RuntimeAnimatorController rac)
	{
		if (rac == null) return null;
		var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(rac));
		return controller;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	List<int> GatherUnityAnimationsHashCodes(AnimationClip[] allClips)
	{
		allClips = Deduplicate(allClips);

		var rv = new List<int>();
		for (int i = 0; i < allClips.Length; ++i)
			rv.Add(allClips[i].GetHashCode());
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Controller GenerateControllerComputationData(AnimatorController ac, List<int> allClipsHashCodes)
	{
		var rv = new RTP.Controller();
		rv.name = ac.name;
		rv.parameters = GenerateControllerParametersComputationData(ac.parameters);

		rv.layers = new UnsafeList<RTP.Layer>(ac.layers.Length, Allocator.Persistent);

		for (int i = 0; i < ac.layers.Length; ++i)
		{
			var l = ac.layers[i];
			var lOverriden = l.syncedLayerIndex >= 0 ? ac.layers[l.syncedLayerIndex] : l;
			var layerData = GenerateControllerLayerComputationData(lOverriden, l, allClipsHashCodes, i, rv.parameters);
			if (!layerData.states.IsEmpty)
				rv.layers.Add(layerData);
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeList<RTP.Parameter> GenerateControllerParametersComputationData(AnimatorControllerParameter[] aps)
	{
		var parameters = new UnsafeList<RTP.Parameter>(aps.Length, Allocator.Persistent);
		for (int i = 0; i < aps.Length; ++i)
		{
			var sourceParam = aps[i];
			var outParam = new RTP.Parameter();

			switch (sourceParam.type)
			{
			case AnimatorControllerParameterType.Float:
				outParam.type = ControllerParameterType.Float;
				outParam.defaultValue.floatValue = sourceParam.defaultFloat;
				break;
			case AnimatorControllerParameterType.Int:
				outParam.type = ControllerParameterType.Int;
				outParam.defaultValue.intValue = sourceParam.defaultInt;
				break;
			case AnimatorControllerParameterType.Bool:
				outParam.type = ControllerParameterType.Bool;
				outParam.defaultValue.boolValue = sourceParam.defaultBool;
				break;
			case AnimatorControllerParameterType.Trigger:
				outParam.type = ControllerParameterType.Trigger;
				outParam.defaultValue.boolValue = sourceParam.defaultBool;
				break;
			};

			outParam.name = sourceParam.name;
			parameters.Add(outParam);
		}
		return parameters;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Layer GenerateControllerLayerComputationData(AnimatorControllerLayer acl, AnimatorControllerLayer aclOverriden, List<int> allClipsHashCodes, int layerIndex, in UnsafeList<RTP.Parameter> allParams)
	{
		var l = new RTP.Layer();
		l.name = acl.name;

		var stateList = new UnsafeList<RTP.State>(128, Allocator.Persistent);
		var anyStateTransitions = new UnsafeList<RTP.Transition>(128, Allocator.Persistent);

		GenerateControllerStateMachineComputationData(acl.stateMachine, acl, aclOverriden, allClipsHashCodes, ref stateList, ref anyStateTransitions, allParams);
		l.avatarMask = AvatarMaskConversionSystem.PrepareAvatarMaskComputeData(acl.avatarMask);
		l.states = stateList;
		
		//	Set default state
		l.defaultStateIndex = -1;
		for (int i = 0; i < stateList.Length; ++i)
		{
			var fullStateName = ConstructCompoundStateName(acl.stateMachine.defaultState.name, acl.stateMachine.name);
			if (fullStateName == stateList[i].name)
			{ 
				l.defaultStateIndex = i;
				break;
			}
		}
		l.anyStateTransitions = anyStateTransitions;
		l.weight = layerIndex == 0 ? 1 : aclOverriden.defaultWeight;
		l.blendMode = (AnimationBlendingMode)aclOverriden.blendingMode;

		return l;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Condition GenerateControllerConditionComputationData(AnimatorCondition c, in UnsafeList<RTP.Parameter> allParams)
	{
		var rv = new RTP.Condition();
		rv.paramName = c.parameter;

		var paramIdx = allParams.IndexOf(rv.paramName);
		var p = allParams[paramIdx];

		switch (p.type)
		{
		case ControllerParameterType.Int:
			rv.threshold.intValue = (int)c.threshold;
			break;
		case ControllerParameterType.Float:
			rv.threshold.floatValue = c.threshold;
			break;
		case ControllerParameterType.Bool:
		case ControllerParameterType.Trigger:
			rv.threshold.boolValue = c.threshold > 0;
			break;
		}
		rv.conditionMode = (AnimatorConditionMode)c.mode;
		rv.name = $"{rv.paramName} {rv.conditionMode} {rv.threshold}";
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	string ConstructCompoundStateName(string ownStateName, string stateMachineName)
	{
		return stateMachineName + "_" + ownStateName;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Transition GenerateControllerTransitionComputationData(AnimatorStateTransition t, string ownStateName, AnimatorStateMachine asm, AnimatorControllerLayer asl, in UnsafeList<RTP.Parameter> allParams)
	{
		var rv = new RTP.Transition();

		rv.duration = t.duration;
		rv.exitTime = t.exitTime;
		rv.hasExitTime = t.hasExitTime;
		rv.hasFixedDuration = t.hasFixedDuration;
		rv.offset = t.offset;
		var smName = t.destinationStateMachine != null ? t.destinationStateMachine.name : asl.stateMachine.name;
		var stateName = t.destinationState != null ? t.destinationState.name : t.destinationStateMachine.defaultState.name;
		rv.targetStateName = ConstructCompoundStateName(stateName, smName);
		rv.conditions = new UnsafeList<RTP.Condition>(t.conditions.Length, Allocator.Persistent);
		rv.soloFlag = t.solo;
		rv.muteFlag = t.mute;
		rv.canTransitionToSelf = t.canTransitionToSelf;

		if (t.name != "")
		{ 
			rv.name = t.name;
		}
		else
		{
			var sourceStateName = ConstructCompoundStateName(ownStateName, asm.name);
			rv.name = $"{sourceStateName} -> {rv.targetStateName}";
		}

		for (int i = 0; i < t.conditions.Length; ++i)
		{
			var c = t.conditions[i];
			rv.conditions.Add(GenerateControllerConditionComputationData(c, allParams));
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.ChildMotion GenerateChildMotionComputationData(ChildMotion cm, List<int> allClipsHashCodes)
	{
		var rv = new RTP.ChildMotion();
		rv.threshold = cm.threshold;
		rv.timeScale = cm.timeScale;
		rv.directBlendParameterName = cm.directBlendParameter;
		//	Data for 2D blend trees
		rv.position2D = cm.position;
		rv.motion = GenerateMotionComputationData(cm.motion, allClipsHashCodes);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.Motion GenerateMotionComputationData(Motion m, List<int> allClipsHashCodes)
	{
		var rv = new RTP.Motion();
		rv.animationIndex = -1;

		if (m == null)
			return rv;

		rv.name = m.name;

		var anm = m as AnimationClip;
		if (anm)
		{
			rv.animationIndex = allClipsHashCodes.IndexOf(anm.GetHashCode());
			rv.type = MotionBlob.Type.AnimationClip;
		}

		var bt = m as BlendTree;
		if (bt)
		{
			rv.type = bt.blendType switch
			{
				BlendTreeType.Simple1D => MotionBlob.Type.BlendTree1D,
				BlendTreeType.Direct => MotionBlob.Type.BlendTreeDirect,
				BlendTreeType.SimpleDirectional2D => MotionBlob.Type.BlendTree2DSimpleDirectional,
				BlendTreeType.FreeformDirectional2D => MotionBlob.Type.BlendTree2DFreeformDirectional,
				BlendTreeType.FreeformCartesian2D => MotionBlob.Type.BlendTree2DFreeformCartesian,
				_ => MotionBlob.Type.None
			};
			rv.blendTree = new RTP.BlendTree();
			rv.blendTree.name = bt.name;
			rv.blendTree.motions = new UnsafeList<RTP.ChildMotion>(bt.children.Length, Allocator.Persistent);
			rv.blendTree.blendParameterName = bt.blendParameter;
			rv.blendTree.blendParameterYName = bt.blendParameterY;
			rv.blendTree.normalizeBlendValues = GetNormalizedBlendValuesProp(bt);
			for (int i = 0; i < bt.children.Length; ++i)
			{
				var c = bt.children[i];
				if (c.motion != null)
				{
					var childMotion = GenerateChildMotionComputationData(bt.children[i], allClipsHashCodes);
					rv.blendTree.motions.Add(childMotion);
				}
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	bool GetNormalizedBlendValuesProp(BlendTree bt)
	{
		//	Hacky way to extract "Normalized Blend Values" prop
		var rv = false;
		using (var so = new SerializedObject(bt))
		{
			var p = so.FindProperty("m_NormalizedBlendValues");
			if (p != null)
				rv = p.boolValue;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	RTP.State GenerateControllerStateComputationData
	(
		AnimatorState state,
		AnimatorStateMachine asm,
		AnimatorControllerLayer acl,
		AnimatorControllerLayer aclOverriden,
		List<int> allClipsHashCodes,
		in UnsafeList<RTP.Parameter> allParams
	)
	{
		var rv = new RTP.State();
		rv.name = ConstructCompoundStateName(state.name, asm.name);
		
		rv.speed = state.speed;
		rv.speedMultiplierParameter = state.speedParameterActive ? state.speedParameter : "";
		rv.transitions = new UnsafeList<RTP.Transition>(state.transitions.Length, Allocator.Persistent);

		for (int i = 0; i < state.transitions.Length; ++i)
		{
			var t = state.transitions[i];
			var generatedTransition = GenerateControllerTransitionComputationData(t, state.name, asm, acl, allParams);
			rv.transitions.Add(generatedTransition);
		}

		FilterSoloAndMuteTransitions(ref rv.transitions);

		var motion = aclOverriden.GetOverrideMotion(state);
		if (motion == null)
			motion = state.motion;

		rv.motion = GenerateMotionComputationData(motion, allClipsHashCodes);
		if (state.timeParameterActive)
			rv.timeParameter = state.timeParameter;

		rv.cycleOffset = state.cycleOffset;
		if (state.cycleOffsetParameterActive)
			rv.cycleOffsetParameter = state.cycleOffsetParameter;

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void FilterSoloAndMuteTransitions(ref UnsafeList<RTP.Transition> transitions)
	{
		var hasSoloTransitions = false;
		var l = transitions.Length;
		for (int i = 0; i < l && !hasSoloTransitions; ++i)
		{
			hasSoloTransitions = transitions[i].soloFlag;
		}

		for (int i = 0; i < l;)
		{
			var t = transitions[i];
			//	According to documentation mute flag has precedence
			if (t.muteFlag)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else if (!t.soloFlag && hasSoloTransitions)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else
			{
				++i;
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	bool GenerateControllerStateMachineComputationData
	(
		AnimatorStateMachine asm,
		AnimatorControllerLayer acl,
		AnimatorControllerLayer aclOverriden,
		List<int> allClipsHashCodes,
		ref UnsafeList<RTP.State> sl,
		ref UnsafeList<RTP.Transition> anyStateTransitions,
		in UnsafeList<RTP.Parameter> allParams
	)
	{
		for (int k = 0; k < asm.anyStateTransitions.Length; ++k)
		{
			var ast = asm.anyStateTransitions[k];
			var stateName = ConstructCompoundStateName("Any State", asm.name);
			var t = GenerateControllerTransitionComputationData(ast, stateName, asm, acl, allParams);
			anyStateTransitions.Add(t);
		}

		FilterSoloAndMuteTransitions(ref anyStateTransitions);

		for (int i = 0; i < asm.states.Length; ++i)
		{
			var s = asm.states[i];
			var generatedState = GenerateControllerStateComputationData(s.state, asm, acl, aclOverriden, allClipsHashCodes, allParams);
			sl.Add(generatedState);
		}

		for (int j = 0; j < asm.stateMachines.Length; ++j)
		{
			var sm = asm.stateMachines[j];
			GenerateControllerStateMachineComputationData(sm.stateMachine, acl, aclOverriden, allClipsHashCodes, ref sl, ref anyStateTransitions, allParams);
		}
		return true;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	AnimationClip[] Deduplicate(AnimationClip[] animationClips)
	{
		var dedupList = new List<AnimationClip>();
		using var dupSet = new NativeHashSet<int>(animationClips.Length, Allocator.Temp);

		foreach (var a in animationClips)
		{
			if (!dupSet.Add(a.GetHashCode()))
			{
				continue;
			}

			dedupList.Add(a);
		}
		return dedupList.ToArray();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeList<RTP.AnimationClip> ConvertAllControllerAnimations(AnimationClip[] animationClips)
	{
		animationClips = Deduplicate(animationClips);

		var rv = new UnsafeList<RTP.AnimationClip>(animationClips.Length, Allocator.Persistent);

		foreach (var a in animationClips)
		{
			var acd = AnimationClipBaker.PrepareAnimationComputeData(a);
			rv.Add(acd);
		}

		return rv;
	}
}
}

#endif