using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{ 
public partial struct AnimatorControllerSystem
{

[BurstCompile]
struct StateMachineProcessJob: IJobChunk
{
	public float dt;
	public int frameIndex;
	public BufferTypeHandle<AnimatorControllerLayerComponent> controllerLayersBufferHandle;
	public BufferTypeHandle<AnimatorControllerParameterComponent> controllerParametersBufferHandle;
	public EntityCommandBuffer.ParallelWriter ecbp;
	[ReadOnly]
	public EntityTypeHandle entityTypeHandle;

#if RUKHANKA_DEBUG_INFO
	public bool doLogging;
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
	{
		var layerBuffers = chunk.GetBufferAccessor(ref controllerLayersBufferHandle);
		var parameterBuffers = chunk.GetBufferAccessor(ref controllerParametersBufferHandle);
		var entities = chunk.GetNativeArray(entityTypeHandle);

		var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

		while (cee.NextEntityIndex(out var i))
		{
			var layers = layerBuffers[i];
			var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i] : default;
			var e = entities[i];

			ExecuteSingle(e, ref layers, ref parameters, unfilteredChunkIndex);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void ExecuteSingle(Entity e, ref DynamicBuffer<AnimatorControllerLayerComponent> aclc, ref DynamicBuffer<AnimatorControllerParameterComponent> acpc, int sortKey)
	{
		for (int i = 0; i < aclc.Length; ++i)
		{
			ref var acc = ref aclc.ElementAt(i);
			ProcessLayer(ref acc.controller.Value, acc.layerIndex, ref acpc, dt, frameIndex, ref acc);
		}
		AddAnimationsForEntity(ref ecbp, sortKey, aclc, e, acpc);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	RuntimeAnimatorData.StateRuntimeData InitRuntimeStateData(int stateID, float stateDuration, ref BlobArray<StateBlob> states, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		ref var sb = ref states[stateID];

		var cycleOffset = sb.cycleOffset;
		if (sb.cycleOffsetParameterIndex >= 0)
		{
			cycleOffset = runtimeParams[sb.cycleOffsetParameterIndex].FloatValue;
		}

		var rv = new RuntimeAnimatorData.StateRuntimeData();
		rv.id = stateID;
		rv.normalizedDuration = cycleOffset / stateDuration;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void ProcessLayer(ref ControllerBlob c, int layerIndex, ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float dt, int frameCount, ref AnimatorControllerLayerComponent acc)
	{
		ref var layer = ref c.layers[layerIndex];

		var currentStateID = acc.rtd.srcState.id;
		if (currentStateID < 0)
			currentStateID = layer.defaultStateIndex;

		ref var currentState = ref layer.states[currentStateID];
		var curStateDuration = CalculateStateDuration(ref currentState, runtimeParams);

		if (Hint.Unlikely(acc.rtd.srcState.id < 0))
		{
			acc.rtd.srcState = InitRuntimeStateData(layer.defaultStateIndex, curStateDuration, ref layer.states, runtimeParams);
		}

		var srcStateDurationFrameDelta = dt / curStateDuration;
		acc.rtd.srcState.normalizedDuration += srcStateDurationFrameDelta;

		if (acc.rtd.dstState.id >= 0)
		{
			var dstStateDuration = CalculateStateDuration(ref layer.states[acc.rtd.dstState.id], runtimeParams);
			acc.rtd.dstState.normalizedDuration += dt / dstStateDuration;
		}

		if (acc.rtd.activeTransition.id >= 0)
		{
			ref var currentTransitionBlob = ref currentState.transitions[acc.rtd.activeTransition.id];
			var transitionDuration = CalculateTransitionDuration(ref currentTransitionBlob, curStateDuration);
			acc.rtd.activeTransition.normalizedDuration += dt / transitionDuration;
		}

	#if RUKHANKA_DEBUG_INFO
		if (doLogging)
			Debug.Log($"[{frameCount}:{c.name.ToFixedString()}:{layer.name.ToFixedString()}] In state: '{currentState.name.ToFixedString()}' with normalized duration: {acc.rtd.srcState.normalizedDuration}");
	#endif

		//	Check conditions if not in active transitions
		if (acc.rtd.activeTransition.id < 0)
		{
			for (int i = 0; i < currentState.transitions.Length; ++i)
			{
				ref var t = ref currentState.transitions[i];
				var b = CheckTransitionEnterExitTimeCondition(ref t, acc.rtd.srcState, srcStateDurationFrameDelta) &&
						CheckTransitionEnterConditions(ref t, ref runtimeParams);
				if (b)
				{
				#if RUKHANKA_DEBUG_INFO
					if (doLogging)
						Debug.Log($"[{frameCount}:{c.name.ToFixedString()}:{layer.name.ToFixedString()}] Entering Transition: '{t.name.ToFixedString()}'");
				#endif

					var timeShouldBeInTransition = GetTimeInSecondsShouldBeInTransition(ref t, acc.rtd.srcState, curStateDuration, srcStateDurationFrameDelta);
					acc.rtd.activeTransition.id	= i;
					acc.rtd.activeTransition.normalizedDuration = timeShouldBeInTransition / CalculateTransitionDuration(ref t, curStateDuration);
					var dstStateDur = CalculateStateDuration(ref layer.states[t.targetStateId], runtimeParams) + t.offset;
					acc.rtd.dstState = InitRuntimeStateData(t.targetStateId, dstStateDur, ref layer.states, runtimeParams);
					acc.rtd.dstState.normalizedDuration += timeShouldBeInTransition / dstStateDur;
					break;
				}
			}
		}

		if (acc.rtd.activeTransition.id >= 0)
		{
			ref var t = ref currentState.transitions[acc.rtd.activeTransition.id];
			ref var dstState = ref layer.states[acc.rtd.dstState.id];
		#if RUKHANKA_DEBUG_INFO
			if (doLogging)
			{
				Debug.Log($"[{frameCount}:{c.name.ToFixedString()}:{layer.name.ToFixedString()}] In transition: '{t.name.ToFixedString()}' with time: {acc.rtd.activeTransition.normalizedDuration}");
				Debug.Log($"[{frameCount}:{c.name.ToFixedString()}:{layer.name.ToFixedString()}] Target state: '{dstState.name.ToFixedString()}' with time: {acc.rtd.dstState.normalizedDuration}");
			}
		#endif

			if (CheckTransitionExitConditions(acc.rtd.activeTransition))
			{
			#if RUKHANKA_DEBUG_INFO
				if (doLogging)
					Debug.Log($"[{frameCount}:{c.name.ToFixedString()}:{layer.name.ToFixedString()}] Exiting transition: '{t.name.ToFixedString()}'");
			#endif
				acc.rtd.srcState = acc.rtd.dstState;
				acc.rtd.dstState = acc.rtd.activeTransition = RuntimeAnimatorData.StateRuntimeData.MakeDefault();
			}
		}

		ProcessTransitionInterruptions();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	//	p0 = (0,0)
	(float, float, float) CalculateBarycentric(float2 p1, float2 p2, float2 pt)
	{
		var np2 = new float2(0 - p2.y, p2.x - 0);
		var np1 = new float2(0 - p1.y, p1.x - 0);

		var l1 = math.dot(pt, np2) / math.dot(p1, np2);
		var l2 = math.dot(pt, np1) / math.dot(p2, np1);
		var l0 = 1 - l1 - l2;
		return (l0, l1, l2);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe void HandleCentroidCase(ref NativeList<MotionIndexAndWeight> rv, float2 pt, ref BlobArray<ChildMotionBlob> mbArr)
	{
		if (math.any(pt))
			return;

		int i = 0;
		for (; i < mbArr.Length && math.any(mbArr[i].position2D); ++i) { }

		if (i < mbArr.Length)
		{
			var miw = new MotionIndexAndWeight() { motionIndex = i, weight = 1 };
			rv.Add(miw);
		}
		else
		{
			var f = 1.0f / mbArr.Length;
			for (int l = 0; l < mbArr.Length; ++l)
			{
				var miw = new MotionIndexAndWeight() { motionIndex = l, weight = f };
				rv.Add(miw);
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DSimpleDirectionalCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		var rv = new NativeList<MotionIndexAndWeight>(Allocator.Temp);
		var pX = runtimeParams[mb.blendTree.blendParameterIndex];
		var pY = runtimeParams[mb.blendTree.blendParameterYIndex];
		var pt = new float2(pX.FloatValue, pY.FloatValue);
		ref var motions = ref mb.blendTree.motions;

		if (motions.Length < 2)
		{
			if (motions.Length == 1)
				rv.Add(new MotionIndexAndWeight() { weight = 1, motionIndex = 0 });
			return rv;
		}

		HandleCentroidCase(ref rv, pt, ref motions);
		if (rv.Length > 0)
			return rv;

		var centerPtIndex = -1;
		//	Loop over all directions and search for sector that contains requested point
		var dotProductsAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
		for (int i = 0; i < motions.Length; ++i)
		{
			ref var m = ref motions[i];
			var motionDir = m.position2D;
			if (!math.any(motionDir))
			{
				centerPtIndex = i;
				continue;
			}
			var angle = math.atan2(motionDir.y, motionDir.x);
			var miw = new MotionIndexAndWeight() { motionIndex = i, weight = angle };
			dotProductsAndWeights.Add(miw);
		}

		var ptAngle = math.atan2(pt.y, pt.x);

		dotProductsAndWeights.Sort();

		// Pick two closest points
		MotionIndexAndWeight d0 = default, d1 = default;
		var l = 0;
		for (; l < dotProductsAndWeights.Length; ++l)
		{
			var d = dotProductsAndWeights[l];
			if (d.weight < ptAngle)
			{
				var ld0 = l == 0 ? dotProductsAndWeights.Length - 1 : l - 1;
				d1 = d;
				d0 = dotProductsAndWeights[ld0];
				break;
			}
		}

		//	Handle last sector
		if (l == dotProductsAndWeights.Length)
		{
			d0 = dotProductsAndWeights[dotProductsAndWeights.Length - 1];
			d1 = dotProductsAndWeights[0];
		}

		ref var m0 = ref motions[d0.motionIndex];
		ref var m1 = ref motions[d1.motionIndex];
		var p0 = m0.position2D;
		var p1 = m1.position2D;
		
		//	Barycentric coordinates for point pt in triangle <p0,p1,0>
		var (l0, l1, l2) = CalculateBarycentric(p0, p1, pt);

		var m0Weight = l1;
		var m1Weight = l2;
		if (l0 < 0)
		{
			var sum = m0Weight + m1Weight;
			m0Weight /= sum;
			m1Weight /= sum;
		}	

		l0 = math.saturate(l0);

		var evenlyDistributedMotionWeight = centerPtIndex < 0 ? 1.0f / motions.Length * l0 : 0;

		var miw0 = new MotionIndexAndWeight() { motionIndex = d0.motionIndex, weight = m0Weight + evenlyDistributedMotionWeight };
		rv.Add(miw0);

		var miw1 = new MotionIndexAndWeight() { motionIndex = d1.motionIndex, weight = m1Weight + evenlyDistributedMotionWeight };
		rv.Add(miw1);

		//	Add other motions of blend tree
		if (evenlyDistributedMotionWeight > 0)
		{
			for (int i = 0; i < motions.Length; ++i)
			{
				if (i != d0.motionIndex && i != d1.motionIndex)
				{
					var miw = new MotionIndexAndWeight() { motionIndex = i, weight = evenlyDistributedMotionWeight };
					rv.Add(miw);
				}
			}
		}

		//	Add centroid motion
		if (centerPtIndex >= 0)
		{
			var miw = new MotionIndexAndWeight() { motionIndex = centerPtIndex, weight = l0 };
			rv.Add(miw);
		}

		dotProductsAndWeights.Dispose();

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformCartesianCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		var pX = runtimeParams[mb.blendTree.blendParameterIndex];
		var pY = runtimeParams[mb.blendTree.blendParameterYIndex];
		var p = new float2(pX.FloatValue, pY.FloatValue);
		ref var motions = ref mb.blendTree.motions;
		Span<float> hpArr = stackalloc float[motions.Length];

		var hpSum = 0.0f;

		//	Calculate influence factors
		for (int i = 0; i < motions.Length; ++i)
		{
			var pi = motions[i].position2D;
			var pip = p - pi;

			var w = 1.0f;

			for (int j = 0; j < motions.Length && w > 0; ++j)
			{
				if (i == j) continue;
				var pj = motions[j].position2D;
				var pipj = pj - pi;
				var f = math.dot(pip, pipj) / math.lengthsq(pipj);
				var hj = math.max(1 - f, 0);
				w = math.min(hj, w);
			}
			hpSum += w;
			hpArr[i] = w;
		}

		var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
		//	Calculate weight functions
		for (int i = 0; i < motions.Length; ++i)
		{
			var w = hpArr[i] / hpSum;
			if (w > 0)
			{
				var miw = new MotionIndexAndWeight() { motionIndex = i, weight = w };
				rv.Add(miw);
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float CalcAngle(float2 a, float2 b)
	{
		var cross = a.x * b.y - a.y * b.x;
		var dot = math.dot(a, b);
		var tanA = new float2(cross, dot);
		var rv = math.atan2(tanA.x, tanA.y);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float2 CalcAngleWeights(float2 i, float2 j, float2 s)
	{
		float2 rv = 0;
		if (!math. any(i))
		{
			rv.x = CalcAngle(j, s);
			rv.y = 0;
		}
		else if (!math.any(j))
		{
			rv.x = CalcAngle(i, s);
			rv.y = rv.x;
		}
		else
		{
			rv.x = CalcAngle(i, j);
			if (!math.any(s))
				rv.y = rv.x;
			else
				rv.y = CalcAngle(i, s);
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformDirectionalCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		var pX = runtimeParams[mb.blendTree.blendParameterIndex];
		var pY = runtimeParams[mb.blendTree.blendParameterYIndex];
		var p = new float2(pX.FloatValue, pY.FloatValue);
		var lp = math.length(p);

		ref var motions = ref mb.blendTree.motions;
		Span<float> hpArr = stackalloc float[motions.Length];

		var hpSum = 0.0f;

		//	Calculate influence factors
		for (int i = 0; i < motions.Length; ++i)
		{
			var pi = motions[i].position2D;
			var lpi = math.length(pi);

			var w = 1.0f;

			for (int j = 0; j < motions.Length && w > 0; ++j)
			{
				if (i == j) continue;
				var pj = motions[j].position2D;
				var lpj = math.length(pj);

				var pRcpMiddle = math.rcp((lpj + lpi) * 0.5f);
				var lpip = (lp - lpi) * pRcpMiddle;
				var lpipj = (lpj - lpi) * pRcpMiddle;
				var angleWeights = CalcAngleWeights(pi, pj, p);

				var pip = new float2(lpip, angleWeights.y);
				var pipj = new float2(lpipj, angleWeights.x);

				var f = math.dot(pip, pipj) / math.lengthsq(pipj);
				var hj = math.saturate(1 - f);
				w = math.min(hj, w);
			}
			hpSum += w;
			hpArr[i] = w;	
		}

		var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
		//	Calculate weight functions
		for (int i = 0; i < motions.Length; ++i)
		{
			var w = hpArr[i] / hpSum;
			if (w > 0)
			{
				var miw = new MotionIndexAndWeight() { motionIndex = i, weight = w };
				rv.Add(miw);
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////
	
	unsafe NativeList<MotionIndexAndWeight> GetBlendTree1DCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		var blendTreeParameter = runtimeParams[mb.blendTree.blendParameterIndex];
		ref var motions = ref mb.blendTree.motions;
		var i0 = 0;
		var i1 = 0;
		bool found = false;
		for (int i = 0; i < motions.Length && !found; ++i)
		{
			ref var m = ref motions[i];
			i0 = i1;
			i1 = i;
			if (m.threshold > blendTreeParameter.FloatValue)
				found = true;
		}
		if (!found)
		{
			i0 = i1 = motions.Length - 1;
		}

		var motion0Threshold = motions[i0].threshold;
		var motion1Threshold = motions[i1].threshold;
		float f = i1 == i0 ? 0 : (blendTreeParameter.FloatValue - motion0Threshold) / (motion1Threshold - motion0Threshold);

		var rv = new NativeList<MotionIndexAndWeight>(2, Allocator.TempJob);
		rv.Add(new MotionIndexAndWeight { motionIndex = i0, weight = 1 - f });
		rv.Add(new MotionIndexAndWeight { motionIndex = i1, weight = f });
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe NativeList<MotionIndexAndWeight> GetBlendTreeDirectCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		ref var motions = ref mb.blendTree.motions;
		var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.TempJob);

		var weightSum = 0.0f;
		for (int i = 0; i < motions.Length; ++i)
		{
			ref var cm = ref motions[i];
			var w = cm.directBlendParameterIndex >= 0 ? runtimeParams[cm.directBlendParameterIndex].FloatValue : 0;
			if (w > 0)
			{
				var miw = new MotionIndexAndWeight() { motionIndex = i, weight = w };
				weightSum += miw.weight;
				rv.Add(miw);
			}
		}

		if (mb.blendTree.normalizeBlendValues && weightSum > 1)
		{
			for (int i = 0; i < rv.Length; ++i)
			{
				var miw = rv[i];
				miw.weight = miw.weight / weightSum;
				rv[i] = miw;
			}
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	unsafe float CalculateMotionDuration(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float weight)
	{
		if (weight == 0) return 0;

		NativeList<MotionIndexAndWeight> blendTreeMotionsAndWeights = default;
		switch (mb.type)
		{
		case MotionBlob.Type.None:
			return 1;
		case MotionBlob.Type.AnimationClip:
			return mb.animationBlob.Value.length * weight;
		case MotionBlob.Type.BlendTreeDirect:
			blendTreeMotionsAndWeights = GetBlendTreeDirectCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree1D:
			blendTreeMotionsAndWeights = GetBlendTree1DCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DSimpleDirectional:
			blendTreeMotionsAndWeights = GetBlendTree2DSimpleDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformCartesian:
			blendTreeMotionsAndWeights = GetBlendTree2DFreeformCartesianCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformDirectional:
			blendTreeMotionsAndWeights = GetBlendTree2DFreeformDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		default:
			Debug.Log($"Unsupported blend tree type");
			break;
		}

		var rv = CalculateBlendTreeMotionDuration(blendTreeMotionsAndWeights, ref mb.blendTree.motions, runtimeParams, weight);
		if (blendTreeMotionsAndWeights.IsCreated) blendTreeMotionsAndWeights.Dispose();
		
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float CalculateBlendTreeMotionDuration(NativeList<MotionIndexAndWeight> miwArr, ref BlobArray<ChildMotionBlob> motions, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float weight)
	{
		if (!miwArr.IsCreated || miwArr.IsEmpty)
			return 1;

		var weightSum = 0.0f;
		for (int i = 0; i < miwArr.Length; ++i)
			weightSum += miwArr[i].weight;

		//	If total weight less then 1, normalize weights
		if (Hint.Unlikely(weightSum < 1))
		{
			for (int i = 0; i < miwArr.Length; ++i)
			{
				var miw = miwArr[i];
				miw.weight = miw.weight / weightSum;
				miwArr[i] = miw;
			}
		}

		var rv = 0.0f;
		for (int i = 0; i < miwArr.Length; ++i)
		{
			var miw = miwArr[i];
			ref var m = ref motions[miw.motionIndex];
			rv += CalculateMotionDuration(ref m.motion, runtimeParams, weight * miw.weight) / m.timeScale;
		}

		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float CalculateTransitionDuration(ref TransitionBlob tb, float curStateDuration)
	{
		var rv = tb.duration;
		if (!tb.hasFixedDuration)
		{
			rv *= curStateDuration;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float CalculateStateDuration(ref StateBlob sb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		var motionDuration = CalculateMotionDuration(ref sb.motion, runtimeParams, 1);
		var speedMuliplier = 1.0f;
		if (sb.speedMultiplierParameterIndex >= 0)
		{
			speedMuliplier = runtimeParams[sb.speedMultiplierParameterIndex].FloatValue;
		}
		return motionDuration / (sb.speed * speedMuliplier);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float GetTimeInSecondsShouldBeInTransition(ref TransitionBlob tb, RuntimeAnimatorData.StateRuntimeData curStateRTD, float curStateDuration, float curStateFrameDelta)
	{
		if (!tb.hasExitTime) return 0;

		var intPart = tb.exitTime < 1 ? (int)(curStateRTD.normalizedDuration - curStateFrameDelta) : 0;
		var rv = (curStateRTD.normalizedDuration - tb.exitTime - intPart) * curStateDuration;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckTransitionEnterExitTimeCondition
	(
		ref TransitionBlob tb,
		RuntimeAnimatorData.StateRuntimeData curStateRuntimeData,
		float srcStateDurationFrameDelta
	)
	{
		var normalizedStateDuration = curStateRuntimeData.normalizedDuration; 

		var noNormalConditions = tb.conditions.Length == 0;
		if (!tb.hasExitTime) return !noNormalConditions;

		var cmpValue = tb.exitTime;
		var l0 = normalizedStateDuration - srcStateDurationFrameDelta;
		var l1 = normalizedStateDuration;
		if (tb.exitTime < 1)
		{
			cmpValue += (int)l0;
		}
		var rv = cmpValue > l0 && cmpValue <= l1;
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckIntCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
	{
		var rv = true;
		switch (c.conditionMode)
		{
		case AnimatorConditionMode.Equals:
			if (param.IntValue != c.threshold.intValue) rv = false;
			break;
		case AnimatorConditionMode.Greater:
			if (param.IntValue <= c.threshold.intValue) rv = false;
			break;
		case AnimatorConditionMode.Less:
			if (param.IntValue >= c.threshold.intValue) rv = false;
			break;
		case AnimatorConditionMode.NotEqual:
			if (param.IntValue == c.threshold.intValue) rv = false;
			break;
		default:
			Debug.LogError($"Unsupported condition type for int parameter value!");
			break;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckFloatCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
	{
		var rv = true;
		switch (c.conditionMode)
		{
		case AnimatorConditionMode.Greater:
			if (param.FloatValue <= c.threshold.floatValue) rv = false;
			break;
		case AnimatorConditionMode.Less:
			if (param.FloatValue >= c.threshold.floatValue) rv = false;
			break;
		default:
			Debug.LogError($"Unsupported condition type for int parameter value!");
			break;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckBoolCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
	{
		var rv = true;
		switch (c.conditionMode)
		{
		case AnimatorConditionMode.If:
			rv = param.BoolValue;
			break;
		case AnimatorConditionMode.IfNot:
			rv = !param.BoolValue;
			break;
		default:
			Debug.LogError($"Unsupported condition type for int parameter value!");
			break;
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckTransitionEnterConditions(ref TransitionBlob tb, ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
	{
		if (tb.conditions.Length == 0)
			return true;

		var rv = true;
		for (int i = 0; i < tb.conditions.Length && rv; ++i)
		{
			ref var c = ref tb.conditions[i];
			var param = runtimeParams[c.paramIdx];

			switch (param.type)
			{
			case ControllerParameterType.Float:
				rv = CheckFloatCondition(param, ref c);
				break;
			case ControllerParameterType.Int:
				rv = CheckIntCondition(param, ref c);
				break;
			case ControllerParameterType.Bool:
				rv = CheckBoolCondition(param, ref c);
				break;
			case ControllerParameterType.Trigger:
				rv = CheckBoolCondition(param, ref c);
				//	For trigger we need to reset value here
				if (rv)
				{
					param.value.boolValue = false;
					runtimeParams[c.paramIdx] = param;
				}
				break;
			}
		}
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	bool CheckTransitionExitConditions(RuntimeAnimatorData.StateRuntimeData transitionRuntimeData)
	{
		return transitionRuntimeData.normalizedDuration >= 1;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void AddAnimationForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims, ref MotionBlob mb, float weight, float normalizedStateTime)
	{
		if (weight == 0)
			return;

		var atp = new AnimationToProcessComponent();
		if (mb.animationBlob.IsValid)
			atp.animation = ExternalBlobPtr<AnimationClipBlob>.Create(ref mb.animationBlob);

		atp.weight = weight;
		atp.time = normalizedStateTime;
		outAnims.Add(atp);
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void AddMotionsFromBlendtree
	(
		in NativeList<MotionIndexAndWeight> miws,
		ref DynamicBuffer<AnimationToProcessComponent> outAnims,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
		ref BlobArray<ChildMotionBlob> motions,
		float weight,
		float normalizedStateTime
	)
	{
		for (int i = 0; i < miws.Length; ++i)
		{
			var miw = miws[i];
			ref var m = ref motions[miw.motionIndex];
			AddMotionForEntity(ref outAnims, ref m.motion, runtimeParams, weight * miw.weight, normalizedStateTime);
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	int AddMotionForEntity
	(
		ref DynamicBuffer<AnimationToProcessComponent> outAnims,
		ref MotionBlob mb,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
		float weight,
		float normalizedStateTime
	)
	{
		if (weight == 0)
			return 0;

		var startLen = outAnims.Length;
		NativeList<MotionIndexAndWeight> blendTreeMotionsAndWeights = default;

		switch (mb.type)
		{
		case MotionBlob.Type.None:
			break;
		case MotionBlob.Type.AnimationClip:
			AddAnimationForEntity(ref outAnims, ref mb, weight, normalizedStateTime);
			break;
		case MotionBlob.Type.BlendTreeDirect:
			blendTreeMotionsAndWeights = GetBlendTreeDirectCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree1D:
			blendTreeMotionsAndWeights = GetBlendTree1DCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DSimpleDirectional:
			blendTreeMotionsAndWeights = GetBlendTree2DSimpleDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformCartesian:
			blendTreeMotionsAndWeights = GetBlendTree2DFreeformCartesianCurrentMotions(ref mb, runtimeParams);
			break;
		case MotionBlob.Type.BlendTree2DFreeformDirectional:
			blendTreeMotionsAndWeights = GetBlendTree2DFreeformDirectionalCurrentMotions(ref mb, runtimeParams);
			break;
		}

		if (blendTreeMotionsAndWeights.IsCreated)
		{
			AddMotionsFromBlendtree(blendTreeMotionsAndWeights, ref outAnims, runtimeParams, ref mb.blendTree.motions, weight, normalizedStateTime);
			blendTreeMotionsAndWeights.Dispose();
		}

		return outAnims.Length - startLen;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	float GetDurationTime(ref StateBlob sb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float normalizedDuration)
	{
		var timeDuration = normalizedDuration;
		if (sb.timeParameterIndex >= 0)
		{
			timeDuration = runtimeParams[sb.timeParameterIndex].FloatValue;
		}
		return timeDuration;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void AddAnimationsForEntity
	(
		ref EntityCommandBuffer.ParallelWriter ecb,
		int sortKey,
		in DynamicBuffer<AnimatorControllerLayerComponent> aclc,
		Entity deformedEntity,
		in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams
	)
	{
		if (deformedEntity == Entity.Null)
			return;

		var animations = ecb.AddBuffer<AnimationToProcessComponent>(sortKey, deformedEntity);

		for (int i = 0; i < aclc.Length; ++i)
		{
			var animationCurIndex = animations.Length;

			ref var l = ref aclc.ElementAt(i);
			ref var cb = ref l.controller;
			ref var lb = ref cb.Value.layers[i];
			if (lb.weight == 0)
				continue;

			ref var srcStateBlob = ref lb.states[l.rtd.srcState.id];

			var srcStateWeight = 1.0f;
			var dstStateWeight = 0.0f;

			if (l.rtd.activeTransition.id >= 0)
			{
				ref var transitionBlob = ref srcStateBlob.transitions[l.rtd.activeTransition.id];
				dstStateWeight = l.rtd.activeTransition.normalizedDuration;
				srcStateWeight = (1 - dstStateWeight);
			}

			var srcStateTime = GetDurationTime(ref srcStateBlob, runtimeParams, l.rtd.srcState.normalizedDuration);

			if (l.rtd.dstState.id >= 0)
			{
				ref var dstStateBlob = ref lb.states[l.rtd.dstState.id];
				var dstStateTime = GetDurationTime(ref dstStateBlob, runtimeParams, l.rtd.dstState.normalizedDuration);
				AddMotionForEntity(ref animations, ref dstStateBlob.motion, runtimeParams, dstStateWeight, dstStateTime);
			}
			AddMotionForEntity(ref animations, ref srcStateBlob.motion, runtimeParams, srcStateWeight, srcStateTime);

			//	Set blending mode and adjust animations weight according to layer weight
			for (int k = animationCurIndex; k < animations.Length; ++k)
			{
				var a = animations[k];
				a.blendMode = lb.blendingMode;
				a.layerWeight = lb.weight;
				a.layerIndex = i;
				if (lb.avatarMask.hash.IsValid)
					a.avatarMask = ExternalBlobPtr<AvatarMaskBlob>.Create(ref lb.avatarMask);
				animations[k] = a;
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	void ProcessTransitionInterruptions()
	{
		// Not implemented yet
	}
}

//=================================================================================================================//

[BurstCompile]
partial struct CreateBoneRemapTablesJob: IJobEntity
{
	public NativeHashMap<Hash128, UnsafeHashMap<Hash128, int>> internalBoneMapsCache;

/////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(ref DynamicBuffer<AnimationToProcessComponent> atps)
	{ 
		for (int i = 0; i < atps.Length; ++i)	
		{
			var atp = atps[i];
			if (atp.animation.IsCreated)
			{
				atp.boneMap = GetBoneMapForAnimationSkeleton(ref atp.animation.Value, internalBoneMapsCache);
				atps[i] = atp;
			}
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	UnsafeHashMap<Hash128, int> GetBoneMapForAnimationSkeleton(ref AnimationClipBlob acb, NativeHashMap<Hash128, UnsafeHashMap<Hash128, int>> boneMapsCache)
	{
		var alreadyHave = boneMapsCache.TryGetValue(acb.hash, out var rv);
		if (alreadyHave)
			return rv;

		//	Build new one
		ref var bones = ref acb.bones;
		var newMap = new UnsafeHashMap<Hash128, int>(bones.Length, Allocator.Persistent);

		for (int i = 0; i < bones.Length; ++i)
		{
			var boneHash = bones[i].hash;
			newMap.Add(boneHash, i);
		}
		boneMapsCache.Add(acb.hash, newMap);
		return newMap;
	}

}

}
}
