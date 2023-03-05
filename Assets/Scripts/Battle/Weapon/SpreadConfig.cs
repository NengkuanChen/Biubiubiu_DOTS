using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Weapon
{
    [CreateAssetMenu(fileName = "SpreadConfig", menuName = "SpreadConfig", order = 0)]
    public class SpreadConfig : ScriptableObject
    {
        [SerializeField]
        private List<SpreadType> spreadTypes = new List<SpreadType>();
        public List<SpreadType> SpreadTypes => spreadTypes;
    }

    [Serializable]
    public class SpreadType
    {
        public float MaxSpreadAngle;
        public float MinSpreadAngle;
        public AnimationCurve SpreadCurve;
        public float SpreadPercetageIncreasePerShot;
        public float SpreadPercentageDecreasePerSecond;
    }
}