using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MainCameraSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (MainGameObjectCamera.Instance != null && SystemAPI.HasSingleton<MainEntityCamera>())
        {
            Entity mainEntityCameraEntity = SystemAPI.GetSingletonEntity<MainEntityCamera>();
            MainEntityCamera mainEntityCamera = SystemAPI.GetSingleton<MainEntityCamera>();
            LocalToWorld targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(mainEntityCameraEntity);
            
            MainGameObjectCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position, targetLocalToWorld.Rotation);
            MainGameObjectCamera.Instance.fieldOfView = mainEntityCamera.CurrentFoV;
        }
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ViewModelCameraSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (Battle.Camera.ViewModelCamera.Instance != null && SystemAPI.HasSingleton<ViewModelCamera>())
        {
            Entity viewModelCameraEntity = SystemAPI.GetSingletonEntity<ViewModelCamera>();
            ViewModelCamera viewModelCamera = SystemAPI.GetSingleton<ViewModelCamera>();
            LocalToWorld targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(viewModelCameraEntity);
            
            Battle.Camera.ViewModelCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position, targetLocalToWorld.Rotation);
            Battle.Camera.ViewModelCamera.Instance.fieldOfView = viewModelCamera.CurrentFoV;
        }
    }
}