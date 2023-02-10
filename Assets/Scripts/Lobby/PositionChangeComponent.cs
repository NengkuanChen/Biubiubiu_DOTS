using Player;
using UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace Lobby
{
    public struct PositionChangeRequestComponent: IRpcCommand
    {
        public int targetTeamID;
        public int targetPositionID;
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerChangePositionSystemServer: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<PositionChangeRequestComponent>();
            state.RequireForUpdate<PlayerIdentity>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<PositionChangeRequestComponent>().WithEntityAccess())
            {
                var idComponent = state.GetComponentLookup<NetworkIdComponent>(true)[req.ValueRO.SourceConnection];
                var positionChangeRequest = state.EntityManager.GetComponentData<PositionChangeRequestComponent>(reqEntity);
                
                foreach (var (identity, identityEntity) in SystemAPI.Query<RefRW<PlayerIdentity>>().WithEntityAccess())
                {
                    if (identity.ValueRW.InGameID == idComponent.Value)
                    {
                        var oPos = identity.ValueRW.LobbyPositionID;
                        var oTeam = identity.ValueRW.TeamId;
                        var nickName = identity.ValueRW.PlayerNickname;
                        // LobbyForm.Singleton<LobbyForm>().GetPlayerInfo(identity.InGameID, out oTeam, out oPos);
                        // nickName = identity.PlayerNickname.ToString();
                        var success = LobbyForm.Singleton<LobbyForm>().OnPlayerPositionChange(
                            idComponent.Value,
                            positionChangeRequest.targetTeamID,
                            positionChangeRequest.targetPositionID,
                            oTeam,
                            oPos,
                            nickName.ToString());
                        
                        if (success)
                        {
                        
                            var playerInfoUpdate = commandBuffer.CreateEntity();
                            
                            identity.ValueRW.LobbyPositionID = positionChangeRequest.targetPositionID;
                            identity.ValueRW.TeamId = positionChangeRequest.targetTeamID;
                            identity.ValueRW.IsReady = false;
                            
                            commandBuffer.AddComponent(playerInfoUpdate, new PlayerIdentityUpdate()
                            {
                                InGameID = idComponent.Value,
                                TeamID = positionChangeRequest.targetTeamID,
                                LobbyPositionID = positionChangeRequest.targetPositionID,
                                PlayerNickname = nickName,
                                IsReady = false
                            });
                            
                            commandBuffer.AddComponent(playerInfoUpdate,
                                new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                            
                            var clearRequest = commandBuffer.CreateEntity();
                            commandBuffer.AddComponent(clearRequest, new ClearOriginalButtonRequest()
                            {
                                PositionID = oPos,
                                TeamID = oTeam
                            });
                            commandBuffer.AddComponent(clearRequest,
                                new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                            Debug.Log($"PlayerChangePositionConfirmation sent to {nickName}");
                        }
                        
                    }
                }
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
    
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct ClearPositionClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<ClearOriginalButtonRequest>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<ClearOriginalButtonRequest>>().WithAll<ReceiveRpcCommandRequestComponent>().WithEntityAccess())
            {
                LobbyForm.Singleton<LobbyForm>().ClearButton(req.ValueRO.TeamID, req.ValueRO.PositionID);
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}