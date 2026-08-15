using System;
using System.Threading.Tasks;
using LocalPvp.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LocalPvp.Networking
{
    /// <summary>
    /// Small, dependency-free front end for local play and two-player Relay rooms.
    /// The arena remains local by default; online mode is enabled only after the
    /// player creates or joins a room.
    /// </summary>
    public sealed class OnlineMatchLauncher : MonoBehaviour
    {
        private enum MenuState
        {
            Main,
            Joining,
            Connecting,
            InRoom,
            Local
        }

        private static GUIStyle titleStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle codeStyle;

        private MenuState state;
        private NetworkManager networkManager;
        private OnlineArenaSession arenaSession;
        private string joinCodeInput = string.Empty;
        private string roomCode = string.Empty;
        private string status = "Choose a game mode";
        private bool operationRunning;

        public string RoomCode => roomCode;
        public bool IsOnline => state == MenuState.InRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureLauncherExists()
        {
            if (FindAnyObjectByType<OnlineMatchLauncher>() != null) return;
            new GameObject("Online Match Launcher").AddComponent<OnlineMatchLauncher>();
        }

        private void Awake()
        {
            state = Application.isBatchMode ? MenuState.Local : MenuState.Main;
            if (Application.isBatchMode) return;
            Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (state == MenuState.Local) return;

            if (state == MenuState.InRoom)
            {
                DrawRoomStatus();
                return;
            }

            var width = Mathf.Min(520f, Screen.width - 32f);
            var height = state == MenuState.Joining ? 360f : 320f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            GUILayout.Label("FOREST ARENA", titleStyle);
            GUILayout.Space(10f);
            GUILayout.Label(status, labelStyle);
            GUILayout.FlexibleSpace();

            if (state == MenuState.Joining)
            {
                GUILayout.Label("ROOM CODE", labelStyle);
                joinCodeInput = GUILayout.TextField(joinCodeInput.ToUpperInvariant(), 12, codeStyle, GUILayout.Height(46f));
                GUILayout.Space(12f);
                GUI.enabled = !operationRunning && !string.IsNullOrWhiteSpace(joinCodeInput);
                if (GUILayout.Button("CONNECT", GUILayout.Height(44f)))
                    _ = JoinRoomAsync();
                GUI.enabled = !operationRunning;
                if (GUILayout.Button("BACK", GUILayout.Height(36f)))
                {
                    state = MenuState.Main;
                    status = "Choose a game mode";
                }
            }
            else
            {
                GUI.enabled = !operationRunning;
                if (GUILayout.Button("CREATE ONLINE ROOM", GUILayout.Height(46f)))
                    _ = HostRoomAsync();
                if (GUILayout.Button("JOIN WITH CODE", GUILayout.Height(46f)))
                {
                    state = MenuState.Joining;
                    status = "Enter the room code shown by the host";
                }
                if (GUILayout.Button("LOCAL TWO-PLAYER", GUILayout.Height(42f)))
                    StartLocalGame();
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawRoomStatus()
        {
            var rect = new Rect((Screen.width - 420f) * 0.5f, 12f, 420f, roomCode.Length > 0 ? 92f : 62f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 16f));
            GUILayout.Label(status, labelStyle);
            if (roomCode.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"CODE: {roomCode}", codeStyle);
                if (GUILayout.Button("COPY", GUILayout.Width(120f), GUILayout.Height(30f)))
                    GUIUtility.systemCopyBuffer = roomCode;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private async Task HostRoomAsync()
        {
            if (operationRunning) return;
            operationRunning = true;
            state = MenuState.Connecting;
            status = "Creating room...";
            try
            {
                await InitializeServicesAsync();
                CreateNetworkManager();
                var preferredRegion = await GetPreferredRelayRegionAsync();
                var allocation = await RelayService.Instance.CreateAllocationAsync(1, preferredRegion);
                roomCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                ConfigureRelay(allocation.ToRelayServerData("wss"));
                if (!networkManager.StartHost())
                    throw new InvalidOperationException("Unity Netcode не смог запустить хоста.");

                arenaSession.BeginAsHost();
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                state = MenuState.InRoom;
                status = "Waiting for Player 2";
                Time.timeScale = 1f;
            }
            catch (Exception exception)
            {
                HandleConnectionError(exception);
            }
            finally
            {
                operationRunning = false;
            }
        }

        private async Task JoinRoomAsync()
        {
            if (operationRunning) return;
            operationRunning = true;
            state = MenuState.Connecting;
            status = "Connecting...";
            try
            {
                await InitializeServicesAsync();
                CreateNetworkManager();
                var allocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput.Trim().ToUpperInvariant());
                ConfigureRelay(allocation.ToRelayServerData("wss"));
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                if (!networkManager.StartClient())
                    throw new InvalidOperationException("Unity Netcode не смог запустить клиента.");

                arenaSession.BeginAsClient();
                roomCode = string.Empty;
                state = MenuState.InRoom;
                status = "Connecting...";
                Time.timeScale = 1f;
            }
            catch (Exception exception)
            {
                HandleConnectionError(exception);
            }
            finally
            {
                operationRunning = false;
            }
        }

        private static async Task InitializeServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static async Task<string> GetPreferredRelayRegionAsync()
        {
            // Relay QoS probing is unavailable in WebGL. For European browser
            // time zones, select a nearby region explicitly instead of letting
            // the service fall back to its potentially distant default.
            if (Application.platform != RuntimePlatform.WebGLPlayer) return null;
            var utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalHours;
            if (utcOffset < -1d || utcOffset > 4d) return null;

            try
            {
                var regions = await RelayService.Instance.ListRegionsAsync();
                Region europeanFallback = null;
                foreach (var region in regions)
                {
                    var searchable = $"{region.Id} {region.Description}".ToLowerInvariant();
                    if (searchable.Contains("frankfurt") || searchable.Contains("europe-central"))
                    {
                        Debug.Log($"Using nearby Relay region: {region.Description} ({region.Id})");
                        return region.Id;
                    }

                    if (europeanFallback == null
                        && (searchable.Contains("europe") || searchable.Contains("germany")))
                    {
                        europeanFallback = region;
                    }
                }

                if (europeanFallback != null)
                {
                    Debug.Log($"Using European Relay region: {europeanFallback.Description} ({europeanFallback.Id})");
                    return europeanFallback.Id;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not select a nearby Relay region: {exception.Message}");
            }
            return null;
        }

        private void CreateNetworkManager()
        {
            if (networkManager != null) return;
            var networkObject = new GameObject("Online Network Manager");
            networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                TickRate = 60,
                EnableSceneManagement = false,
                ConnectionApproval = false
            };
            arenaSession = networkObject.AddComponent<OnlineArenaSession>();
        }

        private void ConfigureRelay(RelayServerData relayData)
        {
            var transport = networkManager.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayData);
            transport.UseWebSockets = true;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null) return;
            if (networkManager.IsHost)
            {
                if (clientId != NetworkManager.ServerClientId)
                    status = "Player 2 connected - fight!";
            }
            else if (clientId == networkManager.LocalClientId)
            {
                status = "Connected as PLAYER 2";
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (networkManager == null) return;
            if (networkManager.IsHost && clientId != NetworkManager.ServerClientId)
                status = "Player 2 disconnected - waiting";
            else if (!networkManager.IsHost)
                status = "Connection to host lost";
        }

        private void StartLocalGame()
        {
            state = MenuState.Local;
            Time.timeScale = 1f;
        }

        private void HandleConnectionError(Exception exception)
        {
            Debug.LogException(exception, this);
            status = GetConnectionErrorMessage(exception);
            state = MenuState.Main;
            roomCode = string.Empty;
            Time.timeScale = 0f;
            if (networkManager == null) return;
            if (networkManager.IsListening) networkManager.Shutdown();
            Destroy(networkManager.gameObject);
            networkManager = null;
            arenaSession = null;
        }

        private static string GetConnectionErrorMessage(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current.Message.IndexOf("services couldn't be initialized", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Online mode is not configured yet. Link this project to Unity Cloud Services and enable Relay.";
                }
            }

            return $"Connection failed: {exception.Message}";
        }

        private static void EnsureStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            codeStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
