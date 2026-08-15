using LocalPvp.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LocalPvp.Networking
{
    /// <summary>
    /// Host-authoritative bridge around the existing local arena. Clients send
    /// input state; the host simulates the original Rigidbody2D combat and sends
    /// compact presentation snapshots back to the joining browser.
    /// </summary>
    public sealed class OnlineArenaSession : MonoBehaviour
    {
        private const string InputMessage = "LocalPvp/Input/v1";
        private const string SnapshotMessage = "LocalPvp/Snapshot/v1";
        private const float SnapshotInterval = 1f / 50f;
        private const float PositionSnapDistance = 4f;
        private const float LocalPlayerSnapDistance = 1.5f;
        private const float LocalPlayerCorrectionRate = 6f;
        private const float LocalSnapshotExtrapolation = 0.08f;

        private readonly PlayerController[] controllers = new PlayerController[2];
        private readonly PlayerCombat[] combats = new PlayerCombat[2];
        private readonly PlayerHealth[] health = new PlayerHealth[2];
        private readonly Vector2[] targetPositions = new Vector2[2];
        private readonly Vector2[] targetVelocities = new Vector2[2];
        private readonly bool[] hasTargetPosition = new bool[2];

        private NetworkManager manager;
        private InputFrame localInput;
        private InputFrame remoteInput;
        private InputFrame previousHostInput;
        private InputFrame previousRemoteInput;
        private InputFrame previousPredictedInput;
        private float nextSnapshotAt;
        private bool running;
        private bool host;

        private struct InputFrame
        {
            public float Horizontal;
            public bool Jump;
            public bool Attack;
            public bool Kick;
            public bool Dodge;
        }

        private void Awake()
        {
            manager = GetComponent<NetworkManager>();
        }

        public void BeginAsHost()
        {
            host = true;
            running = true;
            FindPlayers();
            SetHostSimulationMode();
            manager.CustomMessagingManager.RegisterNamedMessageHandler(InputMessage, ReceiveInput);
        }

        public void BeginAsClient()
        {
            host = false;
            running = true;
            FindPlayers();
            SetClientPresentationMode();
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
        }

        private void FindPlayers()
        {
            foreach (var playerHealth in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                var index = playerHealth.PlayerIndex;
                if (index < 0 || index >= 2) continue;
                health[index] = playerHealth;
                controllers[index] = playerHealth.GetComponent<PlayerController>();
                combats[index] = playerHealth.GetComponent<PlayerCombat>();
                targetPositions[index] = playerHealth.transform.position;
            }
        }

        private void SetHostSimulationMode()
        {
            for (var index = 0; index < 2; index++)
            {
                controllers[index]?.SetNetworkPresentationMode(false);
                controllers[index]?.SetExternalInputMode(true);
                combats[index]?.SetNetworkPresentationMode(false);
                combats[index]?.SetExternalInputMode(true);
            }
        }

        private void SetClientPresentationMode()
        {
            // Player 1 is the remote host and remains a pure visual replica.
            controllers[0]?.SetNetworkPresentationMode(true);
            combats[0]?.SetNetworkPresentationMode(true);

            // Player 2 is controlled by this browser. Simulate movement locally
            // for immediate response, then reconcile it with host snapshots.
            // Combat damage remains host-authoritative; only its animation is
            // previewed locally while the input travels through Relay.
            controllers[1]?.SetNetworkPresentationMode(false);
            controllers[1]?.SetExternalInputMode(true);
            combats[1]?.SetNetworkPresentationMode(true);
        }

        private void Update()
        {
            if (!running || manager == null || !manager.IsListening) return;
            localInput = ReadLocalInput();

            if (host)
            {
                ApplyInput(0, localInput, ref previousHostInput);
                ApplyInput(1, remoteInput, ref previousRemoteInput);
                if (Time.unscaledTime >= nextSnapshotAt)
                {
                    nextSnapshotAt = Time.unscaledTime + SnapshotInterval;
                    SendSnapshot();
                }
            }
            else
            {
                ApplyPredictedInput(localInput);
                SendInput(localInput);
                SmoothRemotePlayers();
            }
        }

        private static InputFrame ReadLocalInput()
        {
            return new InputFrame
            {
                Horizontal = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                Jump = Input.GetKey(KeyCode.W),
                Attack = Input.GetKey(KeyCode.F),
                Kick = Input.GetKey(KeyCode.G),
                Dodge = Input.GetKey(KeyCode.LeftShift)
            };
        }

        private void ApplyInput(int index, InputFrame current, ref InputFrame previous)
        {
            if (controllers[index] == null || combats[index] == null) return;
            var jumpPressed = current.Jump && !previous.Jump;
            var jumpReleased = !current.Jump && previous.Jump;
            var attackPressed = current.Attack && !previous.Attack;
            var kickPressed = current.Kick && !previous.Kick;
            var dodgePressed = current.Dodge && !previous.Dodge && !current.Attack && !current.Kick;
            controllers[index].SubmitExternalInput(current.Horizontal, jumpPressed, jumpReleased, dodgePressed);
            combats[index].SubmitExternalInput(attackPressed, kickPressed, current.Dodge);
            previous = current;
        }

        private void ApplyPredictedInput(InputFrame current)
        {
            if (controllers[1] == null) return;

            var jumpPressed = current.Jump && !previousPredictedInput.Jump;
            var jumpReleased = !current.Jump && previousPredictedInput.Jump;
            var attackPressed = current.Attack && !previousPredictedInput.Attack;
            var kickPressed = current.Kick && !previousPredictedInput.Kick;
            var dodgePressed = current.Dodge && !previousPredictedInput.Dodge
                && !current.Attack && !current.Kick;

            if (health[1] == null || !health[1].IsDead)
            {
                controllers[1].SubmitExternalInput(
                    current.Horizontal,
                    jumpPressed,
                    jumpReleased,
                    dodgePressed);

                if (attackPressed || kickPressed)
                {
                    combats[1]?.PreviewNetworkAttack(
                        kickPressed,
                        current.Dodge && Mathf.Abs(current.Horizontal) > 0.1f);
                }
            }

            previousPredictedInput = current;
        }

        private void SendInput(InputFrame input)
        {
            if (!manager.IsConnectedClient) return;
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(input.Horizontal);
            writer.WriteValueSafe(input.Jump);
            writer.WriteValueSafe(input.Attack);
            writer.WriteValueSafe(input.Kick);
            writer.WriteValueSafe(input.Dodge);
            manager.CustomMessagingManager.SendNamedMessage(
                InputMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveInput(ulong senderId, FastBufferReader reader)
        {
            if (!host || senderId == NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out remoteInput.Horizontal);
            reader.ReadValueSafe(out remoteInput.Jump);
            reader.ReadValueSafe(out remoteInput.Attack);
            reader.ReadValueSafe(out remoteInput.Kick);
            reader.ReadValueSafe(out remoteInput.Dodge);
        }

        private void SendSnapshot()
        {
            if (!manager.IsServer || manager.ConnectedClientsIds.Count <= 1) return;
            var writer = new FastBufferWriter(320, Allocator.Temp);
            try
            {
                for (var index = 0; index < 2; index++) WritePlayerSnapshot(ref writer, index);

                foreach (var clientId in manager.ConnectedClientsIds)
                {
                    if (clientId == NetworkManager.ServerClientId) continue;
                    manager.CustomMessagingManager.SendNamedMessage(
                        SnapshotMessage,
                        clientId,
                        writer,
                        NetworkDelivery.UnreliableSequenced);
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void WritePlayerSnapshot(ref FastBufferWriter writer, int index)
        {
            var controller = controllers[index];
            var combat = combats[index];
            var playerHealth = health[index];
            var position = playerHealth != null ? (Vector2)playerHealth.transform.position : Vector2.zero;
            var velocity = controller != null ? controller.Velocity : Vector2.zero;
            writer.WriteValueSafe(position.x);
            writer.WriteValueSafe(position.y);
            writer.WriteValueSafe(velocity.x);
            writer.WriteValueSafe(velocity.y);
            writer.WriteValueSafe(controller != null ? controller.FacingDirection : 1);
            writer.WriteValueSafe(controller != null ? controller.MoveDirection : 0f);
            writer.WriteValueSafe(controller != null && controller.IsGrounded);
            writer.WriteValueSafe(controller != null && controller.IsDodgePose);
            writer.WriteValueSafe(controller != null && controller.IsDodgePose
                ? Mathf.Max(0f, Time.time - controller.DodgeStartedAt)
                : 0f);
            writer.WriteValueSafe(controller != null ? controller.JumpsUsed : 0);
            writer.WriteValueSafe(controller != null && !float.IsNegativeInfinity(controller.LastJumpStartedAt)
                ? Mathf.Max(0f, Time.time - controller.LastJumpStartedAt)
                : -1f);
            writer.WriteValueSafe(combat != null && combat.IsAttacking);
            writer.WriteValueSafe(combat != null && combat.IsKicking);
            writer.WriteValueSafe((byte)(combat != null ? combat.CurrentAttackType : AttackType.Basic));
            writer.WriteValueSafe(combat != null ? combat.AttackElapsed : 0f);
            writer.WriteValueSafe(playerHealth != null ? playerHealth.CurrentHealth : 0);
            writer.WriteValueSafe(playerHealth != null && playerHealth.IsDead);
            writer.WriteValueSafe(playerHealth != null && playerHealth.WasRecentlyHit);
        }

        private void ReceiveSnapshot(ulong senderId, FastBufferReader reader)
        {
            if (host) return;
            for (var index = 0; index < 2; index++) ReadPlayerSnapshot(reader, index);
        }

        private void ReadPlayerSnapshot(FastBufferReader reader, int index)
        {
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float velocityX);
            reader.ReadValueSafe(out float velocityY);
            reader.ReadValueSafe(out int direction);
            reader.ReadValueSafe(out float movement);
            reader.ReadValueSafe(out bool grounded);
            reader.ReadValueSafe(out bool dodging);
            reader.ReadValueSafe(out float dodgeElapsed);
            reader.ReadValueSafe(out int jumpsUsed);
            reader.ReadValueSafe(out float jumpElapsed);
            reader.ReadValueSafe(out bool attacking);
            reader.ReadValueSafe(out bool kicking);
            reader.ReadValueSafe(out byte attackType);
            reader.ReadValueSafe(out float attackElapsed);
            reader.ReadValueSafe(out int currentHealth);
            reader.ReadValueSafe(out bool dead);
            reader.ReadValueSafe(out bool recentlyHit);

            targetPositions[index] = new Vector2(x, y);
            targetVelocities[index] = new Vector2(velocityX, velocityY);
            hasTargetPosition[index] = true;
            if (index == 0)
            {
                controllers[index]?.ApplyNetworkPresentation(
                    new Vector2(velocityX, velocityY),
                    direction,
                    movement,
                    grounded,
                    dodging,
                    dodgeElapsed,
                    jumpsUsed,
                    jumpElapsed);
            }
            combats[index]?.ApplyNetworkPresentation(attacking, kicking, (AttackType)attackType, attackElapsed);
            health[index]?.ApplyNetworkPresentation(currentHealth, dead, recentlyHit);
        }

        private void SmoothRemotePlayers()
        {
            for (var index = 0; index < 2; index++)
            {
                if (!hasTargetPosition[index] || health[index] == null) continue;
                var current = (Vector2)health[index].transform.position;
                var target = targetPositions[index] + targetVelocities[index] * LocalSnapshotExtrapolation;

                if (index == 1 && controllers[index] != null)
                {
                    controllers[index].ReconcilePredictedPosition(
                        target,
                        LocalPlayerSnapDistance,
                        LocalPlayerCorrectionRate);
                    continue;
                }

                health[index].transform.position = Vector2.Distance(current, target) > PositionSnapDistance
                    ? target
                    : Vector2.Lerp(current, target, 24f * Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            if (manager == null || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(InputMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessage);
        }
    }
}
