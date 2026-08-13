using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float jumpImpulse = 11.8f;
        [SerializeField, Min(1)] private int maxJumps = 2;
        [SerializeField, Min(0f)] private float groundAcceleration = 45f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.65f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.08f;
        [SerializeField, Min(0f)] private float dodgeSpeed = 10f;
        [SerializeField, Min(0f)] private float dodgeDuration = 0.36f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.8f;
        [SerializeField, Min(0.2f)] private float dodgeColliderHeight = 1.2f;
        [SerializeField] private PlayerControls controls;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private BoxCollider2D boxCollider;
        private Vector2 standingColliderSize;
        private Vector2 standingColliderOffset;
        private bool dodgeColliderActive;
        private float horizontalInput;
        private bool jumpRequested;
        private bool jumpReleased;
        private bool dodgeRequested;
        private float dodgeEndsAt;
        private float dodgeStartedAt = float.NegativeInfinity;
        private float nextDodgeAt;
        private int facingDirection = 1;
        private float lastGroundedAt = float.NegativeInfinity;
        private float jumpRequestedAt = float.NegativeInfinity;
        private float movementLockedUntil;
        private bool dodgeVisualActive;
        private float baseMoveSpeed;
        private int jumpsUsed;
        private float lastJumpStartedAt = float.NegativeInfinity;
        private bool isGrounded;
        private CharacterStats stats;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        private readonly RaycastHit2D[] standUpHits = new RaycastHit2D[4];
        private bool externalInputEnabled;
        private bool networkPresentationMode;
        private float externalHorizontal;
        private bool externalJumpPressed;
        private bool externalJumpReleased;
        private bool externalDodgePressed;

        public int FacingDirection => facingDirection;
        public bool IsDodging => Time.time < dodgeEndsAt;
        public bool IsDodgePose => IsDodging || dodgeColliderActive;
        public float DodgeStartedAt => dodgeStartedAt;
        public bool IsInvulnerable => IsDodging;
        public bool IsGrounded => isGrounded;
        public int JumpsUsed => jumpsUsed;
        public float LastJumpStartedAt => lastJumpStartedAt;
        public float MoveDirection => horizontalInput;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsNetworkReplica => networkPresentationMode;
        public float DodgeReadyAmount => dodgeCooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01((nextDodgeAt - Time.time) / dodgeCooldown);

        public void Configure(PlayerControls newControls, float newMoveSpeed = 5f, float newJumpImpulse = 11.8f)
        {
            controls = newControls;
            moveSpeed = Mathf.Max(0f, newMoveSpeed);
            jumpImpulse = Mathf.Max(0f, newJumpImpulse);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            boxCollider = bodyCollider as BoxCollider2D;
            if (boxCollider != null)
            {
                standingColliderSize = boxCollider.size;
                standingColliderOffset = boxCollider.offset;
            }
            stats = GetComponent<CharacterStats>();
            baseMoveSpeed = moveSpeed;
        }

        private void Update()
        {
            if (networkPresentationMode) return;

            horizontalInput = externalInputEnabled ? externalHorizontal : controls.ReadHorizontal();
            jumpRequested |= externalInputEnabled ? externalJumpPressed : controls.JumpPressed();
            jumpReleased |= externalInputEnabled ? externalJumpReleased : controls.JumpReleased();
            externalJumpPressed = false;
            externalJumpReleased = false;
            if (jumpRequested)
            {
                jumpRequestedAt = Time.time;
            }
            dodgeRequested |= externalInputEnabled ? externalDodgePressed : controls.DodgePressed();
            externalDodgePressed = false;
            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                facingDirection = horizontalInput > 0f ? 1 : -1;
            }
        }

        private void FixedUpdate()
        {
            if (networkPresentationMode) return;

            isGrounded = CheckGrounded();
            if (isGrounded)
            {
                lastGroundedAt = Time.time;
                if (body.linearVelocity.y <= 0.05f)
                {
                    jumpsUsed = 0;
                }
            }

            if (dodgeRequested && Time.time >= nextDodgeAt)
            {
                dodgeStartedAt = Time.time;
                dodgeEndsAt = Time.time + dodgeDuration;
                nextDodgeAt = Time.time + dodgeCooldown;
                SetDodgeCollider(true);
            }

            if (IsDodging)
            {
                SetDodgeVisual(true);
                body.linearVelocity = new Vector2(facingDirection * dodgeSpeed, body.linearVelocity.y);
                jumpRequested = false;
                dodgeRequested = false;
                return;
            }

            if (dodgeColliderActive) TryRestoreStandingCollider();
            SetDodgeVisual(dodgeColliderActive);

            if (Time.time >= movementLockedUntil)
            {
                var acceleration = groundAcceleration * (isGrounded ? 1f : airControl);
                var horizontalVelocity = Mathf.MoveTowards(
                    body.linearVelocity.x,
                    horizontalInput * (stats != null ? stats.MoveSpeed : moveSpeed),
                    acceleration * Time.fixedDeltaTime);
                body.linearVelocity = new Vector2(horizontalVelocity, body.linearVelocity.y);
            }

            var hasBufferedJump = Time.time - jumpRequestedAt <= jumpBufferTime;
            var canUseCoyoteTime = Time.time - lastGroundedAt <= coyoteTime;
            var canAirJump = !canUseCoyoteTime && jumpsUsed < maxJumps;
            if (hasBufferedJump && (canUseCoyoteTime || canAirJump))
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
                body.AddForce(Vector2.up * jumpImpulse * body.mass, ForceMode2D.Impulse);
                jumpsUsed = canUseCoyoteTime ? 1 : jumpsUsed + 1;
                lastJumpStartedAt = Time.time;
                jumpRequestedAt = float.NegativeInfinity;
                lastGroundedAt = float.NegativeInfinity;
            }

            if (jumpReleased && body.linearVelocity.y > 0f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * 0.5f);
            }

            jumpRequested = false;
            jumpReleased = false;
            dodgeRequested = false;
        }

        private bool CheckGrounded()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;

            var hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
            for (var i = 0; i < hitCount; i++)
            {
                if (groundHits[i].collider != null && groundHits[i].normal.y > 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            horizontalInput = 0f;
            jumpRequested = false;
            jumpReleased = false;
            dodgeRequested = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            RestoreStandingColliderImmediately();
        }

        public void MultiplyMoveSpeed(float multiplier)
        {
            if (stats != null) stats.MultiplyMoveSpeed(multiplier);
            else moveSpeed *= Mathf.Max(0f, multiplier);
        }

        public void ResetUpgrades()
        {
            if (stats == null) moveSpeed = baseMoveSpeed;
        }

        public void ApplyKnockback(Vector2 force)
        {
            if (IsDodging) return;
            movementLockedUntil = Time.time + 0.12f;
            body.AddForce(force, ForceMode2D.Impulse);
        }

        public void ApplyAerialBounce()
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpImpulse * 0.55f);
            jumpsUsed = Mathf.Min(jumpsUsed, maxJumps - 1);
        }

        public void ApplyAttackLunge(int direction, float speed, float duration)
        {
            if (IsDodging) return;
            movementLockedUntil = Time.time + Mathf.Max(0f, duration);
            body.linearVelocity = new Vector2(direction * Mathf.Max(0f, speed), body.linearVelocity.y);
        }

        public void PrepareDashAttack(float windupDuration)
        {
            dodgeEndsAt = 0f;
            dodgeStartedAt = float.NegativeInfinity;
            dodgeRequested = false;
            movementLockedUntil = Time.time + Mathf.Max(0f, windupDuration);
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            TryRestoreStandingCollider();
            SetDodgeVisual(false);
            RestoreStandingColliderImmediately();
        }

        private void SetDodgeCollider(bool active)
        {
            if (boxCollider == null || dodgeColliderActive == active) return;
            if (!active)
            {
                TryRestoreStandingCollider();
                return;
            }

            var bottom = standingColliderOffset.y - standingColliderSize.y * 0.5f;
            var crouchedHeight = Mathf.Min(standingColliderSize.y, dodgeColliderHeight);
            boxCollider.size = new Vector2(standingColliderSize.x, crouchedHeight);
            boxCollider.offset = new Vector2(standingColliderOffset.x, bottom + crouchedHeight * 0.5f);
            dodgeColliderActive = true;
        }

        private bool TryRestoreStandingCollider()
        {
            if (boxCollider == null || !dodgeColliderActive) return true;

            var extraHeight = standingColliderSize.y - boxCollider.size.y;
            if (extraHeight > 0.001f)
            {
                var filter = new ContactFilter2D();
                filter.SetLayerMask(Physics2D.AllLayers);
                filter.useTriggers = false;
                if (boxCollider.Cast(Vector2.up, filter, standUpHits, extraHeight) > 0)
                    return false;
            }

            RestoreStandingColliderImmediately();
            return true;
        }

        private void RestoreStandingColliderImmediately()
        {
            if (boxCollider == null || !dodgeColliderActive) return;
            boxCollider.size = standingColliderSize;
            boxCollider.offset = standingColliderOffset;
            dodgeColliderActive = false;
        }

        public void ResetMotion(Vector2 position)
        {
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = !networkPresentationMode;
            enabled = true;
            movementLockedUntil = 0f;
            dodgeEndsAt = 0f;
            jumpsUsed = 0;
            lastJumpStartedAt = float.NegativeInfinity;
            SetDodgeVisual(false);
        }

        private void SetDodgeVisual(bool active)
        {
            if (dodgeVisualActive == active) return;
            dodgeVisualActive = active;
            transform.localScale = Vector3.one;
        }

        public void SetExternalInputMode(bool enabled)
        {
            externalInputEnabled = enabled;
            externalHorizontal = 0f;
            externalJumpPressed = false;
            externalJumpReleased = false;
            externalDodgePressed = false;
        }

        public void SubmitExternalInput(
            float horizontal,
            bool jumpPressed,
            bool jumpReleased,
            bool dodgePressed)
        {
            if (!externalInputEnabled || networkPresentationMode) return;
            externalHorizontal = Mathf.Clamp(horizontal, -1f, 1f);
            externalJumpPressed |= jumpPressed;
            externalJumpReleased |= jumpReleased;
            externalDodgePressed |= dodgePressed;
        }

        public void SetNetworkPresentationMode(bool enabled)
        {
            networkPresentationMode = enabled;
            SetExternalInputMode(false);
            if (body == null) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = !enabled;
        }

        public void ApplyNetworkPresentation(
            Vector2 velocity,
            int direction,
            float moveDirection,
            bool grounded,
            bool dodging,
            float dodgeElapsed,
            int syncedJumpsUsed,
            float jumpElapsed)
        {
            if (!networkPresentationMode) return;
            horizontalInput = Mathf.Clamp(moveDirection, -1f, 1f);
            facingDirection = direction < 0 ? -1 : 1;
            isGrounded = grounded;
            jumpsUsed = Mathf.Max(0, syncedJumpsUsed);
            dodgeStartedAt = dodging ? Time.time - Mathf.Max(0f, dodgeElapsed) : float.NegativeInfinity;
            dodgeEndsAt = dodging ? Time.time + 0.12f : 0f;
            lastJumpStartedAt = jumpElapsed >= 0f
                ? Time.time - jumpElapsed
                : float.NegativeInfinity;
            if (body != null) body.linearVelocity = velocity;
        }
    }
}
