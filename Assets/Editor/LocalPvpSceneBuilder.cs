using System.IO;
using LocalPvp.Bootstrap;
using LocalPvp.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LocalPvp.Editor
{
    // Builds the complete local PvP test scene and normalizes imported art assets.
    public static class LocalPvpSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string DebugSpritePath = "Assets/Resources/DebugSquare.png";

        [InitializeOnLoadMethod]
        private static void OpenMainSceneWhenEditorStartsEmpty()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.path == ScenePath)
                {
                    var needsVerticalLevel = GameObject.Find("Forest Canopy Arena v27") == null;
                    var hasSecondPlayer = GameObject.Find("Player 2 (Arrows)") != null;
                    var hasMatchManager = Object.FindAnyObjectByType<MatchManager>() != null;
                    if (needsVerticalLevel || hasSecondPlayer || hasMatchManager)
                        BuildMainScene();
                    return;
                }
                if (!string.IsNullOrEmpty(activeScene.path)) return;
                if (!ContainsOnlyDefaultUntitledObjects(activeScene)) return;

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            };
        }

        private static bool ContainsOnlyDefaultUntitledObjects(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Main Camera" && root.name != "Directional Light")
                    return false;
            }

            return true;
        }

        [MenuItem("Local PvP/Create or Refresh Main Scene")]
        public static void BuildMainScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(PrefabFolder);
            EnsureDebugSprite();
            ConfigureForestCanopyAssets();
            ConfigureBlueKnightFrames();
            ConfigureKnightPreviewSprite();
            CreateModularPlatformLibrary();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrapObject = new GameObject("Local PvP Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<LocalPvpBootstrap>();
            bootstrap.EnsureSceneContents();

            AddKnightPreview(GameObject.Find("Player 1 (WASD)"), new Color(0.2f, 0.65f, 1f));
            AddKnightPreview(GameObject.Find("Player 2 (Arrows)"), new Color(1f, 0.35f, 0.25f));
            SavePlayerPrefab("Player 1 (WASD)", "Player1.prefab");
            SavePlayerPrefab("Player 2 (Arrows)", "Player2.prefab");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Local PvP scene created at {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            BuildMainScene();
            EditorApplication.Exit(0);
        }

        [MenuItem("Local PvP/Create Modular Platform Library")]
        public static void CreateModularPlatformLibrary()
        {
            const string textureFolder = "Assets/Resources/Environment/ModularPlatforms";
            const string prefabFolder = "Assets/Prefabs/Platforms";
            EnsureFolder(prefabFolder);

            var names = new[]
            {
                "platform_short", "platform_medium", "platform_long", "platform_extra_long",
                "block_full", "block_left", "block_middle", "block_right", "support_column"
            };

            foreach (var name in names)
            {
                var path = $"{textureFolder}/{name}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                var piece = new GameObject(name);
                var renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 1;

                if (name.StartsWith("platform_"))
                {
                    var collider = piece.AddComponent<BoxCollider2D>();
                    var colliderHeight = Mathf.Max(0.28f, sprite.bounds.size.y * 0.85f);
                    collider.size = new Vector2(sprite.bounds.size.x, colliderHeight);
                    collider.offset = new Vector2(0f, sprite.bounds.max.y - colliderHeight * 0.5f);
                    collider.usedByEffector = false;
                }
                else if (name.StartsWith("block_"))
                {
                    var collider = piece.AddComponent<BoxCollider2D>();
                    collider.size = sprite.bounds.size * 0.9f;
                    collider.offset = sprite.bounds.center;
                }

                PrefabUtility.SaveAsPrefabAsset(piece, $"{prefabFolder}/{name}.prefab");
                Object.DestroyImmediate(piece);
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureDebugSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(DebugSpritePath) != null) return;

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(DebugSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(DebugSpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(DebugSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        private static void ConfigureKnightPreviewSprite()
        {
            const string path = "Assets/Resources/Characters/BlueKnight/Idle/idle_0.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (texture == null || importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 220f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureForestCanopyAssets()
        {
            var paths = new[]
            {
                "Assets/Resources/Environment/ForestCanopy/far_forest.png",
                "Assets/Resources/Environment/ForestCanopy/background_trees.png",
                "Assets/Resources/Environment/ForestCanopy/midground_trees.png",
                "Assets/Resources/Environment/ForestCanopy/foreground_edges.png",
                "Assets/Resources/Environment/ForestCanopy/platform_moss.png",
                "Assets/Resources/Environment/ForestCanopy/ground_moss.png"
            };

            foreach (var path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureBlueKnightFrames()
        {
            const string root = "Assets/Resources/Characters/BlueKnight";
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 220f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static void AddKnightPreview(GameObject player, Color tint)
        {
            if (player == null) return;

            var placeholder = player.GetComponent<SpriteRenderer>();
            if (placeholder != null) placeholder.enabled = false;

            var visual = player.transform.Find("Knight Sprite Visual");
            if (visual == null)
            {
                visual = new GameObject("Knight Sprite Visual").transform;
                visual.SetParent(player.transform, false);
            }

            visual.localPosition = new Vector3(0f, -0.9f, 0f);
            visual.localScale = new Vector3(0.986f, 0.918f, 1f);
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/Characters/BlueKnight/Idle/idle_0.png");
            renderer.sortingOrder = 4;
            renderer.color = Color.white;
            var knight = player.GetComponent<KnightVisualPrototype>();
            if (knight != null) knight.Configure(tint);
        }

        private static void SavePlayerPrefab(string objectName, string fileName)
        {
            var player = GameObject.Find(objectName);
            if (player == null) return;
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                player,
                $"{PrefabFolder}/{fileName}",
                InteractionMode.AutomatedAction);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }
    }
}
