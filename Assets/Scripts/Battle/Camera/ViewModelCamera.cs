using System;
using UnityEngine;

namespace Battle.Camera
{
    public class ViewModelCamera: MonoBehaviour
    {
        public static UnityEngine.Camera Instance;

        private void Awake()
        {
            Instance = GetComponent<UnityEngine.Camera>();
        }
    }
}