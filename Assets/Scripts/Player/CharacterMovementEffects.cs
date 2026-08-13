using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(PlayerController), typeof(Rigidbody2D))]
    public sealed class CharacterMovementEffects : MonoBehaviour
    {
        [Header("Ground dirt effect")]
        [SerializeField, Min(0.03f)] private float stepInterval = 0.11f;
        [SerializeField, Min(0f)] private float minimumGroundSpeed = 1.2f;
        [SerializeField] private Color dirtColor = new Color(0.31f, 0.20f, 0.09f, 0.9f);

        [Header("Dodge trail")]
        [SerializeField, Min(0.02f)] private float afterimageInterval = 0.045f;
        [SerializeField, Range(0.05f, 1f)] private float afterimageStartAlpha = 0.32f;
        [SerializeField, Min(0.05f)] private float afterimageLifetime = 0.20f;

        private static Sprite particleSprite;
        private PlayerController controller;
        private Rigidbody2D body;
        private Transform visualRoot;
        private SpriteRenderer characterRenderer;
        private float nextStepAt;
        private float nextAfterimageAt;
        private bool wasDodging;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            body = GetComponent<Rigidbody2D>();
            visualRoot = transform.Find("Knight Sprite Visual");
            if (visualRoot != null) characterRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            var horizontalSpeed = Mathf.Abs(body.linearVelocity.x);
            var walking = controller.IsGrounded
                && !controller.IsDodgePose
                && horizontalSpeed >= minimumGroundSpeed
                && Mathf.Abs(controller.MoveDirection) > 0.1f;
            if (walking && Time.time >= nextStepAt)
            {
                EmitDirt(Mathf.InverseLerp(minimumGroundSpeed, 8f, horizontalSpeed));
                nextStepAt = Time.time + stepInterval;
            }

            var dodging = controller.IsDodgePose;
            if (dodging && !wasDodging)
            {
                EmitDashBurst();
                nextAfterimageAt = Time.time;
            }
            if (dodging && Time.time >= nextAfterimageAt)
            {
                SpawnAfterimage();
                nextAfterimageAt = Time.time + afterimageInterval;
            }
            wasDodging = dodging;
        }

        private void EmitDirt(float speedAmount)
        {
            var count = 2 + Mathf.RoundToInt(speedAmount * 3f);
            var origin = (Vector2)transform.position
                + Vector2.down * 0.53f
                - Vector2.right * controller.FacingDirection * 0.20f;
            for (var i = 0; i < count; i++)
            {
                var velocity = new Vector2(
                    -controller.FacingDirection * Random.Range(0.7f, 1.4f + speedAmount),
                    Random.Range(0.65f, 1.35f + speedAmount * 0.55f));
                var color = Color.Lerp(dirtColor, new Color(0.52f, 0.35f, 0.14f, 0.78f), Random.value);
                SpawnParticle(
                    "Ground Dirt",
                    origin + Random.insideUnitCircle * 0.05f,
                    velocity,
                    Random.Range(0.045f, 0.11f),
                    Random.Range(0.23f, 0.40f),
                    color,
                    7.8f,
                    2);
            }
        }

        private void EmitDashBurst()
        {
            var origin = (Vector2)transform.position + Vector2.down * 0.28f;
            for (var i = 0; i < 12; i++)
            {
                var length = Random.Range(0.12f, 0.34f);
                var velocity = new Vector2(
                    -controller.FacingDirection * Random.Range(2.2f, 4.8f),
                    Random.Range(-0.15f, 0.65f));
                SpawnParticle(
                    "Dodge Streak",
                    origin + new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0f, 1.0f)),
                    velocity,
                    length,
                    Random.Range(0.12f, 0.24f),
                    new Color(0.35f, 0.75f, 1f, Random.Range(0.35f, 0.70f)),
                    0f,
                    3,
                    new Vector2(3.2f, 0.32f));
            }
            EmitDirt(1f);
        }

        private static void SpawnParticle(
            string objectName,
            Vector2 position,
            Vector2 velocity,
            float size,
            float lifetime,
            Color color,
            float gravity,
            int sortingOrder,
            Vector2? shape = null)
        {
            var particle = new GameObject(objectName);
            particle.transform.position = position;
            var proportions = shape ?? Vector2.one;
            particle.transform.localScale = new Vector3(size * proportions.x, size * proportions.y, 1f);
            particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));
            var renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = GetParticleSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            particle.AddComponent<TransientEffectSprite>().Configure(velocity, gravity, lifetime, Random.Range(-110f, 110f));
        }

        private void SpawnAfterimage()
        {
            if (characterRenderer == null || characterRenderer.sprite == null || visualRoot == null) return;
            var ghost = new GameObject("Dodge Afterimage");
            ghost.transform.SetPositionAndRotation(visualRoot.position, visualRoot.rotation);
            ghost.transform.localScale = visualRoot.lossyScale;
            var renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = characterRenderer.sprite;
            renderer.flipX = characterRenderer.flipX;
            renderer.sortingLayerID = characterRenderer.sortingLayerID;
            renderer.sortingOrder = characterRenderer.sortingOrder - 1;
            var tint = characterRenderer.color;
            renderer.color = new Color(tint.r * 0.55f, tint.g * 0.85f, 1f, afterimageStartAlpha);
            ghost.AddComponent<FadingAfterimage>().Configure(afterimageLifetime);
        }

        private static Sprite GetParticleSprite()
        {
            if (particleSprite != null) return particleSprite;
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "Procedural Movement Effect Particle";
            texture.filterMode = FilterMode.Bilinear;
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(4.2f - distance)));
            }
            texture.Apply();
            particleSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            return particleSprite;
        }
    }
}
