using LocalPvp.Player;
using UnityEngine;

namespace LocalPvp.Bootstrap
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CameraZone2D : MonoBehaviour
    {
        [SerializeField] private Vector2 cameraMinimum;
        [SerializeField] private Vector2 cameraMaximum;

        public void Configure(Vector2 size, Vector2 minimum, Vector2 maximum)
        {
            var zone = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = size;
            cameraMinimum = minimum;
            cameraMaximum = maximum;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.attachedRigidbody == null
                || other.attachedRigidbody.GetComponent<PlayerController>() == null)
                return;

            var follow = Camera.main != null ? Camera.main.GetComponent<VerticalCameraFollow>() : null;
            if (follow != null) follow.SetBounds(cameraMinimum, cameraMaximum, false);
        }
    }
}
