using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.CharacterSpawn;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

// [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
// [UpdateInGroup(typeof(SimulationSystemGroup))]
// [BurstCompile]
// public partial struct DestroyOnDeathSystem : ISystem
// {
//
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent>().Build());
//     }
//
//     [BurstCompile]
//     public void OnDestroy(ref SystemState state)
//     { }
//
//     [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         DestroyOnDeathJob job = new DestroyOnDeathJob
//         {
//             ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
//         };
//         job.Schedule();
//     }
//
//     [BurstCompile]
//     [WithAll(typeof(Simulate))]
//     public partial struct DestroyOnDeathJob : IJobEntity
//     {
//         public EntityCommandBuffer ECB;
//         
//         void Execute(Entity entity, in Health health)
//         {
//             if (health.IsDead())
//             {
//                 ECB.DestroyEntity(entity);
//             }
//         }
//     }
// }


[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct CharacterDeathHandleSystemServer : ISystem
{
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, OwningPlayer>().Build());
    }
    
    public void OnDestroy(ref SystemState state)
    { }
    
    public void OnUpdate(ref SystemState state)
    {
        var commandBuffer = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
            .CreateCommandBuffer(state.WorldUnmanaged);
        DestroyOnDeathJob job = new DestroyOnDeathJob
        {
            CommandBuffer = commandBuffer,
            RespawnTime = SystemAPI.GetSingleton<BattleConfigSingleton>().RespawnTime,
        };
        job.Schedule(state.Dependency).Complete();
        
        new RespawnCountdownJob
        {
            DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
            CommandBuffer = commandBuffer,
        }.Schedule(state.Dependency).Complete();
    }
    
    [BurstCompile]
    public partial struct DestroyOnDeathJob : IJobEntity
    {
        public EntityCommandBuffer CommandBuffer;
        public float RespawnTime;
        
        [BurstCompile]
        void Execute(Entity entity, in Health health, in OwningPlayer owningPlayer)
        {
            if (health.IsDead())
            {
                CommandBuffer.AddComponent(owningPlayer.Entity, new CharacterWaitingRespawnComponent
                {
                    RespawnTime = RespawnTime
                });
                CommandBuffer.DestroyEntity(entity);
            }
        }
    }
    
    [BurstCompile]
    public partial struct RespawnCountdownJob : IJobEntity
    {
        
        public float DeltaTime;
        public EntityCommandBuffer CommandBuffer;
        
        [BurstCompile]
        void Execute(Entity entity, ref CharacterWaitingRespawnComponent waitingRespawn, in FirstPersonPlayer player, in GhostOwnerComponent ghostOwnerComponent)
        {
            waitingRespawn.RespawnTime -= DeltaTime;
            if (waitingRespawn.RespawnTime <= 0)
            {
                CommandBuffer.RemoveComponent<CharacterWaitingRespawnComponent>(entity);
                var spawnRequestEntity = CommandBuffer.CreateEntity();
                CommandBuffer.AddComponent(spawnRequestEntity, new CharacterSpawnRequest
                {
                    ForPlayer = entity,
                    ForConnectionId = ghostOwnerComponent.NetworkId,
                    SpawnPosition = new float3(0, 0, 0),
                });
            }
        }
    }
}

public struct CharacterWaitingRespawnComponent : IComponentData
{
    public float RespawnTime;
}

public struct PlayerDeathRPC : IRpcCommand
{
    
}

