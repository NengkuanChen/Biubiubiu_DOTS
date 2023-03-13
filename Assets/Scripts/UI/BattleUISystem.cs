﻿using Battle.Weapon;
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
            var localNetworkId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;
            UpdatePlayerAmmoAmount(ref state, localNetworkId);
            UpdatePlayerHealthBar(ref state, localNetworkId);
        }
        
        private void UpdatePlayerHealthBar(ref SystemState state, int localNetworkId)
        {
            var battleForm = BattleForm.Singleton<BattleForm>();
            foreach (var (healthComponent, ghostOwner) in 
                     SystemAPI.Query <RefRO<Health>, RefRO<GhostOwnerComponent>>())
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
                     SystemAPI.Query <RefRO<WeaponReloadComponent>, RefRO<WeaponMagazineComponent>, RefRO<GhostOwnerComponent>>())
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