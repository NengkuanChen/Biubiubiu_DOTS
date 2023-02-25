using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace Battle.Weapon
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [UpdateAfter(typeof(WeaponSystem))]
    [BurstCompile]
    public partial struct BulletSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Bullet>();
        }

        public void OnDestroy(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
            var bulletInitializeJob = new BulletPhysicsPropertyInitializeJob();
            state.Dependency = bulletInitializeJob.Schedule(state.Dependency);
            state.Dependency.Complete();
            // var bulletLifeTimeHandler = new BulletLifeTimeHandler
            // {
            //     commandBuffer = commandBuffer,
            //     DeltaTime = state.WorldUnmanaged.Time.DeltaTime,
            // };
            // state.Dependency = bulletLifeTimeHandler.Schedule(state.Dependency);
            // state.Dependency.Complete();
        }
        
        public partial struct BulletPhysicsPropertyInitializeJob : IJobEntity
        {
            void Execute(Entity entity, ref BulletInitialPhysicsPropertyComponent physicsProperty, 
                ref LocalTransform localTransform, ref PhysicsVelocity physicsVelocity)
            {
                if (!physicsProperty.HasInitialized)
                {
                    physicsVelocity.Linear = localTransform.Forward() * physicsProperty.InitialSpeed;
                    physicsProperty.HasInitialized = true;
                }
            }
        }
        
        public partial struct BulletLifeTimeHandler : IJobEntity
        {
            public EntityCommandBuffer commandBuffer;
            public float DeltaTime;

            void Execute(Entity entity, ref Bullet bullet)
            {
                bullet.LifeTime -= DeltaTime;
                if (bullet.LifeTime <= 0)
                {
                    commandBuffer.DestroyEntity(entity);
                }
            }
        }
    }
}