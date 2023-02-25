using System;
using Battle.Weapon;
using UnityEngine;

namespace Battle
{
    public class GameGlobalConfigs : MonoBehaviour
    {
        public static GameGlobalConfigs Instance;
        
        
        [SerializeField]
        private RecoilConfig recoilConfig;
        public RecoilConfig RecoilConfig => recoilConfig;
        
        [SerializeField]
        private SpreadConfig spreadConfig;
        public SpreadConfig SpreadConfig => spreadConfig;

        private void Awake()
        {
            Instance = this;
        }
    }
}