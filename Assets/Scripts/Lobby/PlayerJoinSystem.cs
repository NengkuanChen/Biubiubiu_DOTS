using System.Collections.Generic;
using Player;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Lobby
{
    
    // [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    // public partial struct PlayerJoinSystem: ISystem
    // {
    //
    //     public void OnCreate(ref SystemState state)
    //     {
    //         state.RequireForUpdate<PlayerIdentitySpawner>();
    //         var builder = new EntityQueryBuilder(Allocator.Temp)
    //             .WithAll<NetworkIdComponent>()
    //             .WithNone<NetworkStreamInGame>()
    //             .WithAll<NetworkStreamConnection>();
    //         state.RequireForUpdate(state.GetEntityQuery(builder));
    //     }
    //
    //     public void OnDestroy(ref SystemState state)
    //     {
    //        
    //     }
    //
    //     public void OnUpdate(ref SystemState state)
    //     {
    //         var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
    //         
    //         foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkIdComponent>>().WithEntityAccess().WithNone<NetworkStreamInGame>())
    //         {
    //             commandBuffer.AddComponent<NetworkStreamInGame>(entity);
    //             var req = commandBuffer.CreateEntity();
    //             commandBuffer.AddComponent<PlayerJoinRequest>(req);
    //             commandBuffer.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = entity });
    //         }
    //         commandBuffer.Playback(state.EntityManager);
    //     }
    // }
    //
    //
    
    

    
}