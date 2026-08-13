using System.Collections;
using LocalPvp.Bootstrap;
using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int fallbackMaxHealth = 100;
        private int currentHealth;
        private PlayerController controller;
        private bool isDead;
        private Transform healthFill;
        private Vector2 spawnPosition;
        private Color normalColor;
        [SerializeField, Range(0, 1)] private int playerIndex;
        private int baseMaxHealth;
        private float lastHitAt = float.NegativeInfinity;
        private CharacterStats stats;
        private bool useRandomPlatformForNextRespawn;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => stats != null ? stats.MaxHealth : fallbackMaxHealth;
        public float HealthRatio => MaxHealth > 0 ? (float)currentHealth / MaxHealth : 0f;
        public bool IsDead => isDead;
        public bool WasRecentlyHit => Time.time - lastHitAt < 0.18f;
        public int PlayerIndex => playerIndex;

        private void Awake()
        {
            stats = GetComponent<CharacterStats>();
            currentHealth = MaxHealth;
            baseMaxHealth = fallbackMaxHealth;
            controller = GetComponent<PlayerController>();
            spawnPosition = transform.position;
            normalColor = GetComponent<SpriteRenderer>().color;
            RemoveWorldHealthBar();
        }

        public void Configure(int index)
        {
            playerIndex = index;
            if (MatchManager.Instance != null)
                MatchManager.Instance.RegisterPlayer(index, this);
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.RegisterPlayer(playerIndex, this);
        }

        public bool TakeDamage(int amount, Vector2 knockback)
        {
            if (isDead || amount <= 0 || (controller != null && controller.IsInvulnerable))
            {
                return false;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            lastHitAt = Time.time;
            controller.ApplyKnockback(knockback);
            StartCoroutine(FlashOnHit());
            UpdateHealthBar();
            if (currentHealth == 0)
            {
                Die();
            }

            return true;
        }

        public void IncreaseMaxHealth(int amount)
        {
            if (stats != null) stats.AddMaxHealth(amount);
            else fallbackMaxHealth += Mathf.Max(0, amount);
            RefillHealth();
        }

        public void RefillHealth()
        {
            currentHealth = MaxHealth;
            UpdateHealthBar();
        }

        private void Die()
        {
            isDead = true;
            var body = GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            GetComponent<Collider2D>().enabled = false;
            GetComponent<PlayerController>().enabled = false;
            GetComponent<PlayerCombat>().enabled = false;
            var renderer = GetComponent<SpriteRenderer>();
            renderer.color = new Color(0.15f, 0.15f, 0.15f);
            SetVisualTint(new Color(0.15f, 0.15f, 0.15f));
            UpdateHealthBar();
            if (MatchManager.Instance != null) MatchManager.Instance.PlayerDied(playerIndex);
            else StartCoroutine(RespawnSoloPlayer());
        }

        public void KillFromFall()
        {
            if (isDead) return;
            currentHealth = 0;
            useRandomPlatformForNextRespawn = true;
            Die();
        }

        private IEnumerator RespawnSoloPlayer()
        {
            yield return new WaitForSeconds(0.8f);
            RespawnForRound();
        }

        private IEnumerator FlashOnHit()
        {
            var renderer = GetComponent<SpriteRenderer>();
            normalColor = renderer.color;
            renderer.color = Color.white;
            SetVisualTint(Color.white);
            yield return new WaitForSeconds(0.1f);
            if (!isDead)
            {
                renderer.color = normalColor;
                SetVisualTint(Color.white);
            }
        }

        public void PrepareForIntermission()
        {
            var body = GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            controller.enabled = false;
            GetComponent<PlayerCombat>().enabled = false;
        }

        public void RespawnForRound()
        {
            currentHealth = MaxHealth;
            isDead = false;
            var collider = GetComponent<Collider2D>();
            collider.enabled = true;
            var targetPosition = useRandomPlatformForNextRespawn
                ? ArenaFallRespawner.GetRandomPlatformSpawn(this, spawnPosition)
                : spawnPosition;
            useRandomPlatformForNextRespawn = false;
            controller.ResetMotion(targetPosition);
            GetComponent<PlayerCombat>().enabled = true;
            GetComponent<SpriteRenderer>().color = normalColor;
            SetVisualTint(Color.white);
            UpdateHealthBar();
        }

        private void SetVisualTint(Color color)
        {
            var knightVisual = GetComponent<KnightVisualPrototype>();
            if (knightVisual != null) knightVisual.SetTint(color);
        }

        public void ResetUpgrades()
        {
            if (stats == null) fallbackMaxHealth = baseMaxHealth;
            GetComponent<UpgradeController>().ResetUpgrades();
            controller.ResetUpgrades();
            GetComponent<PlayerCombat>().ResetUpgrades();
        }

        public void ApplyNetworkPresentation(int health, bool dead, bool recentlyHit)
        {
            currentHealth = Mathf.Clamp(health, 0, MaxHealth);
            isDead = dead;
            if (recentlyHit) lastHitAt = Time.time;

            var color = dead ? new Color(0.15f, 0.15f, 0.15f) : normalColor;
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = color;
            SetVisualTint(dead ? color : Color.white);
            UpdateHealthBar();
        }

        private void CreateHealthBar()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);

            var background = new GameObject("Health Background");
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(-0.6f, 0.8f, 0f);
            background.transform.localScale = new Vector3(1.2f, 0.12f, 1f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = sprite;
            backgroundRenderer.color = new Color(0.12f, 0.12f, 0.12f);
            backgroundRenderer.sortingOrder = 9;

            var fill = new GameObject("Health Fill");
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(-0.6f, 0.8f, 0f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = new Color(0.2f, 0.9f, 0.25f);
            fillRenderer.sortingOrder = 10;
            healthFill = fill.transform;
            UpdateHealthBar();
        }

        private void RemoveWorldHealthBar()
        {
            healthFill = null;
            RemoveChild("Health Background");
            RemoveChild("Health Fill");
        }

        private void RemoveChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        private void UpdateHealthBar()
        {
            if (healthFill == null) return;
            var ratio = MaxHealth > 0 ? (float)currentHealth / MaxHealth : 0f;
            healthFill.localScale = new Vector3(1.2f * ratio, 0.12f, 1f);
        }
    }
}
