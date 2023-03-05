using System;
using Battle.Weapon;
using Unity.Entities;
using UnityEngine;

namespace Battle
{

    public static class GameGlobalConfigs
    {
        private static RecoilConfig recoilConfig;
        public static RecoilConfig RecoilConfig => recoilConfig;

        private static SpreadConfig spreadConfig;
        public static SpreadConfig SpreadConfig => spreadConfig;


        public static RecoilConfig LoadRecoilConfig()
        {
            if (recoilConfig == null)
            {
                recoilConfig = Resources.Load<RecoilConfig>("Config/RecoilConfig");
                if (recoilConfig == null)
                {
#if UNITY_EDITOR
                    throw new Exception("Load RecoilConfig failed");
#endif
                }
            }

            return recoilConfig;
        }

        public static SpreadConfig LoadSpreadConfig()
        {
            if (spreadConfig == null)
            {
                spreadConfig = Resources.Load<SpreadConfig>("Config/SpreadConfig");
                if (spreadConfig == null)
                {
#if UNITY_EDITOR
                    throw new Exception("Load SpreadConfig failed");
#endif
                }
            }

            return spreadConfig;
        }

    }
}