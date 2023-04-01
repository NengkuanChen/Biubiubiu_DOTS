using Battle.Weapon;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace UI
{
    [UpdateInGroup(typeof(UIUpdateSystemGroup))]
    public partial struct BattleUISystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponComponent>();
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var localNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;
            UpdatePlayerAmmoAmount(ref state, localNetworkId);
            UpdatePlayerHealthBar(ref state, localNetworkId);
            UpdatePingShow(ref state);
        }
        
        
        private void UpdatePingShow(ref SystemState state)
        {
            var framerateForm = BattleForm.Singleton<FramerateForm>();
            EntityQuery networkAckQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkSnapshotAck>().Build(state.EntityManager);
            if (networkAckQuery.HasSingleton<NetworkSnapshotAck>())
            {
                NetworkSnapshotAck networkAck = networkAckQuery.GetSingleton<NetworkSnapshotAck>();
                framerateForm.UpdatePing((int)networkAck.EstimatedRTT);
            }
        }
        
        private void UpdatePlayerHealthBar(ref SystemState state, int localNetworkId)
        {
            var battleForm = BattleForm.Singleton<BattleForm>();
            foreach (var (healthComponent, ghostOwner) in 
                     SystemAPI.Query <RefRO<Health>, RefRO<GhostOwner>>())
            {
                if (ghostOwner.ValueRO.NetworkId == localNetworkId)
                {
                    var health = math.ceil(healthComponent.ValueRO.CurrentHealth);
                    var maxHealth = healthComponent.ValueRO.MaxHealth;
                    battleForm.UpdatePlayerHealth(health, maxHealth);
                }
            }
        }
        
        private void UpdatePlayerAmmoAmount(ref SystemState state, int localNetworkId)
        {
            var battleForm = BattleForm.Singleton<BattleForm>();
            foreach (var (reloadComponent, weaponMagazineComponent, ghostOwner) in 
                     SystemAPI.Query <RefRO<WeaponReloadComponent>, RefRO<WeaponMagazineComponent>, RefRO<GhostOwner>>())
            {
                if (ghostOwner.ValueRO.NetworkId == localNetworkId)
                {
                    var ammoAmount = weaponMagazineComponent.ValueRO.MagazineRestBullet;
                    var maxAmmoAmount = weaponMagazineComponent.ValueRO.MagazineSize;
                    bool isReloading = reloadComponent.ValueRO.IsReloading;
                    battleForm.UpdatePlayerAmmoAmount(ammoAmount, maxAmmoAmount, isReloading);
                }
            }
        }
    }
}