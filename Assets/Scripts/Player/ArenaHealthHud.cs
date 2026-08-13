using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class ArenaHealthHud : MonoBehaviour
    {
        private const float HealthFillWidth = 5.15f;
        private const float BloodFillHeight = 0.67f;
        private const float PotionFillWidth = 0.68f;
        private const float PotionFillHeight = 0.62f;
        private readonly PlayerHealth[] players = new PlayerHealth[2];
        private readonly SpriteRenderer[] healthRenderers = new SpriteRenderer[2];
        private readonly Transform[] attackFills = new Transform[2];
        private readonly Transform[] dodgeFills = new Transform[2];
        private readonly Transform[] kickFills = new Transform[2];
        private readonly TextMesh[] labels = new TextMesh[2];
        private Sprite whiteSprite;
        private Sprite bottomMaskSprite;
        private Sprite swordFrameSprite;
        private Sprite potionFrameSprite;
        private Sprite attackPotionLiquidSprite;
        private Sprite dodgePotionLiquidSprite;
        private Sprite kickPotionLiquidSprite;
        private readonly Sprite[] bloodStages = new Sprite[11];

        private void Awake()
        {
            whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0f, 0.5f),
                1f);
            bottomMaskSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0f),
                1f);
            swordFrameSprite = LoadUiSprite("UI/ForestHud/sword_health_frame", "Sword Health Frame");
            potionFrameSprite = LoadUiSprite("UI/ForestHud/potion_frame", "Cooldown Potion Frame");
            attackPotionLiquidSprite = LoadUiSprite("UI/ForestHud/potion_liquid_attack", "Attack Potion Liquid");
            dodgePotionLiquidSprite = LoadUiSprite("UI/ForestHud/potion_liquid_dodge", "Dodge Potion Liquid");
            kickPotionLiquidSprite = LoadUiSprite("UI/ForestHud/potion_liquid_kick", "Kick Potion Liquid");
            for (var stage = 0; stage < bloodStages.Length; stage++)
                bloodStages[stage] = LoadUiSprite($"UI/ForestHud/BloodStages/blood_{stage:00}", $"Blood Stage {stage}");
            CreatePlayerHud(0, new Vector2(-16.15f, 8.15f));
            CreatePlayerHud(1, new Vector2(9.35f, 8.15f));
            RefreshPlayers();
        }

        private void Update()
        {
            if (players[0] == null || players[1] == null) RefreshPlayers();
            for (var index = 0; index < players.Length; index++)
            {
                var player = players[index];
                if (player == null) continue;
                if ((healthRenderers[index] == null || labels[index] == null) && !RestoreHudReferences(index)) continue;
                var healthRatio = Mathf.Clamp01(player.HealthRatio);
                var healthStage = Mathf.Clamp(Mathf.RoundToInt(healthRatio * 10f), 0, 10);
                healthRenderers[index].sprite = bloodStages[healthStage];
                labels[index].text = $"PLAYER {index + 1}";

                var combat = player.GetComponent<PlayerCombat>();
                var controller = player.GetComponent<PlayerController>();
                if (combat != null)
                {
                    SetPotionAmount(attackFills[index], combat.AttackReadyAmount);
                    SetPotionAmount(kickFills[index], combat.KickReadyAmount);
                }
                if (controller != null)
                    SetPotionAmount(dodgeFills[index], controller.DodgeReadyAmount);
            }
        }

        private bool RestoreHudReferences(int index)
        {
            var root = transform.Find($"Player {index + 1} Sword HUD");
            if (root == null) return false;

            healthRenderers[index] = root.Find("Blood Liquid")?.GetComponent<SpriteRenderer>();
            labels[index] = root.Find("Player And Health Label")?.GetComponent<TextMesh>();
            attackFills[index] = root.Find("Attack Potion Amount Mask");
            dodgeFills[index] = root.Find("Dodge Potion Amount Mask");
            kickFills[index] = root.Find("Kick Potion Amount Mask");
            return healthRenderers[index] != null && labels[index] != null;
        }

        private void CreatePlayerHud(int index, Vector2 position)
        {
            var root = new GameObject($"Player {index + 1} Sword HUD").transform;
            root.SetParent(transform, false);
            root.position = position;

            var cavityStart = index == 0 ? 6.8f - (0.72f + HealthFillWidth) : 0.72f;
            var healthShadow = CreateSolid(root, "Health Cavity", new Vector2(cavityStart, 0f), new Vector2(HealthFillWidth, 0.7f), new Color(0.11f, 0.025f, 0.02f), 100);
            var fillObject = new GameObject("Blood Liquid");
            fillObject.transform.SetParent(root, false);
            // The painted cavity is slightly off-centre inside the sword asset.
            // Mirror that offset around the frame centre as well as the sprite,
            // otherwise the left sword has a larger empty gap than the right.
            var bloodCenter = 0.72f + HealthFillWidth * 0.5f;
            var mirroredBloodCenter = 6.8f - bloodCenter;
            fillObject.transform.localPosition = new Vector3(index == 0 ? mirroredBloodCenter : bloodCenter, 0f, 0f);
            var bloodRenderer = fillObject.AddComponent<SpriteRenderer>();
            bloodRenderer.sprite = bloodStages[10] != null ? bloodStages[10] : whiteSprite;
            bloodRenderer.color = Color.white;
            bloodRenderer.sortingOrder = 102;
            var bloodBounds = bloodRenderer.sprite.bounds.size;
            fillObject.transform.localScale = new Vector3(
                (index == 0 ? -1f : 1f) * HealthFillWidth / bloodBounds.x,
                BloodFillHeight / bloodBounds.y,
                1f);
            healthRenderers[index] = bloodRenderer;

            CreateFramedSprite(root, "Sword Frame", swordFrameSprite, new Vector2(3.4f, 0f), new Vector2(6.8f, 1.88f), 103, index == 0);

            var labelObject = new GameObject("Player And Health Label");
            labelObject.transform.SetParent(root, false);
            labelObject.transform.localPosition = new Vector3(3.4f, 1.08f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.1f;
            label.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 106;
            labels[index] = label;

            attackFills[index] = CreatePotion(root, "Attack Potion", new Vector2(1.25f, -1.45f), attackPotionLiquidSprite);
            dodgeFills[index] = CreatePotion(root, "Dodge Potion", new Vector2(3.4f, -1.45f), dodgePotionLiquidSprite);
            kickFills[index] = CreatePotion(root, "Kick Potion", new Vector2(5.55f, -1.45f), kickPotionLiquidSprite);
        }

        private Transform CreatePotion(Transform parent, string objectName, Vector2 position, Sprite liquidSprite)
        {
            var liquid = new GameObject(objectName + " Liquid");
            liquid.transform.SetParent(parent, false);
            liquid.transform.localPosition = new Vector3(position.x + 0.16f, position.y - 0.13f, 0f);
            var fillRenderer = liquid.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = liquidSprite != null ? liquidSprite : whiteSprite;
            fillRenderer.color = Color.white;
            fillRenderer.sortingOrder = 102;
            fillRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var liquidBounds = fillRenderer.sprite.bounds.size;
            liquid.transform.localScale = new Vector3(0.70f / liquidBounds.x, 0.62f / liquidBounds.y, 1f);

            var maskObject = new GameObject(objectName + " Amount Mask");
            maskObject.transform.SetParent(parent, false);
            maskObject.transform.localPosition = new Vector3(position.x + 0.16f, position.y - 0.45f, 0f);
            maskObject.transform.localScale = new Vector3(PotionFillWidth, PotionFillHeight, 1f);
            var mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = bottomMaskSprite;
            mask.alphaCutoff = 0.01f;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = 101;
            mask.frontSortingOrder = 102;

            CreateFramedSprite(parent, objectName + " Frame", potionFrameSprite, position, new Vector2(1.28f, 1.48f), 103, false);
            return maskObject.transform;
        }

        private GameObject CreateSolid(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, int order)
        {
            var result = new GameObject(objectName);
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = size;
            var renderer = result.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return result;
        }

        private static void CreateFramedSprite(Transform parent, string objectName, Sprite sprite, Vector2 position, Vector2 size, int order, bool flipX)
        {
            if (sprite == null) return;
            var result = new GameObject(objectName);
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            var renderer = result.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            var bounds = sprite.bounds.size;
            result.transform.localScale = new Vector3((flipX ? -1f : 1f) * size.x / bounds.x, size.y / bounds.y, 1f);
        }

        private static void SetPotionAmount(Transform fill, float amount)
        {
            if (fill == null) return;
            fill.localScale = new Vector3(PotionFillWidth, Mathf.Max(0.001f, PotionFillHeight * Mathf.Clamp01(amount)), 1f);
        }

        private static Sprite LoadUiSprite(string resourcePath, string spriteName)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * 0.5f, 100f);
            sprite.name = spriteName;
            return sprite;
        }

        private void RefreshPlayers()
        {
            foreach (var health in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                if (health.PlayerIndex >= 0 && health.PlayerIndex < players.Length)
                    players[health.PlayerIndex] = health;
        }
    }
}
