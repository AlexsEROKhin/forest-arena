using LocalPvp.Player;
using UnityEngine;

namespace LocalPvp.Bootstrap
{
    public sealed class LocalPvpBootstrap : MonoBehaviour
    {
        private const float ArenaWidth = 36f;
        private const float ArenaHeight = 20f;
        private const float LevelBottom = -10f;
        private const float LevelTop = 10f;
        private static Sprite debugSprite;
        private static Sprite platformShortSprite;
        private static Sprite platformMediumSprite;
        private static Sprite platformLongSprite;
        private static Sprite floorSprite;
        private static Sprite arenaBackgroundSprite;
        private static PhysicsMaterial2D frictionlessMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureArenaExists()
        {
            if (FindAnyObjectByType<LocalPvpBootstrap>() != null)
            {
                return;
            }

            new GameObject("Local PvP Bootstrap").AddComponent<LocalPvpBootstrap>();
        }

        private void Awake()
        {
            EnsureSceneContents();
        }

        public void EnsureSceneContents()
        {
            CreateCamera();
            RemoveLegacyMultiplayerObjects();
            var arena = GameObject.Find("Arena");
            var rebuiltArena = false;
            if (arena == null || GameObject.Find("Forest Canopy Arena v27") == null)
            {
                if (arena != null) DestroySceneObject(arena);
                CreateWalls();
                rebuiltArena = true;
            }
            if (rebuiltArena)
            {
                DestroyNamedObject("Player 1 (WASD)");
                DestroyNamedObject("Player 2 (Arrows)");
            }
            if (FindAnyObjectByType<CombatFeedback>() == null)
                new GameObject("Combat Feedback").AddComponent<CombatFeedback>();
            if (FindAnyObjectByType<ArenaHealthHud>() == null)
                new GameObject("Arena Health HUD").AddComponent<ArenaHealthHud>();
            RemoveWeaponCrates();
            if (rebuiltArena || GameObject.Find("Player 1 (WASD)") == null)
                CreatePlayer(
                    0,
                    "Player 1 (WASD)",
                    GetRoomSpawn(0),
                    new PlayerControls(KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.F, KeyCode.G, KeyCode.LeftShift));
            if (rebuiltArena || GameObject.Find("Player 2 (Arrows)") == null)
                CreatePlayer(
                    1,
                    "Player 2 (Arrows)",
                    GetRoomSpawn(1),
                    new PlayerControls(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.RightControl, KeyCode.RightAlt, KeyCode.RightShift));
            ConfigureExistingPlayer(
                "Player 1 (WASD)",
                new PlayerControls(KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.F, KeyCode.G, KeyCode.LeftShift));
            ConfigureExistingPlayer(
                "Player 2 (Arrows)",
                new PlayerControls(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.RightControl, KeyCode.RightAlt, KeyCode.RightShift));
            var activePlayer = GameObject.Find("Player 1 (WASD)");
            if (activePlayer != null && (rebuiltArena
                || activePlayer.transform.position.y < LevelBottom
                || activePlayer.transform.position.y > LevelTop))
                activePlayer.transform.position = GetRoomSpawn(0);
            ConfigureCameraFollow();
        }

        private static void RemoveLegacyMultiplayerObjects()
        {
            var matchManager = FindAnyObjectByType<MatchManager>();
            if (matchManager != null) DestroySceneObject(matchManager.gameObject);
        }

        private static void RemoveWeaponCrates()
        {
            // The wooden-weapon experiment is safely archived outside Assets. Remove
            // the scene-saved spawner before its first Start callback can run.
            DestroyNamedObject("Wooden Weapon Crate Spawner");
            DestroyNamedObject("Breakable Wooden Crate");
            DestroyNamedObject("Wooden Sword Pickup");
        }

        private static void DestroyNamedObject(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null) DestroySceneObject(target);
        }

        private static void ConfigureExistingPlayer(string objectName, PlayerControls controls)
        {
            var player = GameObject.Find(objectName);
            if (player == null) return;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.Configure(controls);
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.Configure(controls);
            var bodyCollider = player.GetComponent<Collider2D>();
            if (bodyCollider != null) bodyCollider.sharedMaterial = GetFrictionlessMaterial();
            if (player.GetComponent<ArenaFallRespawner>() == null)
                player.AddComponent<ArenaFallRespawner>();
            if (player.GetComponent<CharacterMovementEffects>() == null)
                player.AddComponent<CharacterMovementEffects>();
        }

        private static void DestroySceneObject(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static void CreateCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.orthographic = true;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.05f);

