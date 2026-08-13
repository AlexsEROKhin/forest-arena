using UnityEngine;

namespace LocalPvp.Player
{
    public enum UpgradeType
    {
        Damage,
        MaxHealth,
        MoveSpeed
    }

    public sealed class UpgradePickup : MonoBehaviour
    {
        [SerializeField] private UpgradeType type;

        public void Configure(UpgradeType newType) => type = newType;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<UpgradeController>(out var upgrades)) return;
            upgrades.Apply(type);

            Destroy(gameObject);
        }
    }
}
