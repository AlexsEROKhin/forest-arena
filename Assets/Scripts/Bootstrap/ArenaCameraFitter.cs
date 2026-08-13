using UnityEngine;

namespace LocalPvp.Bootstrap
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFitter : MonoBehaviour
    {
        private float arenaWidth;
        private float arenaHeight;
        private float margin;
        private Camera targetCamera;

        public void Configure(float width, float height, float framingMargin)
        {
            arenaWidth = width;
            arenaHeight = height;
            margin = Mathf.Max(0f, framingMargin);
            FitArena();
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void OnPreCull()
        {
            FitArena();
        }

        private void FitArena()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            var aspect = Mathf.Max(targetCamera.aspect, 0.01f);
            var halfHeight = arenaHeight * 0.5f;
            var halfWidthInVerticalUnits = arenaWidth * 0.5f / aspect;
            targetCamera.orthographicSize = Mathf.Max(halfHeight, halfWidthInVerticalUnits) + margin;
        }
    }
}