            camera.orthographicSize = 10.5f;
            var fitter = camera.GetComponent<ArenaCameraFitter>();
            if (fitter == null) fitter = camera.gameObject.AddComponent<ArenaCameraFitter>();
            fitter.enabled = true;
            fitter.Configure(ArenaWidth, ArenaHeight, 0.5f);
            var follow = camera.GetComponent<VerticalCameraFollow>();
            if (follow != null) follow.enabled = false;
        }

        private static void CreateWalls()
        {
            var root = new GameObject("Arena").transform;
            var versionMarker = new GameObject("Forest Canopy Arena v27");
            versionMarker.transform.SetParent(root);
            var environment = CreateGroup(root, "Environment");
            CreateForestEnvironment(environment);
            var gameplay = CreateGroup(environment, "Gameplay");
            CreateGroup(gameplay, "Ground");
            var geometry = CreateGroup(gameplay, "Platforms");
            CreateGroup(gameplay, "GameplayProps");

            // Two equal spawn decks with a recoverable central gap.
            CreateOneWayPlatform(geometry, "Main Deck Left", new Vector2(-8.5f, -8f), new Vector2(15f, 0.5f));
            CreateOneWayPlatform(geometry, "Main Deck Right", new Vector2(8.5f, -8f), new Vector2(15f, 0.5f));
            // Twelve total playable surfaces. Vertical gaps stay within a comfortable double-jump range.
            CreateOneWayPlatform(geometry, "Suspended Chain Platform", new Vector2(0f, -5.8f), new Vector2(4.5f, 0.4f));
            CreateOneWayPlatform(geometry, "Lower Stone Ledge Left", new Vector2(-10.5f, -4.9f), new Vector2(5.5f, 0.4f));
            CreateOneWayPlatform(geometry, "Lower Stone Ledge Right", new Vector2(10.5f, -4.9f), new Vector2(5.5f, 0.4f));
            CreateOneWayPlatform(geometry, "Oak Walkway Left", new Vector2(-5.3f, -2.1f), new Vector2(5f, 0.4f));
            CreateOneWayPlatform(geometry, "Oak Walkway Right", new Vector2(5.3f, -2.1f), new Vector2(5f, 0.4f));
            CreateOneWayPlatform(geometry, "Castle Ledge Left", new Vector2(-12.4f, 0.1f), new Vector2(3.8f, 0.4f));
            CreateOneWayPlatform(geometry, "Castle Ledge Right", new Vector2(12.4f, 0.1f), new Vector2(3.8f, 0.4f));
            CreateOneWayPlatform(geometry, "Banner Platform Left", new Vector2(-6.2f, 2.8f), new Vector2(4.5f, 0.4f));
            CreateOneWayPlatform(geometry, "Banner Platform Right", new Vector2(6.2f, 2.8f), new Vector2(4.5f, 0.4f));
            CreateOneWayPlatform(geometry, "Royal Top Bridge", new Vector2(0f, 5.5f), new Vector2(7.5f, 0.4f));
        }

        private static void CreateForestEnvironment(Transform environment)
        {
            var background = CreateGroup(environment, "Background");
            CreateForestLayer(background, "FarForest", "Environment/ForestCanopy/far_forest", -30, 0.05f, 40f, 23f);
            CreateForestLayer(background, "BackgroundTrees", "Environment/ForestCanopy/background_trees", -20, 0.15f, 40f, 23f);

            var midground = CreateGroup(environment, "Midground");
            CreateForestLayer(midground, "Trees And Vines", "Environment/ForestCanopy/midground_trees", -10, 0.30f, 40f, 23f);
            CreateGroup(midground, "Decorations");

            var foreground = CreateGroup(environment, "Foreground");
            // Edge decorations move slightly faster than gameplay, but remain
            // behind the knights so rocks and mushrooms never hide a fighter.
            CreateForestLayer(foreground, "Leaves Rocks And Plants", "Environment/ForestCanopy/foreground_edges", 0, 1.15f, 40f, 23f);
        }

        private static void CreateForestLayer(
            Transform parent,
            string objectName,
            string resourcePath,
            int sortingOrder,
            float parallaxFactor,
            float width,
            float height)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                Vector2.one * 0.5f,
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = objectName;
            var layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            if (objectName == "Leaves Rocks And Plants")
                layer.transform.localPosition = new Vector3(0f, -2.35f, 0f);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            var bounds = sprite.bounds.size;
            layer.transform.localScale = new Vector3(width / bounds.x, height / bounds.y, 1f);
            layer.AddComponent<ParallaxLayer2D>().Configure(parallaxFactor);
        }

        private static Vector2 GetRoomSpawn(int roomIndex) => new Vector2(roomIndex == 0 ? -12f : 12f, -6.8f);

        private static Transform CreateGroup(Transform parent, string objectName)
        {
            var group = new GameObject(objectName).transform;
            group.SetParent(parent);
            return group;
        }

        private static void CreateOneWayPlatform(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var platform = new GameObject(objectName);
            platform.transform.SetParent(parent);
            platform.transform.position = position;
            var isFloor = size.x >= 6.5f;
            var colliderHeight = isFloor ? 1.0f : 0.72f;
            var collider = platform.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(size.x, colliderHeight);
            collider.offset = new Vector2(0f, -colliderHeight * 0.5f);
            collider.usedByEffector = false;
            collider.sharedMaterial = GetFrictionlessMaterial();
            Sprite sprite;
            if (isFloor)
            {
                floorSprite = LoadEnvironmentSprite(
                    floorSprite,
                    "Environment/ForestCanopy/ground_moss",
                    "Mossy Forest Ground");
                sprite = floorSprite;
            }
            else
            {
                platformShortSprite = LoadEnvironmentSprite(
                    platformShortSprite,
                    "Environment/ForestCanopy/platform_moss",
                    "Mossy Forest Platform");
                sprite = platformShortSprite;
            }
            // Source sprites are tightly cropped. These heights keep the mossy
            // body substantial relative to the 2.18-unit knight while the top
            // pivot stays exactly aligned with the physics surface.
            AddEnvironmentRenderer(platform, sprite, new Vector2(size.x, isFloor ? 1.05f : 0.78f), 1);
        }

        private static Sprite GetDebugSprite()
        {
            if (debugSprite == null) debugSprite = Resources.Load<Sprite>("DebugSquare");
            if (debugSprite == null)
            {
                debugSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
                debugSprite.name = "Runtime Debug Square";
            }
            return debugSprite;
        }

        private static void CreateSolidBlock(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var block = new GameObject(objectName);
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.AddComponent<BoxCollider2D>().size = size;
        }

        private static void CreateCameraZone(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Vector2 cameraMinimum,
            Vector2 cameraMaximum)
        {
            var zone = new GameObject(objectName);
            zone.transform.SetParent(parent);
            zone.transform.position = position;
            zone.AddComponent<BoxCollider2D>();
            zone.AddComponent<CameraZone2D>().Configure(size, cameraMinimum, cameraMaximum);
        }

        private static void ConfigureCameraFollow()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var follow = camera.GetComponent<VerticalCameraFollow>();
            if (follow != null) follow.enabled = false;
        }

        private static Sprite LoadEnvironmentSprite(Sprite cached, string resourcePath, string spriteName)
        {
            if (cached != null) return cached;
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 1f),
                64f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            return sprite;
        }

        private static void AddEnvironmentRenderer(
            GameObject target,
            Sprite sprite,
            Vector2 size,
            int sortingOrder,
            bool tiled = false)
        {
            if (sprite == null)
            {
                AddDebugRenderer(target, size, new Color(0.22f, 0.36f, 0.46f));
                return;
            }

            var visual = new GameObject("Visual");
            visual.transform.SetParent(target.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (tiled)
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = size;
            }
            else
            {
                var bounds = sprite.bounds.size;
                visual.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
            }
        }

        private static void CreatePlayer(int playerIndex, string objectName, Vector2 position, PlayerControls controls)
        {
            var player = new GameObject(objectName);
            player.transform.position = position;

            var color = objectName.StartsWith("Player 1")
                ? new Color(0.2f, 0.65f, 1f)
                : new Color(1f, 0.35f, 0.25f);
            AddDebugRenderer(player, Vector2.one, color);

            var body = player.AddComponent<Rigidbody2D>();
            body.mass = 1f;
            body.gravityScale = 3.2f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var playerCollider = player.AddComponent<BoxCollider2D>();
            playerCollider.size = new Vector2(0.95f, 2.18f);
            playerCollider.offset = new Vector2(0f, 0.59f);
            playerCollider.sharedMaterial = GetFrictionlessMaterial();
            player.AddComponent<CharacterStats>();
            player.AddComponent<PlayerController>().Configure(controls);
            player.AddComponent<PlayerHealth>().Configure(playerIndex);
            player.AddComponent<UpgradeController>();
            player.AddComponent<PlayerCombat>().Configure(controls);
            player.AddComponent<ArenaFallRespawner>();
            player.AddComponent<KnightVisualPrototype>().Configure(color);
            player.AddComponent<CharacterMovementEffects>();
        }

        private static PhysicsMaterial2D GetFrictionlessMaterial()
        {
            if (frictionlessMaterial != null) return frictionlessMaterial;
            frictionlessMaterial = new PhysicsMaterial2D("Frictionless Platform Sliding")
            {
                friction = 0f,
                bounciness = 0f
            };
            return frictionlessMaterial;
        }

        private static void AddDebugRenderer(GameObject target, Vector2 size, Color color)
        {
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDebugSprite();
            renderer.color = color;
            target.transform.localScale = size;
        }
    }
}
