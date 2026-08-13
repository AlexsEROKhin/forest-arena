using UnityEngine;

namespace LocalPvp.Bootstrap
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelPortal : MonoBehaviour
    {
        [SerializeField] private Vector2 destination;
        [SerializeField, Min(0f)] private float reentryDelay = 0.6f;

        private static float nextAllowedTeleportTime;

        public void Configure(Vector2 targetPosition)
        {
            destination = targetPosition;
            var portalCollider = GetComponent<Collider2D>();
            portalCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Time.time < nextAllowedTeleportTime) return;

            var body = other.attachedRigidbody;
            if (body == null || body.GetComponent<Player.PlayerController>() == null) return;

            nextAllowedTeleportTime = Time.time + reentryDelay;
            body.position = destination;
            body.linearVelocity = Vector2.zero;
            var follow = Camera.main != null ? Camera.main.GetComponent<VerticalCameraFollow>() : null;
            if (follow != null) follow.SnapTo(destination);
        }
    }
}
