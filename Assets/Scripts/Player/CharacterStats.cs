using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class CharacterStats : MonoBehaviour
    {
        [SerializeField, Min(1)] private int baseMaxHealth = 100;
        [SerializeField, Min(1)] private int baseDamage = 25;
        [SerializeField, Min(0f)] private float baseMoveSpeed = 5f;

        private int bonusMaxHealth;
        private float damageMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;

        public int MaxHealth => baseMaxHealth + bonusMaxHealth;
        public int Damage => Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        public float MoveSpeed => baseMoveSpeed * moveSpeedMultiplier;

        public void AddMaxHealth(int amount) => bonusMaxHealth += Mathf.Max(0, amount);
        public void MultiplyDamage(float multiplier) => damageMultiplier *= Mathf.Max(0f, multiplier);
        public void MultiplyMoveSpeed(float multiplier) => moveSpeedMultiplier *= Mathf.Max(0f, multiplier);

        public void ResetUpgrades()
        {
            bonusMaxHealth = 0;
            damageMultiplier = 1f;
            moveSpeedMultiplier = 1f;
        }
    }
}
