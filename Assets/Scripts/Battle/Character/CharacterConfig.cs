using UnityEngine;

namespace Battle.Character
{
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "CharacterConfig", order = 0)]
    public class CharacterConfig : ScriptableObject
    {
        [SerializeField]
        private float characterWalkAcceleration = 0.5f;
        public float CharacterWalkAcceleration => characterWalkAcceleration;
        
        [SerializeField]
        private float characterWalkSpeed = 2.0f;
        public float CharacterWalkSpeed => characterWalkSpeed;
        
        [SerializeField]
        private float characterSprintAcceleration = 0.5f;
        public float CharacterSprintAcceleration => characterSprintAcceleration;
        
        [SerializeField]
        private float characterSprintSpeed = 4.0f;
        public float CharacterSprintSpeed => characterSprintSpeed;
        
        [SerializeField]
        private float characterJumpSpeed = 4.0f;
        public float CharacterJumpSpeed => characterJumpSpeed;
        
        
    }
}