using System;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;
using CapsuleCollider = UnityEngine.CapsuleCollider;
using Collider = UnityEngine.Collider;
using SphereCollider = UnityEngine.SphereCollider;

namespace Editor
{
    [ExecuteInEditMode]
    public class ColliderConverter: MonoBehaviour
    {
        public bool Convert;
        public bool RemovePhysicsShapes;
        public bool RemoveColliders;
#if UNITY_EDITOR
        private void Update()
        {
            if (Convert)
            {
                ConvertColliders();
            }
            if (RemovePhysicsShapes)
            {
                RemovePhysicsShape();
            }
            if (RemoveColliders)
            {
                RemoveCollider();
            }
            Convert = false;
            RemovePhysicsShapes = false;
            RemoveColliders = false;
        }

        public void ConvertColliders()
        {
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                var physicsShape = collider.gameObject.AddComponent<PhysicsShapeAuthoring>();
                if (collider is BoxCollider)
                {
                    var boxCollider = (BoxCollider) collider;
                    BoxGeometry boxGeometry = new BoxGeometry();
                    boxGeometry.Size = boxCollider.size;
                    boxGeometry.Center = boxCollider.center;
                    physicsShape.SetBox(boxGeometry);
                }

                if (collider is SphereCollider)
                {
                    var sphereCollider = (SphereCollider) collider;
                    SphereGeometry sphereGeometry = new SphereGeometry();
                    sphereGeometry.Radius = sphereCollider.radius;
                    sphereGeometry.Center = sphereCollider.center;
                    physicsShape.SetSphere(sphereGeometry, quaternion.identity);
                }

                if (collider is CapsuleCollider)
                {
                    var capsuleCollider = (CapsuleCollider) collider;
                    var capsuleGeometry = new CapsuleGeometryAuthoring();
                    capsuleGeometry.Radius = capsuleCollider.radius;
                    capsuleGeometry.Height = capsuleCollider.height;
                    capsuleGeometry.Center = capsuleCollider.center;
                    var orientation = new float3();
                    switch (capsuleCollider.direction)
                    {
                        case 0:
                            orientation = new float3(0, math.PI / 2, 0);
                            break;
                        case 1:
                            orientation = new float3(math.PI / 2, 0, 0);
                            break;
                        case 2:
                            orientation = new float3(0, 0, math.PI / 2);
                            break;
                    }

                    capsuleGeometry.Orientation = quaternion.Euler(orientation);
                    
                    physicsShape.SetCapsule(capsuleGeometry);
                }
            }
        }
        
        public void RemovePhysicsShape()
        {
            var physicsShapes = GetComponentsInChildren<PhysicsShapeAuthoring>();
            foreach (var physicsShape in physicsShapes)
            {
                DestroyImmediate(physicsShape);
            }
        }
        public void RemoveCollider()
        {
            var physicsColliders = GetComponentsInChildren<Collider>();
            foreach (var physicsCollider in physicsColliders)
            {
                DestroyImmediate(physicsCollider);
            }
        }
#endif
    }
}