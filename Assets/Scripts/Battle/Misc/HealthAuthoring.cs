using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class HealthAuthoring : MonoBehaviour
{
    public float MaxHealth = 100f;
    public float HealthRecoveryRate = 10f;
    public float HealthRecoveryDelay = 8f;
    
    public class Baker : Baker<HealthAuthoring>
    {
        public override void Bake(HealthAuthoring authoring)
        {
            AddComponent(new Health
            {
                MaxHealth = authoring.MaxHealth,
                CurrentHealth = authoring.MaxHealth,
            });
            AddComponent(new HealthRecoveryComponent
            {
                RecoveryRate = authoring.HealthRecoveryRate,
                RecoveryDelay = authoring.HealthRecoveryDelay,
                RecoveryTimer = authoring.HealthRecoveryDelay,
            });
        }
    }
}
