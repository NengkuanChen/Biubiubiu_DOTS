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
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerChangePositionSystem: ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReceiveRpcCommandRequestComponent>()
                .WithNone<NetworkStreamInLobby>()
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
                var positionChangeRequest = state.EntityManager.GetComponentData<PositionChangeRequestComponent>(reqEntity);
                foreach (var identity in SystemAPI.Query<PlayerIdentity>())
                {
                //     if (identity.InGameID == req.ValueRO)
                //     {
                //         
                //     }
                // }
                // var success = LobbyForm.Singleton<LobbyForm>().OnPlayerPositionChange(
                //     positionChangeRequest.targetTeamID, 
                //     positionChangeRequest.targetPositionID,
                //     
                //
                // if (success)
                // {
                //     var confirmation = commandBuffer.CreateEntity();
                //     commandBuffer.AddComponent<PlayerChangePositionConfirmation>(confirmation, new PlayerChangePositionConfirmation()
                //     {
                //         targetTeamID = positionChangeRequest.targetTeamID,
                //         targetPositionID = positionChangeRequest.targetPositionID
                //     });
                //     commandBuffer.AddComponent(confirmation, new SendRpcCommandRequestComponent { TargetConnection = Entity.Null });
                }
                Debug.Log("Position change request");
                // state.EntityManager.DestroyEntity(reqEntity);
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}