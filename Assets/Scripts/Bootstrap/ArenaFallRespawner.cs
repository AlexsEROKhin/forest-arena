using LocalPvp.Player;
using UnityEngine;

namespace LocalPvp.Bootstrap
{
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class ArenaFallRespawner : MonoBehaviour
    {
        private const float KillY = -10.8f;
        private const float KillX = 18.7f;
        private PlayerHealth health;

        private void Awake() => health = GetComponent<PlayerHealth>();

        private void Update()
        {
            var controller = GetComponent<PlayerController>();
            if (health == null || health.IsDead || (controller != null && controller.IsNetworkReplica)) return;
            var position = transform.position;
            if (position.y < KillY || Mathf.Abs(position.x) > KillX)
                health.KillFromFall();
        }

        public static Vector2 GetRandomPlatformSpawn(PlayerHealth respawningPlayer, Vector2 fallback)
        {
            var platformRoot = GameObject.Find("Arena")?.transform.Find("Environment/Gameplay/Platforms");
            if (platformRoot == null) return fallback;
            var platforms = platformRoot.GetComponentsInChildren<BoxCollider2D>();
            if (platforms.Length == 0) return fallback;

            var playerCollider = respawningPlayer.GetComponent<BoxCollider2D>();
            var feetOffset = playerCollider != null
                ? playerCollider.size.y * 0.5f - playerCollider.offset.y
                : 0.5f;
            var opponent = FindOpponent(respawningPlayer);
            var best = fallback;

            for (var attempt = 0; attempt < 16; attempt++)
            {
                var platformCollider = platforms[Random.Range(0, platforms.Length)];
                if (platformCollider == null || !platformCollider.enabled) continue;
                var bounds = platformCollider.bounds;
                var margin = Mathf.Min(0.75f, bounds.extents.x * 0.35f);
                var x = Random.Range(bounds.min.x + margin, bounds.max.x - margin);
                var candidate = new Vector2(x, bounds.max.y + feetOffset + 0.04f);
                best = candidate;
                if (opponent == null || Vector2.Distance(candidate, opponent.position) >= 3f)
                    return candidate;
            }

            return best;
        }

        private static Transform FindOpponent(PlayerHealth player)
        {
            foreach (var other in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                if (other != player && !other.IsDead) return other.transform;
            return null;
        }
    }
}
