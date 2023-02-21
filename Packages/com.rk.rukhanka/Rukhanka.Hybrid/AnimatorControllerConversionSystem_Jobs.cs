#if UNITY_EDITOR

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Hash128 = Unity.Entities.Hash128;
using System;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public partial class AnimatorControllerConversionSystem
{
//=================================================================================================================//

	[BurstCompile]
	struct CreateBlobAssetsJob: IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeSlice<AnimatorBlobAssets> outBlobAssets;
		public AnimatorControllerBakerData inData;

		void AddTransitionBlob(RTP.Transition t, UnsafeList<RTP.State> allStates, UnsafeList<RTP.Parameter> allParams, ref BlobBuilder bb, ref TransitionBlob tb)
		{
		#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref tb.name, ref t.name);
		#endif

			var bbc = bb.Allocate(ref tb.conditions, t.conditions.Length);
			for (int ci = 0; ci < t.conditions.Length; ++ci)
			{
				ref var cb = ref bbc[ci];
				var src = t.conditions[ci];
				cb.conditionMode = src.conditionMode;
				cb.paramIdx = allParams.IndexOf(src.paramName);
				cb.threshold = src.threshold;

			#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref cb.name, ref src.name);
			#endif
			}

			tb.duration = t.duration;
			tb.exitTime = t.exitTime;
			tb.hasExitTime = t.hasExitTime;
			tb.offset = t.offset;
			tb.hasFixedDuration = t.hasFixedDuration;
			tb.targetStateId = allStates.IndexOf(t.targetStateName);
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddChildMotionBlob(RTP.ChildMotion cm, ref BlobBuilder bb, ref ChildMotionBlob cmb, ref BlobBuilderArray<AnimationClipBlob> allAnims, in UnsafeList<RTP.Parameter> allParams)
		{
			cmb.threshold = cm.threshold;
			cmb.timeScale = cm.timeScale;
			cmb.position2D = cm.position2D;
			cmb.directBlendParameterIndex = allParams.IndexOf(cm.directBlendParameterName);
			AddMotionBlob(cm.motion, ref bb, ref cmb.motion, ref allAnims, allParams);
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddMotionBlob(RTP.Motion m, ref BlobBuilder bb, ref MotionBlob mb, ref BlobBuilderArray<AnimationClipBlob> allAnims, in UnsafeList<RTP.Parameter> allParams)
		{
		#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref mb.name, ref m.name);
		#endif

			mb.type = m.type;
			if (m.animationIndex >= 0 && m.type == MotionBlob.Type.AnimationClip)
			{
				ref var ab = ref bb.SetPointer(ref mb.animationBlob, ref allAnims[m.animationIndex]);
			}

			if (m.type != MotionBlob.Type.None && m.type != MotionBlob.Type.AnimationClip)
			{
				ref var bt = ref mb.blendTree;
				var bbm = bb.Allocate(ref bt.motions, m.blendTree.motions.Length);
				for (int i = 0; i < bbm.Length; ++i)
				{
					AddChildMotionBlob(m.blendTree.motions[i], ref bb, ref bbm[i], ref allAnims, allParams);
				}
				bt.blendParameterIndex = allParams.IndexOf(m.blendTree.blendParameterName);
				bt.blendParameterYIndex = allParams.IndexOf(m.blendTree.blendParameterYName);
				bt.normalizeBlendValues = m.blendTree.normalizeBlendValues;
			}
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddStateBlob(RTP.State s, ref BlobBuilder bb, ref StateBlob sb, ref BlobBuilderArray<AnimationClipBlob> allAnims, UnsafeList<RTP.Transition> anyStateTransitions, UnsafeList<RTP.State> allStates, UnsafeList<RTP.Parameter> allParams)
		{
		#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref sb.name, ref s.name);
		#endif

			sb.speed = s.speed;
			sb.speedMultiplierParameterIndex = allParams.IndexOf(s.speedMultiplierParameter);
			sb.timeParameterIndex = allParams.IndexOf(s.timeParameter);
			sb.cycleOffset = s.cycleOffset;
			sb.cycleOffsetParameterIndex = allParams.IndexOf(s.cycleOffsetParameter);

			var bbt = bb.Allocate(ref sb.transitions, s.transitions.Length + anyStateTransitions.Length);

			//	Any state transitions are first priority
			for (int ti = 0; ti < anyStateTransitions.Length; ++ti)
			{
				var ast = anyStateTransitions[ti];
				//	Do not add transitions to self according to flag
				if (ast.canTransitionToSelf || ast.targetStateName != s.name)
					AddTransitionBlob(ast, allStates, allParams, ref bb, ref bbt[ti]);
			}

			for (int ti = 0; ti < s.transitions.Length; ++ti)
			{
				var src = s.transitions[ti];
				AddTransitionBlob(src, allStates, allParams, ref bb, ref bbt[ti + anyStateTransitions.Length]);
			}

			//	Add motion
			AddMotionBlob(s.motion, ref bb, ref sb.motion, ref allAnims, allParams);
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddKeyFrameArray(UnsafeList<KeyFrame> kf, ref BlobBuilderArray<KeyFrame> outKf)
		{
			for (int i = 0; i < kf.Length; ++i)
			{
				outKf[i] = kf[i];
			}
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddBoneClipArr(ref BlobBuilder bb, ref BlobArray<BoneClipBlob> bonesBlob, in UnsafeList<RTP.BoneClip> inData)
		{
			var bonesArr = bb.Allocate(ref bonesBlob, inData.Length);
			for (int i = 0; i < bonesArr.Length; ++i)
			{
				ref var boneBlob = ref bonesArr[i];
				var boneInData = inData[i];

				var anmCurvesArr = bb.Allocate(ref boneBlob.animationCurves, boneInData.animationCurves.Length);
				for (int l = 0; l < boneInData.animationCurves.Length; ++l)
				{
					var anmCurveData = boneInData.animationCurves[l];
					ref var anmCurveBlob = ref anmCurvesArr[l];
					var keyFramesArr = bb.Allocate(ref anmCurveBlob.keyFrames, anmCurveData.keyFrames.Length);

					anmCurveBlob.channelIndex = anmCurveData.channelIndex;
					anmCurveBlob.bindingType = anmCurveData.bindingType;
					AddKeyFrameArray(anmCurveData.keyFrames, ref keyFramesArr);
				}

		#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref boneBlob.name, ref boneInData.name);
		#endif
				boneBlob.hash = boneInData.name.CalculateHash128();
			}
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	
		void AddAnimationClipBlob(RTP.AnimationClip ac, ref BlobBuilder bb, ref AnimationClipBlob acb)
		{
		#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref acb.name, ref ac.name);
		#endif

			acb.hash = new Hash128((uint)ac.hash, 5, 6, 7);
			AddBoneClipArr(ref bb, ref acb.bones, ac.bones);
			AddBoneClipArr(ref bb, ref acb.curves, ac.curves);

			acb.looped = ac.looped;
			acb.length = ac.length;
			acb.loopPoseBlend = ac.loopPoseBlend;
			acb.cycleOffset = ac.cycleOffset;
			acb.additiveReferencePoseTime = ac.additiveReferencePoseTime;
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddAvatarMaskBlob(RTP.AvatarMask am, ref BlobBuilder bb, ref AvatarMaskBlob amb)
		{
			amb.hash = am.hash;
			if (am.name.Length != 0)
			{
			#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref amb.name, ref am.name);
			#endif
			}

			var avatarMaskArr = bb.Allocate(ref amb.includedBoneHashes, am.includedBonePaths.Length);
			for (int i = 0; i < avatarMaskArr.Length; ++i)
			{
				var ibp = am.includedBonePaths[i];
				avatarMaskArr[i] = ibp.CalculateHash128();
			}

		#if RUKHANKA_DEBUG_INFO
			var avatarMaskNameArr = bb.Allocate(ref amb.includedBoneNames, am.includedBonePaths.Length);
			for (int i = 0; i < avatarMaskNameArr.Length; ++i)
			{
				var ibp = am.includedBonePaths[i];
				bb.AllocateString(ref avatarMaskNameArr[i], ref ibp);
			}
		#endif
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddAllAnimationClips(ref BlobBuilder bb, ref ControllerBlob c, in RTP.Controller data, out BlobBuilderArray<AnimationClipBlob> bbc)
		{
			bbc = bb.Allocate(ref c.animationClips, data.animationClips.Length);
			for (int ai = 0; ai < data.animationClips.Length; ++ai)
			{
				var src = data.animationClips[ai];
				ref var clip = ref bbc[ai];
				AddAnimationClipBlob(src, ref bb, ref clip);
			}
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		unsafe BlobAssetReference<ParameterPerfectHashTableBlob> AddAllParameters(ref BlobBuilder bb, ref ControllerBlob c, RTP.Controller data)
		{
			//	Create perfect hash table and reshuffle all parameters
			var hashesArr = new NativeArray<Hash128>(data.parameters.Length, Allocator.Temp);
			for (int l = 0; l < data.parameters.Length; ++l)
			{
				hashesArr[l] = data.parameters[l].name.CalculateHash128();
			}
			PerfectHash.CreateMinimalPerfectHash(hashesArr, out var seedValues, out var shuffleIndices);
			MathUtils.ShuffleList(data.parameters, shuffleIndices);

			//	Now place parameters in shuffled places
			var bba = bb.Allocate(ref c.parameters, data.parameters.Length);
			for	(int pi = 0; pi < data.parameters.Length; ++pi)
			{
				var src = data.parameters[pi];
				ref var p = ref bba[pi];
				p.defaultValue = src.defaultValue;
#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref p.name, ref src.name);
#endif
				p.hash = hashesArr[shuffleIndices[pi]];
				p.type = src.type;
			}

			shuffleIndices.Dispose();
			hashesArr.Dispose();

			//	Create separate blob asset for perfect hash table
			using var bb2 = new BlobBuilder(Allocator.Temp);
			ref var ppb = ref bb2.ConstructRoot<ParameterPerfectHashTableBlob>();
			var bbh = bb2.Allocate(ref ppb.seedTable, hashesArr.Length);
			for (var hi = 0; hi < hashesArr.Length; ++hi)
			{
				ref var paramRef = ref bbh[hi];
				paramRef = seedValues[hi];
			}

			seedValues.Dispose();
		
			var rv = bb2.CreateBlobAssetReference<ParameterPerfectHashTableBlob>(Allocator.Persistent);
			return rv;
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void AddAllLayers(ref BlobBuilder bb, ref ControllerBlob c, ref BlobBuilderArray<AnimationClipBlob> bbc, RTP.Controller data)
		{
			var bbl = bb.Allocate(ref c.layers, data.layers.Length);
			for (int li = 0; li < data.layers.Length; ++li)
			{
				var src = data.layers[li];
				ref var l = ref bbl[li];

			#if RUKHANKA_DEBUG_INFO
				bb.AllocateString(ref l.name, ref src.name);
			#endif

				l.defaultStateIndex = src.defaultStateIndex;
				l.blendingMode = src.blendMode;
				l.weight = src.weight;

				// States
				var bbs = bb.Allocate(ref l.states, src.states.Length);
				for (int si = 0; si < src.states.Length; ++si)
				{
					var s = src.states[si];
					AddStateBlob(s, ref bb, ref bbs[si], ref bbc, src.anyStateTransitions, src.states, data.parameters);
				}

				AddAvatarMaskBlob(src.avatarMask, ref bb, ref l.avatarMask);
			}
		}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		public void Execute()
		{
			var data = inData.controllerData;
			var bb = new BlobBuilder(Allocator.Temp);
			ref var c = ref bb.ConstructRoot<ControllerBlob>();

		#if RUKHANKA_DEBUG_INFO
			bb.AllocateString(ref c.name, ref data.name);
		#endif

			AddAllAnimationClips(ref bb, ref c, data, out var bbc);
			var parameterPerfectHashTableBlob = AddAllParameters(ref bb, ref c, data);
			AddAllLayers(ref bb, ref c, ref bbc, data);

			var rv = bb.CreateBlobAssetReference<ControllerBlob>(Allocator.Persistent);

			//	Entire slice has same blob assets
			for (var i = 0; i < outBlobAssets.Length; ++i)
			{
				outBlobAssets[i] = new AnimatorBlobAssets() { controllerBlob = rv, paremetersPerfectHashTableBlob = parameterPerfectHashTableBlob };
			}
		}
	}

//=================================================================================================================//

	[BurstCompile]
	struct CreateComponentDatasJob: IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<AnimatorControllerBakerData> bakerData;
		[ReadOnly]
		public NativeArray<AnimatorBlobAssets> blobAssets;

		public EntityCommandBuffer.ParallelWriter ecb;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		public unsafe void Execute(int startIndex, int count)
		{
			for (int i = startIndex; i < startIndex + count; ++i)
			{
				var e = bakerData[i].targetEntity;
				var ba = blobAssets[i];

				var acc = new AnimatorControllerLayerComponent();
				acc.rtd = RuntimeAnimatorData.MakeDefault();
				acc.controller = ba.controllerBlob;

				var buf = ecb.AddBuffer<AnimatorControllerLayerComponent>(startIndex, e);
				ref var cb = ref ba.controllerBlob.Value;
				for (int k = 0; k < cb.layers.Length; ++k)
				{
					acc.layerIndex = k;
					buf.Add(acc);
				}

				if (cb.parameters.Length > 0)
				{
					//	Add dynamic parameters
					var paramArray = ecb.AddBuffer<AnimatorControllerParameterComponent>(startIndex, e);
					for (int p = 0; p < cb.parameters.Length; ++p)
					{
						ref var pm = ref cb.parameters[p];
						var acpc = new AnimatorControllerParameterComponent()
						{
							value = pm.defaultValue,
							hash = pm.hash,
							type = pm.type,
						};

					#if RUKHANKA_DEBUG_INFO
						pm.name.CopyTo(ref acpc.name);
					#endif

						paramArray.Add(acpc);
					}

					//	Add perfect hash table used to fast runtime parameter value lookup
					var pht = new AnimatorControllerParameterIndexTableComponent()
					{
						seedTable = ba.paremetersPerfectHashTableBlob
					};
					ecb.AddComponent(startIndex, e, pht);
				}
			}
		}
	}
}
}

#endif