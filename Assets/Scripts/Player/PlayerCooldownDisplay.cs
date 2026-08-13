using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerCombat))]
    public sealed class PlayerCooldownDisplay : MonoBehaviour
    {
        private void Awake()
        {
            RemoveLegacyBar("Attack Cooldown Background");
            RemoveLegacyBar("Attack Cooldown Fill");
            RemoveLegacyBar("Dodge Cooldown Background");
            RemoveLegacyBar("Dodge Cooldown Fill");
            enabled = false;
        }

        private void RemoveLegacyBar(string objectName)
        {
            var child = transform.Find(objectName);
            if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
