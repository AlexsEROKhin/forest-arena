using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class FadingAfterimage : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float bornAt;
        private Color initialColor;

        public void Configure(float duration)
        {
            lifetime = Mathf.Max(0.05f, duration);
            bornAt = Time.time;
            spriteRenderer = GetComponent<SpriteRenderer>();
            initialColor = spriteRenderer.color;
        }

        private void Update()
        {
            var amount = Mathf.Clamp01((Time.time - bornAt) / lifetime);
            spriteRenderer.color = new Color(
                initialColor.r,
                initialColor.g,
                initialColor.b,
                initialColor.a * (1f - amount));
            transform.position += Vector3.down * (0.18f * Time.deltaTime);
            if (amount >= 1f) Destroy(gameObject);
        }
    }
}
