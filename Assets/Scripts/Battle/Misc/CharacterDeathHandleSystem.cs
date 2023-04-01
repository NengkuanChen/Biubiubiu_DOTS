using System;
using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.CharacterSpawn;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

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
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, OwningPlayer>().Build()); ;
    }
    
    public void OnDestroy(ref SystemState state)
    { }
    
    public void OnUpdate(ref SystemState state)
    {
        var commandBuffer = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
            .CreateCommandBuffer(state.WorldUnmanaged);
        EntityQuery spawnPointsQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint, LocalToWorld>().Build();
        NativeArray<LocalToWorld> spawnPointLtWs = spawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        EntityQuery characterPositionsQuery = SystemAPI.QueryBuilder().WithAll<Health, OwningPlayer, LocalToWorld>().Build();
        NativeArray<LocalToWorld> characterLtWs = characterPositionsQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        
        DestroyOnDeathJob job = new DestroyOnDeathJob
        {
            CommandBuffer = commandBuffer,
            RespawnTime = SystemAPI.GetSingleton<BattleConfigSingleton>().RespawnTime,
        };
        job.Schedule(state.Dependency).Complete();
        
        new RespawnRequestJob
        {
            Time = state.WorldUnmanaged.Time,
            CommandBuffer = commandBuffer,
            SpawnPositions = spawnPointLtWs,
            CharacterLtWs = characterLtWs,
        }.Schedule(state.Dependency).Complete();
        
        spawnPointLtWs.Dispose();
        characterLtWs.Dispose();
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
    public partial struct RespawnRequestJob : IJobEntity
    {
        
        public EntityCommandBuffer CommandBuffer;
        public NativeArray<LocalToWorld> SpawnPositions;
        public NativeArray<LocalToWorld> CharacterLtWs;
        public TimeData Time;



        [BurstCompile]
        void Execute(Entity entity, ref CharacterWaitingRespawnComponent waitingRespawn, in FirstPersonPlayer player, in GhostOwner ghostOwnerComponent)
        {
            waitingRespawn.RespawnTime -= Time.DeltaTime;
            if (waitingRespawn.RespawnTime <= 0)
            {
                CommandBuffer.RemoveComponent<CharacterWaitingRespawnComponent>(entity);
                var spawnRequestEntity = CommandBuffer.CreateEntity();
                CommandBuffer.AddComponent(spawnRequestEntity, new CharacterSpawnRequest
                {
                    ForPlayer = entity,
                    ForConnectionId = ghostOwnerComponent.NetworkId,
                    SpawnPosition = GetSpawnPositionByDistance()
                });
            }
        }

        [BurstCompile]
        private float3 GetSpawnPositionByDistance()
        {
            var tempDistance = 0f;
            var finalSpawnPosition = SpawnPositions[0].Position;
            foreach (var spawnPosition in SpawnPositions)
            {
                var spawnDistance = float.MaxValue;
                foreach (var characterLtW in CharacterLtWs)
                {
                    var distance = math.distance(characterLtW.Position, spawnPosition.Position);
                    if (distance < spawnDistance)
                    {
                        spawnDistance = distance;
                    }
                }
                if (spawnDistance > tempDistance)
                {
                    tempDistance = spawnDistance;
                    finalSpawnPosition = spawnPosition.Position;
                }
            }
            
            return finalSpawnPosition;
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

