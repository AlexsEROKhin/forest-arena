using UnityEngine;

namespace LocalPvp.Bootstrap
{
    [RequireComponent(typeof(Camera))]
    public sealed class VerticalCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0.18f;
        [SerializeField] private float minimumY = -29f;
        [SerializeField] private float maximumY = 29f;
        [SerializeField] private float minimumX;
        [SerializeField] private float maximumX;

        private Vector3 velocity;

        public void Configure(Transform followTarget, float minY, float maxY)
        {
            target = followTarget;
            minimumY = Mathf.Min(minY, maxY);
            maximumY = Mathf.Max(minY, maxY);
        }

        public void Configure(Transform followTarget, Vector2 minimum, Vector2 maximum)
        {
            target = followTarget;
            SetBounds(minimum, maximum, false);
        }

        public void SetBounds(Vector2 minimum, Vector2 maximum, bool snapToTarget)
        {
            minimumX = Mathf.Min(minimum.x, maximum.x);
            maximumX = Mathf.Max(minimum.x, maximum.x);
            minimumY = Mathf.Min(minimum.y, maximum.y);
            maximumY = Mathf.Max(minimum.y, maximum.y);
            if (snapToTarget && target != null) SnapTo(target.position);
        }

        public void SnapTo(Vector2 worldPosition)
        {
            velocity = Vector3.zero;
            transform.position = new Vector3(
                Mathf.Clamp(worldPosition.x, minimumX, maximumX),
                Mathf.Clamp(worldPosition.y, minimumY, maximumY),
                transform.position.z);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = new Vector3(
                Mathf.Clamp(target.position.x, minimumX, maximumX),
                Mathf.Clamp(target.position.y, minimumY, maximumY),
                transform.position.z);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref velocity,
                smoothTime);
        }
    }
}
