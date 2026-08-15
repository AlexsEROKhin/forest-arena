using System.Collections;
using System.Collections.Generic;
using LocalPvp.Items;
using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 25;
        [SerializeField, Min(0f)] private float attackRange = 1.1f;
        [SerializeField, Min(0f)] private float attackWindup = 0.15f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.55f;
        [SerializeField, Min(0f)] private float kickCooldown = 0.7f;
        [SerializeField, Min(0f)] private float kickWindup = 0.225f;
        [SerializeField, Min(0f)] private float kickRecovery = 0.225f;
        [SerializeField, Min(0f)] private float knockbackForce = 5.5f;
        [SerializeField, Min(0f)] private float lungeSpeed = 12f;
        [SerializeField, Min(0f)] private float lungeDuration = 0.28f;
        [SerializeField] private PlayerControls controls;
        [SerializeField] private AttackDefinition basicAttack = null;
        [SerializeField] private AttackDefinition airAttack = null;
        [SerializeField] private AttackDefinition dashAttack = null;
        [SerializeField] private AttackDefinition kickAttack = null;

        private PlayerController controller;
        private float nextAttackAt;
        private float nextKickAt;
        private bool attacking;
        private bool kicking;
        private int baseDamage;
        private GameObject attackMarker;
        private CharacterStats stats;
        private float attackStartedAt = float.NegativeInfinity;
        private float kickBufferedUntil = float.NegativeInfinity;
        private const float KickInputBuffer = 0.65f;
        private const float DashPunchImpactDelay = 0.30f;
        private const float DashPunchRecovery = 0.30f;
        private int attackSequenceId;
        private bool externalInputEnabled;
        private bool networkPresentationMode;
        private bool externalAttackPressed;
        private bool externalKickPressed;
        private bool externalDodgeHeld;
        private float networkPreviewUntil = float.NegativeInfinity;


        public void Configure(PlayerControls newControls) => controls = newControls;
        public void MultiplyDamage(float multiplier)
        {
            if (stats != null) stats.MultiplyDamage(multiplier);
            else damage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
        }
        public float AttackReadyAmount => attackCooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01((nextAttackAt - Time.time) / attackCooldown);
        public float KickReadyAmount => kickCooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01((nextKickAt - Time.time) / kickCooldown);
        public bool IsAttacking => attacking;
        public bool IsKicking => kicking;
        public float AttackStartedAt => attackStartedAt;
        public AttackType CurrentAttackType { get; private set; } = AttackType.Basic;
        public float AttackElapsed => attacking ? Mathf.Max(0f, Time.time - attackStartedAt) : 0f;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<CharacterStats>();
            baseDamage = damage;
            EnsureAttackDefinitions();
        }

        private void OnEnable()
        {
            EnsureAttackDefinitions();
            if (controls.kick == KeyCode.None)
                controls.kick = gameObject.name.StartsWith("Player 1") ? KeyCode.G : KeyCode.RightAlt;
        }

        private void EnsureAttackDefinitions()
        {
            kickWindup = 0.225f;
            kickRecovery = 0.225f;
            if (basicAttack == null || basicAttack.type != AttackType.Basic)
                basicAttack = AttackDefinition.Basic();
            if (airAttack == null || airAttack.type != AttackType.Air)
                airAttack = AttackDefinition.Air();
            if (dashAttack == null || dashAttack.type != AttackType.Dash)
                dashAttack = AttackDefinition.Dash();
            if (kickAttack == null || kickAttack.type != AttackType.Kick)
                kickAttack = AttackDefinition.Kick();
        }

        public void ResetUpgrades()
        {
            if (stats == null) damage = baseDamage;
        }

        private void Update()
        {
            EnsureAttackDefinitions();
            if (networkPresentationMode) return;

            var kickPressed = externalInputEnabled
                ? externalKickPressed
                : controls.KickPressed() || (gameObject.name.StartsWith("Player 1")
                    ? Input.GetKeyDown(KeyCode.G)
                    : Input.GetKeyDown(KeyCode.RightAlt));
            var attackPressed = externalInputEnabled ? externalAttackPressed : controls.AttackPressed();
            var dodgeHeld = externalInputEnabled ? externalDodgeHeld : controls.DodgeHeld();
            externalKickPressed = false;
            externalAttackPressed = false;

            if (kickPressed)
                kickBufferedUntil = Time.time + KickInputBuffer;

            if (attacking) return;

            if (Time.time <= kickBufferedUntil && Time.time >= nextKickAt)
            {
                kickBufferedUntil = float.NegativeInfinity;
                StartCoroutine(AttackRoutine(false, kickAttack));
                return;
            }

            if (!attackPressed || Time.time < nextAttackAt) return;

            var dashAttackRequested = dodgeHeld && Mathf.Abs(controller.MoveDirection) > 0.1f;
            if (!controller.IsDodging || dashAttackRequested)
            {
                StartCoroutine(AttackRoutine(dashAttackRequested, null));
            }
        }

        private IEnumerator AttackRoutine(bool dashAttackRequested, AttackDefinition forcedAttack)
        {
            var attackId = ++attackSequenceId;
            attacking = true;
            attackStartedAt = Time.time;
            var isKick = forcedAttack != null && forcedAttack.type == AttackType.Kick;
            var aerialAttack = !isKick && !controller.IsGrounded;
            var lungeAttack = !aerialAttack && dashAttackRequested;
            var attack = forcedAttack ?? (aerialAttack ? airAttack : lungeAttack ? dashAttack : basicAttack);
            var impactDelay = isKick
                ? kickWindup
                : lungeAttack
                    ? DashPunchImpactDelay
                    : attackWindup;
            CurrentAttackType = attack.type;
            kicking = isKick;
            // Hold the character in the anticipation pose until the visible fist
            // reaches its impact frame. This keeps movement, hitbox and art synced.
            if (lungeAttack) controller.PrepareDashAttack(impactDelay);
            if (isKick) nextKickAt = Time.time + kickCooldown;
            else nextAttackAt = Time.time + attackCooldown + attack.extraCooldown;
            var attackSize = new Vector2(attackRange * attack.rangeMultiplier, attack.height);
            var marker = CreateAttackMarker(transform.position, attackSize, attack);
            attackMarker = marker;
            yield return new WaitForSeconds(impactDelay);

            // The fighter can keep moving during windup. Calculate the hitbox
            // from the current body position at the actual impact frame rather
            // than leaving it behind where the attack began.
            var attackDirection = controller.FacingDirection;
            var center = (Vector2)transform.position
                + Vector2.right * attackDirection * attackRange * attack.forwardOffset
                + Vector2.up * attack.verticalOffset;
            marker.transform.position = center;

            var bounceHit = false;
            if (!controller.IsDodging || lungeAttack)
            {
                if (lungeAttack) controller.ApplyAttackLunge(attackDirection, lungeSpeed, lungeDuration);
                var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f);
                var hitPlayers = new HashSet<PlayerHealth>();
                var hitObjects = new HashSet<IAttackReceiver>();
                foreach (var hit in hits)
                {
                    if (hit.transform.root == transform.root) continue;
                    var health = hit.GetComponentInParent<PlayerHealth>();
                    if (health != null && hitPlayers.Add(health))
                    {
                        var currentDamage = stats != null ? stats.Damage : damage;
                        var dealtDamage = Mathf.RoundToInt(currentDamage * attack.damageMultiplier);
                        var knockback = new Vector2(
                            attackDirection * knockbackForce * attack.horizontalKnockback,
                            knockbackForce * attack.verticalKnockback);
                        var hitSucceeded = health.TakeDamage(dealtDamage, knockback);
                        bounceHit |= hitSucceeded && attack.aerialBounce;
                        if (hitSucceeded)
                        {
                            CombatFeedback.Instance.PlayHit(hit.transform.position, attack.strongFeedback);
                        }
                        continue;
                    }

                    var receiverComponent = hit.GetComponentInParent(typeof(IAttackReceiver));
                    var receiver = receiverComponent as IAttackReceiver;
                    if (receiver != null && hitObjects.Add(receiver)
                        && receiver.ReceiveAttack(attackId, attack.type, this))
                    {
                        CombatFeedback.Instance.PlayHit(hit.transform.position, false);
                    }
                }
            }

            if (bounceHit) controller.ApplyAerialBounce();

            yield return new WaitForSeconds(isKick ? kickRecovery : lungeAttack ? DashPunchRecovery : 0.08f);
            Destroy(marker);
            attackMarker = null;
            kicking = false;
            attacking = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (attackMarker != null) Destroy(attackMarker);
            attackMarker = null;
            kicking = false;
            attacking = false;
            kickBufferedUntil = float.NegativeInfinity;
        }

        private GameObject CreateAttackMarker(Vector2 center, Vector2 size, AttackDefinition attack)
        {
            // Keep a lightweight lifetime marker for coroutine cleanup, but do not
            // render the rectangular physics hitbox in the finished game.
            var marker = new GameObject("Attack Hitbox (Hidden)");
            marker.transform.position = center;
            marker.transform.localScale = new Vector3(size.x, size.y, 1f);
            return marker;
        }

        public void SetExternalInputMode(bool enabled)
        {
            externalInputEnabled = enabled;
            externalAttackPressed = false;
            externalKickPressed = false;
            externalDodgeHeld = false;
        }

        public void SubmitExternalInput(bool attackPressed, bool kickPressed, bool dodgeHeld)
        {
            if (!externalInputEnabled || networkPresentationMode) return;
            externalAttackPressed |= attackPressed;
            externalKickPressed |= kickPressed;
            externalDodgeHeld = dodgeHeld;
        }

        public void SetNetworkPresentationMode(bool enabled)
        {
            networkPresentationMode = enabled;
            SetExternalInputMode(false);
            if (!enabled) return;
            StopAllCoroutines();
            if (attackMarker != null) Destroy(attackMarker);
            attackMarker = null;
            attacking = false;
            kicking = false;
        }

        public void ApplyNetworkPresentation(
            bool syncedAttacking,
            bool syncedKicking,
            AttackType attackType,
            float elapsed)
        {
            if (!networkPresentationMode) return;
            if (!syncedAttacking && Time.time < networkPreviewUntil) return;
            attacking = syncedAttacking;
            kicking = syncedKicking;
            CurrentAttackType = attackType;
            attackStartedAt = syncedAttacking
                ? Time.time - Mathf.Max(0f, elapsed)
                : float.NegativeInfinity;
            if (syncedAttacking) networkPreviewUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Starts the local attack pose immediately on an online client. The
        /// host still performs the real hit detection and damage calculation.
        /// </summary>
        public void PreviewNetworkAttack(bool kick, bool dashAttack)
        {
            if (!networkPresentationMode || attacking) return;

            attacking = true;
            kicking = kick;
            CurrentAttackType = kick
                ? AttackType.Kick
                : !controller.IsGrounded
                    ? AttackType.Air
                    : dashAttack
                        ? AttackType.Dash
                        : AttackType.Basic;
            attackStartedAt = Time.time;
            networkPreviewUntil = Time.time + 0.22f;
        }
    }
}
