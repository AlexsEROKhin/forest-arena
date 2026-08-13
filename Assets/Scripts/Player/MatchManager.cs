using System.Collections;
using UnityEngine;

namespace LocalPvp.Player
{
    public sealed class MatchManager : MonoBehaviour
    {
        private const int ScoreToWin = 3;
        private readonly PlayerHealth[] players = new PlayerHealth[2];
        private readonly int[] scores = new int[2];
        private readonly int[] choices = { -1, -1 };
        private readonly SpriteRenderer[,] scorePips = new SpriteRenderer[2, ScoreToWin];
        private readonly GameObject[,] choiceBlocks = new GameObject[2, 3];
        private bool resolvingRound;
        private bool choosingUpgrades;
        private float choiceDeadline;
        private readonly TextMesh[] scoreTexts = new TextMesh[2];
        private TextMesh countdownText;
        private GameObject controlsScreen;
        private float controlsScreenEndsAt;

        public static MatchManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            RefreshPlayerReferences();
            CreateTechnicalInterface();
            controlsScreenEndsAt = Time.time + 6f;
        }

        public void RegisterPlayer(int index, PlayerHealth health)
        {
            if (index < 0 || index >= players.Length || health == null) return;
            players[index] = health;
        }

        public void PlayerDied(int defeatedPlayer)
        {
            if (resolvingRound) return;
            RefreshPlayerReferences();
            if (players[0] == null || players[1] == null)
            {
                Debug.LogError("Round cannot be resolved because both players are not registered.", this);
                return;
            }
            resolvingRound = true;
            var winner = 1 - defeatedPlayer;
            scores[winner]++;
            UpdateScorePips();
            StartCoroutine(ResolveRound(winner));
        }

        private void RefreshPlayerReferences()
        {
            var scenePlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            foreach (var player in scenePlayers)
                RegisterPlayer(player.PlayerIndex, player);
        }

        private void Update()
        {
            if (controlsScreen != null && controlsScreen.activeSelf && Time.time >= controlsScreenEndsAt)
                controlsScreen.SetActive(false);

            if (countdownText != null)
                countdownText.text = choosingUpgrades
                    ? $"ВЫБОР: {Mathf.Max(0, Mathf.CeilToInt(choiceDeadline - Time.time))}"
                    : string.Empty;

            if (!choosingUpgrades) return;
            ReadChoice(0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3);
            ReadChoice(1, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9);
            ReadChoice(1, KeyCode.J, KeyCode.K, KeyCode.L);

            if (Time.time >= choiceDeadline)
            {
                if (choices[0] < 0) SelectChoice(0, Random.Range(0, 3));
                if (choices[1] < 0) SelectChoice(1, Random.Range(0, 3));
            }
        }

        private IEnumerator ResolveRound(int winner)
        {
            foreach (var player in players) player.PrepareForIntermission();
            yield return new WaitForSeconds(0.25f);

            if (scores[winner] >= ScoreToWin)
            {
                players[winner].GetComponent<SpriteRenderer>().color = new Color(1f, 0.85f, 0.15f);
                var knight = players[winner].GetComponent<KnightVisualPrototype>();
                if (knight != null) knight.SetTint(new Color(1f, 0.85f, 0.15f));
                yield return new WaitForSeconds(1f);
                scores[0] = scores[1] = 0;
                foreach (var player in players) player.ResetUpgrades();
                UpdateScorePips();
                StartNextRound();
                yield break;
            }

            choices[0] = choices[1] = -1;
            choosingUpgrades = true;
            choiceDeadline = Time.time + 6f;
            SetChoicesVisible(true);
            yield return new WaitUntil(() => choices[0] >= 0 && choices[1] >= 0);
            ApplyChoice(players[0], choices[0]);
            ApplyChoice(players[1], choices[1]);
            choosingUpgrades = false;
            SetChoicesVisible(false);
            yield return new WaitForSeconds(0.1f);
            StartNextRound();
        }

        private void StartNextRound()
        {
            foreach (var player in players) player.RespawnForRound();
            resolvingRound = false;
        }

        private void ReadChoice(int player, KeyCode first, KeyCode second, KeyCode third)
        {
            if (choices[player] >= 0) return;
            if (Input.GetKeyDown(first)) choices[player] = 0;
            else if (Input.GetKeyDown(second)) choices[player] = 1;
            else if (Input.GetKeyDown(third)) choices[player] = 2;
            if (choices[player] >= 0) SelectChoice(player, choices[player]);
        }

        private void SelectChoice(int player, int choice)
        {
            choices[player] = choice;
            choiceBlocks[player, choice].transform.localScale = Vector3.one * 0.61f;
        }

        private static void ApplyChoice(PlayerHealth player, int choice)
        {
            var type = choice == 0 ? UpgradeType.Damage : choice == 1 ? UpgradeType.MaxHealth : UpgradeType.MoveSpeed;
            player.GetComponent<UpgradeController>().Apply(type);
        }

