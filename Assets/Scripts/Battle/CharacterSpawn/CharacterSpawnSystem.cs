using Battle.Misc;
using Battle.Weapon;
using Game.Battle;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Battle.CharacterSpawn
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct CharacterSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleEntitySpawner>();
            state.RequireForUpdate<CharacterSpawnRequest>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRO<CharacterSpawnRequest>>().WithEntityAccess())
            {
                var entitySpawner = SystemAPI.GetSingleton<BattleEntitySpawner>();
                var characterGhost = commandBuffer.Instantiate(entitySpawner.TestCharacterGhost);
                var connectionId = SystemAPI.GetComponent<NetworkIdComponent>(spawnRequest.ValueRO.ForConnection).Value;
                commandBuffer.SetComponent(characterGhost,
                    LocalTransform.FromPosition(spawnRequest.ValueRO.SpawnPosition));
                commandBuffer.SetComponent(characterGhost, new OwningPlayer{ Entity = spawnRequest.ValueRO.ForPlayer});
                
                commandBuffer.AddComponent(characterGhost, new CharacterCleanupServer
                {
                    OwningConnectionEntity = spawnRequest.ValueRO.ForConnection,
                });
                
                //Add DamageRecorder
                commandBuffer.AddBuffer<CharacterDamageSourceRecordBuffer>(characterGhost);
                
                //assign character to player
                FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(spawnRequest.ValueRO.ForPlayer);
                player.ControlledCharacter = characterGhost;
                commandBuffer.SetComponent(spawnRequest.ValueRO.ForPlayer, player);
                commandBuffer.DestroyEntity(entity);
                
                //Spawn weapon
                var weaponEntity = commandBuffer.Instantiate(entitySpawner.TestGunGhost);


                //
                
                commandBuffer.AddComponent(characterGhost, new ActiveWeaponComponent
                {
                    WeaponEntity = weaponEntity
                });
                

                commandBuffer.SetComponent(weaponEntity, new GhostOwnerComponent
                {
                    NetworkId = connectionId
                });
                commandBuffer.SetComponent(characterGhost, new GhostOwnerComponent
                {
                    NetworkId = connectionId
                });
            }
            
            commandBuffer.Playback(state.EntityManager);
        }
    }

    public struct CharacterSpawnRequest : IComponentData
    {
        public Entity ForConnection;
        public Entity ForPlayer;
        public float3 SpawnPosition;
        public Entity PlayerIdentity;
    }
}