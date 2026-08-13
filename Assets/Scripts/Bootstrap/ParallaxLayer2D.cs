using UnityEngine;

namespace LocalPvp.Bootstrap
{
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [SerializeField] private float movementFactor;
        private Transform cameraTransform;
        private Vector3 initialCameraPosition;
        private Vector3 initialLayerPosition;

        public void Configure(float factor)
        {
            movementFactor = factor;
            CaptureStartPositions();
        }

        private void Awake() => CaptureStartPositions();

        private void CaptureStartPositions()
        {
            var targetCamera = Camera.main;
            if (targetCamera == null) return;
            cameraTransform = targetCamera.transform;
            initialCameraPosition = cameraTransform.position;
            initialLayerPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                CaptureStartPositions();
                if (cameraTransform == null) return;
            }

            var cameraDelta = cameraTransform.position - initialCameraPosition;
            transform.position = initialLayerPosition + new Vector3(
                cameraDelta.x * movementFactor,
                cameraDelta.y * movementFactor,
                0f);
        }
    }
}
