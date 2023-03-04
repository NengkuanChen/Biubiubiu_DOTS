using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Rival;
using Unity.Physics;
using System.Collections.Generic;
using Battle.Character;
using Battle.Weapon;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class FirstPersonCharacterAuthoring : MonoBehaviour
{
    public GameObject ViewObject;
    public GameObject NameTagSocket;
    public GameObject WeaponSocket;
    public GameObject WeaponAnimationSocket;
    public GameObject DeathVFX;
    public GameObject DeathVFXSpawnPoint;
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public FirstPersonCharacterComponent Character = FirstPersonCharacterComponent.GetDefault();
    public List<GameObject> CharacterHitBoxes = new List<GameObject>();

    public class Baker : Baker<FirstPersonCharacterAuthoring>
    {
        public override void Bake(FirstPersonCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            authoring.Character.ViewEntity = GetEntity(authoring.ViewObject);
            authoring.Character.NameTagSocketEntity = GetEntity(authoring.NameTagSocket);
            authoring.Character.WeaponSocketEntity = GetEntity(authoring.WeaponSocket);
            authoring.Character.WeaponAnimationSocketEntity = GetEntity(authoring.WeaponAnimationSocket);
            authoring.Character.DeathVFX = GetEntity(authoring.DeathVFX);
            authoring.Character.DeathVFXSpawnPoint = GetEntity(authoring.DeathVFXSpawnPoint);
        
            AddComponent(authoring.Character);
            AddComponent(new FirstPersonCharacterControl());
            AddComponent(new OwningPlayer());
            
            AddComponent(new ActiveWeaponComponent());
            AddBuffer<CharacterHitBoxEntityBuffer>();
            for (int i = 0; i < authoring.CharacterHitBoxes.Count; i++)
            {
                AppendToBuffer(new CharacterHitBoxEntityBuffer { HitBoxEntity = GetEntity(authoring.CharacterHitBoxes[i]) });
            }
            
        }
    }
}