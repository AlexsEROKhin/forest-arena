using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(CharacterStats), typeof(PlayerHealth))]
    public sealed class UpgradeController : MonoBehaviour
    {
        private CharacterStats stats;
        private PlayerHealth health;

        private void Awake()
        {
            stats = GetComponent<CharacterStats>();
            health = GetComponent<PlayerHealth>();
        }

        public void Apply(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Damage:
                    stats.MultiplyDamage(1.25f);
                    break;
                case UpgradeType.MaxHealth:
                    stats.AddMaxHealth(25);
                    health.RefillHealth();
                    break;
                case UpgradeType.MoveSpeed:
                    stats.MultiplyMoveSpeed(1.15f);
                    break;
            }
        }

        public void ResetUpgrades() => stats.ResetUpgrades();
    }
}
