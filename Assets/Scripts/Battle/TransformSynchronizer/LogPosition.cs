using System;
using UnityEngine;

namespace Battle.TransformSynchronizer
{
    public class LogPosition : MonoBehaviour
    {
        private void Update()
        {
            Debug.Log(transform.position);
        }
    }
}