        private void CreateTechnicalInterface()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            var choiceColors = new[] { new Color(1f, 0.25f, 0.25f), new Color(0.25f, 1f, 0.35f), new Color(1f, 0.85f, 0.2f) };
            for (var player = 0; player < 2; player++)
            {
                scoreTexts[player] = CreateText(
                    $"Player {player + 1} Numeric Score",
                    player == 0 ? "ИГРОК 1: 0" : "ИГРОК 2: 0",
                    new Vector3(player == 0 ? -3.8f : 3.8f, 3.8f, 0f),
                    player == 0 ? new Color(0.2f, 0.65f, 1f) : new Color(1f, 0.35f, 0.25f),
                    0.1f);

                for (var pip = 0; pip < ScoreToWin; pip++)
                {
                    var scoreObject = new GameObject($"Player {player + 1} Score {pip + 1}");
                    scoreObject.transform.position = new Vector3((player == 0 ? -2.2f : 1.4f) + pip * 0.4f, 3.8f, 0f);
                    scoreObject.transform.localScale = Vector3.one * 0.25f;
                    scorePips[player, pip] = scoreObject.AddComponent<SpriteRenderer>();
                    scorePips[player, pip].sprite = sprite;
                    scorePips[player, pip].sortingOrder = 20;
                }

                for (var choice = 0; choice < 3; choice++)
                {
                    var block = new GameObject($"Player {player + 1} Choice {choice + 1}");
                    block.transform.position = new Vector3((player == 0 ? -5f : 3.6f) + choice * 0.7f, 2.7f, 0f);
                    block.transform.localScale = Vector3.one * 0.45f;
                    var renderer = block.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = choiceColors[choice];
                    renderer.sortingOrder = 20;
                    choiceBlocks[player, choice] = block;
                    var key = player == 0 ? (choice + 1).ToString() : choice == 0 ? "7/J" : choice == 1 ? "8/K" : "9/L";
                    var upgradeName = choice == 0 ? "УРОН" : choice == 1 ? "ЗДОРОВЬЕ" : "СКОРОСТЬ";
                    var label = CreateText(
                        $"Player {player + 1} Choice {choice + 1} Label",
                        $"{key}\n{upgradeName}",
                        new Vector3(0f, -0.75f, 0f),
                        Color.white,
                        0.08f);
                    label.transform.SetParent(block.transform, false);
                    block.SetActive(false);
                }
            }

            countdownText = CreateText("Upgrade Countdown", string.Empty, new Vector3(0f, 2.8f, 0f), Color.white, 0.11f);
            CreateControlsScreen(sprite);
            UpdateScorePips();
        }

        private void CreateControlsScreen(Sprite sprite)
        {
            controlsScreen = new GameObject("Controls Screen");
            var background = controlsScreen.AddComponent<SpriteRenderer>();
            background.sprite = sprite;
            background.color = new Color(0.04f, 0.04f, 0.06f, 0.9f);
            background.sortingOrder = 29;
            controlsScreen.transform.position = new Vector3(0f, 0.7f, 0f);
            controlsScreen.transform.localScale = new Vector3(10f, 4.4f, 1f);

            var text = CreateText(
                "Controls Text",
                "УПРАВЛЕНИЕ\n\nИГРОК 1: A/D  |  W прыжок  |  F удар  |  Left Shift уклонение\nИГРОК 2: ←/→  |  ↑ прыжок  |  Right Ctrl удар  |  Right Shift уклонение\n\nНаправление + Shift + удар = длинный рывковый удар\nУдар в воздухе = воздушная атака",
                Vector3.zero,
                Color.white,
                0.075f);
            text.transform.SetParent(controlsScreen.transform, false);
            text.transform.localScale = new Vector3(0.1f, 0.23f, 1f);
            text.GetComponent<MeshRenderer>().sortingOrder = 30;
        }

        private static TextMesh CreateText(string objectName, string content, Vector3 position, Color color, float characterSize)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.position = position;
            var text = textObject.AddComponent<TextMesh>();
            text.text = content;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
            textObject.GetComponent<MeshRenderer>().sortingOrder = 21;
            return text;
        }

        private void UpdateScorePips()
        {
            var colors = new[] { new Color(0.2f, 0.65f, 1f), new Color(1f, 0.35f, 0.25f) };
            for (var player = 0; player < 2; player++)
            for (var pip = 0; pip < ScoreToWin; pip++)
                scorePips[player, pip].color = pip < scores[player] ? colors[player] : new Color(0.2f, 0.2f, 0.2f);
            if (scoreTexts[0] != null) scoreTexts[0].text = $"ИГРОК 1: {scores[0]}";
            if (scoreTexts[1] != null) scoreTexts[1].text = $"ИГРОК 2: {scores[1]}";
        }

        private void SetChoicesVisible(bool visible)
        {
            for (var player = 0; player < 2; player++)
            for (var choice = 0; choice < 3; choice++)
            {
                choiceBlocks[player, choice].transform.localScale = Vector3.one * 0.45f;
                choiceBlocks[player, choice].SetActive(visible);
            }
        }
    }
}
