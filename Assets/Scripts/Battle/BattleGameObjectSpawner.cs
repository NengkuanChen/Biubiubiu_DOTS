using Battle.ViewModel;
using UnityEngine;

namespace Battle
{
    public class BattleGameObjectSpawner : MonoBehaviour
    {
        public static BattleGameObjectSpawner Instance;

        // [SerializeField] 
        // private PlayerViewModel viewModelPrefab;
        //
        // public PlayerViewModel ViewModelPrefab => viewModelPrefab;
        
        
        private void Awake()
        {
            Instance = this;
        }
    }
}