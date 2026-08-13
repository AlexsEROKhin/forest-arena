using UnityEngine;

namespace LocalPvp.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerCombat), typeof(PlayerHealth))]
    public sealed class KnightVisualPrototype : MonoBehaviour
    {
        private const float IdleBreathDuration = 2.4f;
        private const int IdleFrameCount = 6;
        private const float IdleFrameDuration = 0.18f;
        private const int WalkFrameCount = 8;
        private const float WalkFrameDuration = 0.1f;
        private const int GroundJumpFrameCount = 6;
        private const int AirJumpFrameCount = 5;
        private const float JumpFrameDuration = 0.09f;
        private const int AttackFrameCount = 6;
        private const int HeavyAttackFrameCount = 8;
        private const int AerialAttackFrameCount = 6;
        private const float AttackFrameDuration = 0.075f;
        private const float HeavyAttackFrameDuration = 0.075f;
        private const int DodgeFrameCount = 6;
        private const float DodgeFrameDuration = 0.06f;
        private const int KickFrameCount = 6;
        private const float KickFrameDuration = 0.075f;
        private const float KickWidthCorrection = 1f;
        private const float KickHeightCorrection = 1f;
        // Imported frames use a centered pivot and contain a small transparent
        // margin below the boots. Lower the artwork without moving the physics
        // body so the visible soles meet the platform's collider top.
        private const float VisualGroundOffset = -0.84f;
        private const float VisualWidthScale = 0.986f;
        private const float VisualHeightScale = 0.918f;
        private const float SpritePixelsPerUnit = 220f;
        private readonly Sprite[] idleFrames = new Sprite[IdleFrameCount];
        private readonly Sprite[] walkFrames = new Sprite[WalkFrameCount];
        private readonly Sprite[] groundJumpFrames = new Sprite[GroundJumpFrameCount];
        private readonly Sprite[] airJumpFrames = new Sprite[AirJumpFrameCount];
        private readonly Sprite[] attackFrames = new Sprite[AttackFrameCount];
        private readonly Sprite[] heavyAttackFrames = new Sprite[HeavyAttackFrameCount];
        private readonly Sprite[] aerialAttackFrames = new Sprite[AerialAttackFrameCount];
        private readonly Sprite[] dodgeFrames = new Sprite[DodgeFrameCount];
        private readonly Sprite[] kickFrames = new Sprite[KickFrameCount];

        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerHealth health;
        private Transform visualRoot;
        private SpriteRenderer characterRenderer;
        [SerializeField] private Color baseTint = Color.white;
        private Sprite idleFrame;
        private float idleAnimationTime;
        private float walkAnimationTime;
        private bool wasWalking;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();

            // Match the physics body to the visible knight while keeping its feet
            // on the same world-space ground line as the original 1x1 collider.
            var bodyCollider = GetComponent<BoxCollider2D>();
            if (bodyCollider != null)
            {
                bodyCollider.size = new Vector2(0.95f, 2.18f);
                bodyCollider.offset = new Vector2(0f, 0.59f);
            }

            var placeholder = GetComponent<SpriteRenderer>();
            if (placeholder != null) placeholder.enabled = false;

            visualRoot = transform.Find("Knight Sprite Visual");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("Knight Sprite Visual").transform;
                visualRoot.SetParent(transform, false);
            }
            visualRoot.localPosition = new Vector3(0f, VisualGroundOffset, 0f);

            characterRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (characterRenderer == null)
                characterRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            characterRenderer.sortingOrder = 4;
            LoadIdleFrames();
            LoadWalkFrames();
            LoadJumpFrames();
            LoadAttackFrames();
            LoadHeavyAttackFrames();
            LoadAerialAttackFrames();
            LoadDodgeFrames();
            LoadKickFrames();
            characterRenderer.sprite = idleFrame;
            ApplyVisualTransform(1f, 1f);
        }

        private void Update()
        {
            var kickVisualActive = combat.IsKicking;

            var isIdle = !health.IsDead
                && !health.WasRecentlyHit
                && !controller.IsDodgePose
                && !combat.IsAttacking
                && controller.IsGrounded
                && Mathf.Abs(controller.MoveDirection) < 0.1f;

            var isWalking = !health.IsDead
                && !health.WasRecentlyHit
                && !controller.IsDodgePose
                && !combat.IsAttacking
                && controller.IsGrounded
                && Mathf.Abs(controller.MoveDirection) >= 0.1f;

            var isAttacking = !health.IsDead
                && !health.WasRecentlyHit
                && combat.IsAttacking;

            var isJumping = !health.IsDead
                && !health.WasRecentlyHit
                && !controller.IsDodgePose
                && !isAttacking
                && !controller.IsGrounded;

            var isDodging = !health.IsDead
                && !health.WasRecentlyHit
                && !isAttacking
                && controller.IsDodgePose;

            if (isAttacking)
            {
                walkAnimationTime = 0f;
                if (kickVisualActive)
                {
                    if (kickFrames[0] == null) LoadKickFrames();
                    var kickFrame = Mathf.Min(
                        Mathf.FloorToInt(Mathf.Max(0f, Time.time - combat.AttackStartedAt) / KickFrameDuration),
                        KickFrameCount - 1);
                    characterRenderer.sprite = kickFrames[kickFrame] != null ? kickFrames[kickFrame] : idleFrame;
                }
                else
                {
                    var elapsed = Mathf.Max(0f, Time.time - combat.AttackStartedAt);
                    if (combat.CurrentAttackType == AttackType.Air)
                    {
                        var frame = Mathf.Min(Mathf.FloorToInt(elapsed / AttackFrameDuration), AerialAttackFrameCount - 1);
                        characterRenderer.sprite = aerialAttackFrames[frame] != null ? aerialAttackFrames[frame] : idleFrame;
                    }
                    else if (combat.CurrentAttackType == AttackType.Dash)
                    {
                        var frame = Mathf.Min(Mathf.FloorToInt(elapsed / HeavyAttackFrameDuration), HeavyAttackFrameCount - 1);
                        characterRenderer.sprite = heavyAttackFrames[frame] != null ? heavyAttackFrames[frame] : idleFrame;
                    }
                    else
                    {
                        var frame = Mathf.Min(Mathf.FloorToInt(elapsed / AttackFrameDuration), AttackFrameCount - 1);
                        characterRenderer.sprite = attackFrames[frame] != null ? attackFrames[frame] : idleFrame;
                    }
                }
            }
            else if (isDodging)
            {
                walkAnimationTime = 0f;
                var dodgeFrame = Mathf.Min(
                    Mathf.FloorToInt(Mathf.Max(0f, Time.time - controller.DodgeStartedAt) / DodgeFrameDuration),
                    DodgeFrameCount - 1);
                characterRenderer.sprite = dodgeFrames[dodgeFrame] != null ? dodgeFrames[dodgeFrame] : idleFrame;
            }
            else if (isJumping)
            {
                walkAnimationTime = 0f;
                var jumpFrame = float.IsNegativeInfinity(controller.LastJumpStartedAt)
                    ? GroundJumpFrameCount - 1
                    : Mathf.Min(
                        Mathf.FloorToInt(Mathf.Max(0f, Time.time - controller.LastJumpStartedAt) / JumpFrameDuration),
                        controller.JumpsUsed >= 2 ? AirJumpFrameCount - 1 : GroundJumpFrameCount - 1);
                var jumpFrames = controller.JumpsUsed >= 2 ? airJumpFrames : groundJumpFrames;
                characterRenderer.sprite = jumpFrames[jumpFrame] != null ? jumpFrames[jumpFrame] : idleFrame;
            }
            else if (isWalking)
            {
                if (!wasWalking) walkAnimationTime = 0f;
                var walkFrame = Mathf.FloorToInt(walkAnimationTime / WalkFrameDuration) % WalkFrameCount;
                characterRenderer.sprite = walkFrames[walkFrame] != null ? walkFrames[walkFrame] : idleFrame;
                walkAnimationTime += Time.deltaTime;
            }
            else
            {
                walkAnimationTime = 0f;
                if (isIdle)
                {
                    idleAnimationTime += Time.deltaTime;
                    var idleIndex = Mathf.FloorToInt(idleAnimationTime / IdleFrameDuration) % IdleFrameCount;
                    characterRenderer.sprite = idleFrames[idleIndex] != null ? idleFrames[idleIndex] : idleFrame;
                }
                else
                {
                    characterRenderer.sprite = idleFrame;
                    idleAnimationTime = 0f;
                }
            }

            wasWalking = isWalking;

            var breathAmount = isIdle
                ? Mathf.Sin(idleAnimationTime / IdleBreathDuration * Mathf.PI * 2f) * 0.004f
                : 0f;
            var kickWidth = kickVisualActive ? KickWidthCorrection : 1f;
            var kickHeight = kickVisualActive ? KickHeightCorrection : 1f;
            ApplyVisualTransform(
                kickWidth,
                (1f + breathAmount) * kickHeight);

            if (health.IsDead)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);
                visualRoot.localPosition = new Vector3(0f, -0.75f, 0f);
            }
            else if (health.WasRecentlyHit)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, 12f * controller.FacingDirection);
            }
        }

        public void SetTint(Color color)
        {
            if (characterRenderer != null) characterRenderer.color = baseTint * color;
        }

        private void ApplyVisualTransform(float widthMultiplier, float heightMultiplier)
        {
            if (visualRoot == null) return;
            var direction = controller != null ? controller.FacingDirection : 1;
            visualRoot.localScale = new Vector3(
                direction * VisualWidthScale * widthMultiplier,
                VisualHeightScale * heightMultiplier,
                1f);
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localPosition = new Vector3(0f, VisualGroundOffset, 0f);
        }

        public void Configure(Color tint)
        {
            // The new sprite already contains its final palette. Player identity is
            // communicated by HUD and spawn side without recoloring the armor.
            baseTint = Color.white;
            if (characterRenderer != null) characterRenderer.color = baseTint;
        }

        private void LoadIdleFrames()
        {
            for (var i = 0; i < IdleFrameCount; i++)
                idleFrames[i] = LoadFrame($"Characters/BlueKnight/Idle/idle_{i}", $"Blue Knight Idle {i + 1}");
            idleFrame = idleFrames[0];
        }

        private void LoadWalkFrames()
        {
            for (var i = 0; i < WalkFrameCount; i++)
                walkFrames[i] = LoadFrame($"Characters/BlueKnight/Walk/walk_{i}", $"Blue Knight Walk {i + 1}");
        }

        private void LoadJumpFrames()
        {
            for (var i = 0; i < GroundJumpFrameCount; i++)
                groundJumpFrames[i] = LoadFrame(
                    $"Characters/BlueKnight/Jump/Ground/jump_{i}",
                    $"Blue Knight Ground Jump {i + 1}");
            for (var i = 0; i < AirJumpFrameCount; i++)
                airJumpFrames[i] = LoadFrame(
                    $"Characters/BlueKnight/Jump/Air/jump_{i}",
                    $"Blue Knight Air Jump {i + 1}");
        }

        private void LoadAttackFrames()
        {
            for (var i = 0; i < AttackFrameCount; i++)
                attackFrames[i] = LoadFrame(
                    $"Characters/BlueKnight/Attack/Basic/attack_{i}",
                    $"Blue Knight Basic Attack {i + 1}");
        }

        private void LoadHeavyAttackFrames()
        {
            // Reuse the animation that already looks correct: four sliding dash
            // poses followed by the final four poses of the normal punch. The
            // first punch pose lands exactly on the dash attack's impact time.
            for (var i = 0; i < 4; i++)
                heavyAttackFrames[i] = LoadFrame(
                    $"Characters/BlueKnight/DashKneeV2/dash_{i}",
                    $"Blue Knight Dash Attack Slide {i + 1}");

            for (var i = 4; i < HeavyAttackFrameCount; i++)
                heavyAttackFrames[i] = attackFrames[i - 2];
        }

        private void LoadAerialAttackFrames()
        {
            for (var i = 0; i < AerialAttackFrameCount; i++)
                aerialAttackFrames[i] = LoadFrame($"Characters/BlueKnight/Attack/Aerial/aerial_{i}", $"Blue Knight Aerial Attack {i + 1}");
        }

        private void LoadDodgeFrames()
        {
            for (var i = 0; i < DodgeFrameCount; i++)
                dodgeFrames[i] = LoadFrame(
                    $"Characters/BlueKnight/DashKneeV2/dash_{i}",
                    $"Blue Knight Knee Dash V2 {i + 1}");
        }

        private void LoadKickFrames()
        {
            for (var i = 0; i < KickFrameCount; i++)
                kickFrames[i] = i == 0 || i == KickFrameCount - 1
                    ? idleFrame
                    : LoadFrame(
                        $"Characters/BlueKnight/Kick/kick_{i}",
                        $"Blue Knight Kick {i + 1}");
        }

        private Sprite LoadFrame(string resourcePath, string spriteName, bool forceBottomPivot = false)
        {
            if (!forceBottomPivot)
            {
                var importedSprite = Resources.Load<Sprite>(resourcePath);
                if (importedSprite != null) return importedSprite;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogError($"Character frame '{resourcePath}' was not found in Resources.", this);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0f),
                SpritePixelsPerUnit);
            sprite.name = spriteName;
            return sprite;
        }
    }
}
