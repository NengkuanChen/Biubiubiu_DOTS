using System.Collections.Generic;
using Battle;
using Battle.CharacterSpawn;
using Battle.ViewModel;
using Battle.Weapon;
using Lobby;
using Player;
using UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Game.Battle
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct BattleStartSystemServer: ISystem
    {
        private ComponentLookup<NetworkIdComponent> networkIdFromEntity;
        

        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAny<ServerReadyToStartTag, ClientReadyToStartTag>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkIdComponent>(true);
            state.RequireForUpdate<BattleEntitySpawner>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state);
            var isServerReady = false;
            var isClientReady = false;
            var serverReadyEntity = Entity.Null;
            var clientReadyEntity = Entity.Null;
            foreach (var (serverReady, e) in SystemAPI.Query<ServerReadyToStartTag>().WithEntityAccess())
            {
                isServerReady = true;
                serverReadyEntity = e;
            }

            foreach (var (clientReady, e) in SystemAPI.Query<ClientReadyToStartTag>().WithEntityAccess())
            {
                isClientReady = true;
                clientReadyEntity = e;
            }

            if (isClientReady && isServerReady)
            {
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<StartGameCommand>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                commandBuffer.DestroyEntity(serverReadyEntity);
                commandBuffer.DestroyEntity(clientReadyEntity);
                var gameEntitySpawner = SystemAPI.GetSingleton<BattleEntitySpawner>();
                GameStart(commandBuffer, ref state, gameEntitySpawner);
            }
            
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
        
        private void GameStart(EntityCommandBuffer commandBuffer, ref SystemState state,BattleEntitySpawner battleEntitySpawner)
        {
            UIManager.Singleton.CloseForm<LoadingForm>();
            Debug.Log("Server: GameStart");
            
            NativeArray<float3> testSpawnPositions = new NativeArray<float3>(new float3[]
            {
                new float3(0, 0, 0),
                new float3(1, 0, 1),
                new float3(-1, 0, -1),
                new float3(2, 0, 2),
            }, Allocator.Temp);
            var spawnPositionIndex = 0;

            //Spawn Player For Each PlayerIdentity
            foreach (var (playerIdentity, playerIdentityEntity) in SystemAPI.Query<PlayerIdentity>().WithEntityAccess())
            {
                var playerEntity = commandBuffer.Instantiate(battleEntitySpawner.PlayerGhost);
                commandBuffer.SetComponent(playerEntity, new FirstPersonPlayer
                {
                    Name = playerIdentity.PlayerNickname,
                    
                });
                var networkId = networkIdFromEntity[playerIdentity.SourceConnection];
                commandBuffer.SetComponent(playerEntity, new GhostOwnerComponent
                {
                    NetworkId = networkId.Value
                });


                var spawnCharacterRequest = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(spawnCharacterRequest, new CharacterSpawnRequest
                {
                    // ForConnection = playerIdentity.SourceConnection,
                    ForConnectionId = networkId.Value,
                    SpawnPosition = testSpawnPositions[spawnPositionIndex++],
                    ForPlayer = playerEntity,
                    PlayerIdentity = playerIdentityEntity
                });

            }
            
            testSpawnPositions.Dispose();
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct BattleStartSystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<StartGameCommand>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            // state.RequireForUpdate<FirstPersonCharacterComponent>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, entity) in SystemAPI.Query<RefRO<StartGameCommand>>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                GameStart(ref commandBuffer, ref state);
                commandBuffer.DestroyEntity(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }

        private void GameStart(ref EntityCommandBuffer commandBuffer, ref SystemState state)
        {
            UIManager.Singleton.CloseForm<LoadingForm>();
            commandBuffer.AddComponent(SystemAPI.GetSingletonEntity<NetworkIdComponent>(), new NetworkStreamInGame());
            Cursor.lockState = CursorLockMode.Locked;
            
        }
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct WeaponLayerSetUpSystemClient : ISystem
    {
        private BufferLookup<Child> childLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponLayerSetupRequest>();
            childLookup = state.GetBufferLookup<Child>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int localNetworkId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            childLookup.Update(ref state);
            foreach (var (weaponLayerSetupRequest, ghostOwner, entity) in SystemAPI.Query<WeaponLayerSetupRequest, RefRO<GhostOwnerComponent>>().WithEntityAccess())
            {
                if (localNetworkId == ghostOwner.ValueRO.NetworkId)
                {
                    MiscUtilities.SetLayerInHierarchy(state.EntityManager, commandBuffer, entity, childLookup,
                        weaponLayerSetupRequest.Layer);
                }
                else
                {
                    MiscUtilities.SetLayerInHierarchy(state.EntityManager, commandBuffer, entity, childLookup,
                        0);
                }
                commandBuffer.RemoveComponent<WeaponLayerSetupRequest>(entity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct CameraSetUpSystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<FirstPersonCharacterComponent>()
                .WithAll<OwningPlayer>()
                .WithAll<GhostOwnerComponent>()
                .WithNone<CharacterInBattleTag>();
            state.RequireForUpdate<BattleEntitySpawner>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            int localNetworkId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (character, owningPlayer, ghostOwner, entity) in SystemAPI.Query<FirstPersonCharacterComponent, OwningPlayer, GhostOwnerComponent>().WithNone<CharacterInBattleTag>().WithEntityAccess())
            {
                if (ghostOwner.NetworkId == localNetworkId)
                {
                    commandBuffer.AddComponent(character.ViewEntity, new MainEntityCamera()
                    {
                        BaseFoV = character.BaseFoV,
                        CurrentFoV = character.BaseFoV,
                    });
                    
                    commandBuffer.AddComponent(character.ViewEntity, new ViewModelCamera()
                    {
                        BaseFoV = character.BaseFoV,
                        CurrentFoV = character.BaseFoV,
                    });
                    
                    //Do CrossHair Here
                    //.....
                    BattleForm.Open<BattleForm>();
                    
                    commandBuffer.AddComponent(entity, new CharacterInBattleTag());
                    //Disable CharacterRenderer
                    MiscUtilities.SetShadowModeInHierarchy(state.EntityManager, commandBuffer, entity, SystemAPI.GetBufferLookup<Child>(), UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
                    
                    // //Spawn ViewModel;
                    // if (ViewModelManager.Instance != null)
                    // {
                    //     ViewModelManager.Instance.InstantiateViewModel();
                    // }
                    
                    
                }
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }


    public struct BattleStartTag : IComponentData
    {
        
    }
    
    public struct CharacterInBattleTag : IComponentData
    {
        
    }
    
    public struct JoinedClient : IComponentData
    {
        public Entity PlayerEntity;
    }
}