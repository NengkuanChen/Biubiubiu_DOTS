using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class BattleForm : UIForm
    {
        [SerializeField]
        private TextMeshProUGUI ammoAmountText;

        [SerializeField]
        private Image healthBar;
        
        [SerializeField]
        private TextMeshProUGUI healthAmountText;
        
        
        
        public void UpdatePlayerAmmoAmount(int ammoAmount, int maxAmmoAmount, bool isReloading)
        {
            ammoAmountText.text = $"{ammoAmount}/{maxAmmoAmount}";
        }
        
        
        public void UpdatePlayerHealth(float health, float maxHealth)
        {
            healthBar.fillAmount = health/ maxHealth;
            healthAmountText.text = $"{health}/{maxHealth}";
        }
    }
}