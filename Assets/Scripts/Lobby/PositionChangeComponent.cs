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
    
    public struct PlayerChangePositionConfirmation : IRpcCommand
    {
        public int targetTeamID;
        public int targetPositionID;
        public int oPositionID;
        public int oTeamID;
        public FixedString32Bytes playerName;
        public int PlayerID;
    }
    
    
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerChangePositionSystemClient : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithAll<PlayerChangePositionConfirmation>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (req, e) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequestComponent>>().WithAll<PlayerChangePositionConfirmation>().WithEntityAccess())
            {
                var positionChangeRequest = state.EntityManager.GetComponentData<PlayerChangePositionConfirmation>(e);
                var success = LobbyForm.Singleton<LobbyForm>().OnPlayerPositionChange(
                    positionChangeRequest.PlayerID,
                    positionChangeRequest.targetTeamID,
                    positionChangeRequest.targetPositionID,
                    positionChangeRequest.oTeamID,
                    positionChangeRequest.oPositionID,
                    positionChangeRequest.playerName.ToString());
                commandBuffer.DestroyEntity(e);
            }
            commandBuffer.Playback(state.EntityManager);
        }
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
                var oPos = -1;
                var oTeam = -1;
                var nickName = "";
                foreach (var (identity, identityEntity) in SystemAPI.Query<PlayerIdentity>().WithEntityAccess())
                {
                    if (identity.InGameID == idComponent.Value)
                    {
                        LobbyForm.Singleton<LobbyForm>().GetPlayerInfo(identity.InGameID, out oTeam, out oPos);
                        nickName = identity.PlayerNickname.ToString();
                    }

                    var success = LobbyForm.Singleton<LobbyForm>().OnPlayerPositionChange(
                        idComponent.Value,
                        positionChangeRequest.targetTeamID,
                        positionChangeRequest.targetPositionID,
                        oTeam,
                        oPos,
                        nickName);


                    if (success)
                    {
                        
                        var confirmation = commandBuffer.CreateEntity();
                        commandBuffer.SetComponent(identityEntity, new PlayerIdentity()
                        {
                            LobbyPositionID = positionChangeRequest.targetPositionID,
                            TeamId = positionChangeRequest.targetTeamID,
                            PlayerNickname = identity.PlayerNickname,
                            InGameID = identity.InGameID,
                            IsReady = false
                        });
                        commandBuffer.AddComponent<PlayerChangePositionConfirmation>(confirmation,
                            new PlayerChangePositionConfirmation()
                            {
                                targetTeamID = positionChangeRequest.targetTeamID,
                                targetPositionID = positionChangeRequest.targetPositionID,
                                oPositionID = oPos,
                                oTeamID = oTeam,
                                playerName = nickName,
                                PlayerID = idComponent.Value
                            });
                        commandBuffer.AddComponent(confirmation,
                            new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                        Debug.Log($"PlayerChangePositionConfirmation sent to {nickName}");
                    }
                }

                // state.EntityManager.DestroyEntity(reqEntity);
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}