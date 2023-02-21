using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Battle.ViewModel
{
    public class ViewModelManager: MonoBehaviour
    {
        public static ViewModelManager Instance;
        
        
        private PlayerViewModel currentViewModel;
        public PlayerViewModel CurrentViewModel => currentViewModel;

        private void Awake()
        {
            Instance = this;
        }


        public void InstantiateViewModel()
        {
            if (BattleGameObjectSpawner.Instance == null)
            {
                return;
            }

            if (currentViewModel != null)
            {
                Destroy(currentViewModel.gameObject);
            }

            currentViewModel = Instantiate(BattleGameObjectSpawner.Instance.ViewModelPrefab);
        }
    }
    
    
    public partial struct ViewModelTransformSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ViewModelCamera>();
            
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            if (ViewModelManager.Instance == null)
            {
                return;
            }

            if (ViewModelManager.Instance.CurrentViewModel == null)
            {
                return;
            }
            foreach (var (viewModelTransform, localToWorld) in SystemAPI.Query<LocalTransform, LocalToWorld>().WithAll<ViewModelCamera>())
            {
                ViewModelManager.Instance.CurrentViewModel.transform.SetPositionAndRotation(localToWorld.Position,
                    localToWorld.Rotation);
            }
        }
    }
}