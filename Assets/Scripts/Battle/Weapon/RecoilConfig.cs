using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Weapon
{
    [CreateAssetMenu(fileName = "RecoilConfig", menuName = "RecoilConfig", order = 0)]
    public class RecoilConfig : ScriptableObject
    {
        [SerializeField]
        private List<RecoilType> recoilTypes = new List<RecoilType>();
        public List<RecoilType> RecoilTypes => recoilTypes;

    }

    [Serializable]
    public class RecoilType
    {
        public float MaxVerticalAngleOffset;
        public float MaxHorizontalAngleOffset;
        public AnimationCurve VerticalRecoilCurve;
        public AnimationCurve HorizontalRecoilCurve;
        public float MaxRecoveryTime;
        public AnimationCurve RecoveryCurve;
        public float ADSRecoilMultiplier;
    }
}