using System.Collections;
using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class CombatFeedback : MonoBehaviour
    {
        private const float NormalHitStop = 0.055f;
        private const float StrongHitStop = 0.08f;
        private Coroutine hitStopRoutine;
        private Coroutine shakeRoutine;
        private float timeScaleBeforeStop = 1f;

        public static CombatFeedback Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void PlayHit(Vector2 position, bool strong)
        {
            CreateHitParticles(position, strong);

            if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
            hitStopRoutine = StartCoroutine(HitStop(strong ? StrongHitStop : NormalHitStop));

            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(CameraShake(strong ? 0.18f : 0.11f, strong ? 0.18f : 0.1f));
        }

        private IEnumerator HitStop(float duration)
        {
            if (Time.timeScale > 0f) timeScaleBeforeStop = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = timeScaleBeforeStop;
            hitStopRoutine = null;
        }

        private IEnumerator CameraShake(float duration, float strength)
        {
            var camera = Camera.main;
            if (camera == null) yield break;

            var basePosition = new Vector3(0f, 0f, -10f);
            var remaining = duration;
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                var fade = Mathf.Clamp01(remaining / duration);
                var offset = Random.insideUnitCircle * strength * fade;
                camera.transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            camera.transform.position = basePosition;
            shakeRoutine = null;
        }

        private static void CreateHitParticles(Vector2 position, bool strong)
        {
            var count = strong ? 10 : 6;
            var color = strong ? new Color(1f, 0.35f, 0.05f) : new Color(1f, 0.9f, 0.3f);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            for (var i = 0; i < count; i++)
            {
                var particle = new GameObject("Hit Particle");
                particle.transform.position = position;
                particle.transform.localScale = Vector3.one * (strong ? 0.14f : 0.1f);
                var renderer = particle.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = 25;

                var body = particle.AddComponent<Rigidbody2D>();
                body.gravityScale = 0.5f;
                body.linearVelocity = Random.insideUnitCircle.normalized * Random.Range(3f, strong ? 7f : 5f);
                Object.Destroy(particle, 0.35f);
            }
        }

        private void OnDisable()
        {
            if (Time.timeScale == 0f) Time.timeScale = timeScaleBeforeStop;
            var camera = Camera.main;
            if (camera != null) camera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
