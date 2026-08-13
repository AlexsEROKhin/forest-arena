using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class TransientEffectSprite : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector2 velocity;
        private float gravity;
        private float lifetime;
        private float bornAt;
        private float angularSpeed;
        private Color initialColor;

        public void Configure(Vector2 startVelocity, float gravityStrength, float duration, float rotationSpeed)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            velocity = startVelocity;
            gravity = gravityStrength;
            lifetime = Mathf.Max(0.05f, duration);
            angularSpeed = rotationSpeed;
            bornAt = Time.time;
            initialColor = spriteRenderer.color;
        }

        private void Update()
        {
            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, angularSpeed * Time.deltaTime);
            var amount = Mathf.Clamp01((Time.time - bornAt) / lifetime);
            spriteRenderer.color = new Color(initialColor.r, initialColor.g, initialColor.b, initialColor.a * (1f - amount));
            if (amount >= 1f) Destroy(gameObject);
        }
    }
}
