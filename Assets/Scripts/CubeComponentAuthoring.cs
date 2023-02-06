using Unity.Entities;
using Unity.NetCode;
using UnityEngine;


public struct CubeComponent : IComponentData
{
    [GhostField] 
    public int TestValue;
}

[DisallowMultipleComponent]
public class CubeComponentAuthoring : MonoBehaviour
{
    [SerializeField]
    private int testValue = 0;
    
    class MovableCubeComponentBaker : Baker<CubeComponentAuthoring>
    {
        public override void Bake(CubeComponentAuthoring authoring)
        {
            CubeComponent component = default(CubeComponent);
            component.TestValue = authoring.testValue;
            AddComponent(component);
        }
    }
}