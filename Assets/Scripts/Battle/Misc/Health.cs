using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[Serializable]
[GhostComponent()]
public struct Health : IComponentData
{
    public float MaxHealth;
    [GhostField()]
    public float CurrentHealth;

    public bool IsDead()
    {
        return CurrentHealth <= 0f;
    }
}

[Serializable]
[GhostComponent]
public struct HealthRecoveryComponent : IComponentData
{
    public float RecoveryRate;
    public float RecoveryDelay;
    public float RecoveryTimer;
}