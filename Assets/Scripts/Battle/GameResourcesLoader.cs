using System;
using UnityEngine;

namespace Battle
{
    public class GameResourcesLoader : MonoBehaviour
    {
        private void Awake()
        {
            GameGlobalConfigs.LoadRecoilConfig();
            GameGlobalConfigs.LoadSpreadConfig();
        }
    }
}