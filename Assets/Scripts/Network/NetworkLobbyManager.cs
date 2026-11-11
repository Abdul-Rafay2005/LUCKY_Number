using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LuckyNumber.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace LuckyNumber.Network
{
    public enum RoomConnectionState
    {
        Idle,
        Connecting,
        InRoom,
        Leaving,
        Error
    }

    public class NetworkLobbyManager : MonoBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private string lobbyName = "LuckyNumberRoom";
        [SerializeField] private ushort defaultLocalPort = 7777;

        private const string Loopback = "127.0.0.1";
        private const float ConnectionTimeout = 12f;
        private const float HeartbeatInterval = 15f;

        private NetworkManager network;
        private TaskCompletionSource<bool> connectionResult;
        private Task<bool> servicesInitialization;
        private bool settingUpRelay;
        private bool ownsCloudLobby;
        private bool cloudMaintenanceRunning;
        private bool cloudLobbyLocked;
        private float connectionDeadline;
        private float nextHeartbeat;
        private float nextLockAttempt;

        public RoomConnectionState State { get; private set; }
        public string LastStatus { get; private set; } = "Ready.";
        public string CurrentRoomCode { get; private set; } = string.Empty;
        public string CurrentLobbyId { get; private set; } = string.Empty;
        public bool IsUsingRelay { get; private set; }
        public bool IsServicesInitialized { get; private set; }

        public event Action<RoomConnectionState> OnStateChanged;
        public event Action<string> OnStatusChanged;
        public event Action<string> OnRoomJoined;
        public event Action OnLeftRoom;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (State == RoomConnectionState.Connecting && connectionDeadline > 0f &&
                Time.unscaledTime >= connectionDeadline)
            {
                BeginDisconnect("Connection timed out. Check that the host is running and try again.", true);
            }

            if (State != RoomConnectionState.InRoom || !ownsCloudLobby || cloudMaintenanceRunning) return;
            var game = NetworkGameManager.Instance;
            bool lockDue = !cloudLobbyLocked && Time.unscaledTime >= nextLockAttempt &&
                game != null && game.IsSpawned && game.CurrentGamePhase.Value != GamePhase.Lobby;
            if (lockDue || Time.unscaledTime >= nextHeartbeat)
                MaintainCloudLobbyAsync(lockDue);
        }

        public Task<bool> InitializeServicesAsync()
        {
            if (IsServicesInitialized) return Task.FromResult(true);
            if (servicesInitialization == null || servicesInitialization.IsCompleted)
                servicesInitialization = InitializeServicesCoreAsync();
            return servicesInitialization;
        }

        private async Task<bool> InitializeServicesCoreAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetProfile($"Player_{Guid.NewGuid().ToString("N").Substring(0, 8)}");
                    await UnityServices.InitializeAsync(options);
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsServicesInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkLobbyManager] Services initialization failed: {ex.Message}");
                return false;
            }
        }

        public Task<bool> CreateRelayRoomAsync(int maxPlayers) => ConnectRelayAsync(true, string.Empty, maxPlayers);
        public Task<bool> JoinRelayRoomAsync(string roomCode) => ConnectRelayAsync(false, roomCode, 0);

        private async Task<bool> ConnectRelayAsync(bool host, string roomCode, int maxPlayers)
        {
            if (!host && string.IsNullOrWhiteSpace(roomCode))
            {
                if (CanConnect()) SetStatus("Please enter a room code.");
                return false;
            }
            if (!TryBeginConnection(true)) return false;
            var result = connectionResult;

            settingUpRelay = true;
            try
            {
                SetStatus("Connecting to Unity Services...");
                if (!await InitializeServicesAsync())
                    throw new InvalidOperationException("Unity Services are unavailable.");
                if (State != RoomConnectionState.Connecting) return false;

                var transport = network.GetComponent<UnityTransport>();
                if (host)
                {
                    SetStatus("Allocating an online room...");
                    var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                    if (State != RoomConnectionState.Connecting) return false;
                    string relayCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if (State != RoomConnectionState.Connecting) return false;

                    var options = new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
                        }
                    };
                    var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                    RememberLobby(lobby, true);
                    if (State != RoomConnectionState.Connecting) return false;
                    transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                }
                else
                {
                    SetStatus("Joining the online room...");
                    var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(roomCode.Trim());
                    RememberLobby(lobby, false);
                    if (State != RoomConnectionState.Connecting) return false;
                    if (lobby.Data == null || !lobby.Data.TryGetValue("RelayCode", out var relayCode))
                        throw new InvalidOperationException("The room has no Relay connection code.");
                    var allocation = await RelayService.Instance.JoinAllocationAsync(relayCode.Value);
                    if (State != RoomConnectionState.Connecting) return false;
                    transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                }
                StartTransport(host);
            }
            catch (Exception ex)
            {
                BeginDisconnect($"Unable to {(host ? "create" : "join")} the room. {ex.Message}", true);
            }
            finally
            {
                settingUpRelay = false;
            }
            return await result.Task;
        }

        private void RememberLobby(Lobby lobby, bool owned)
        {
            CurrentLobbyId = lobby.Id;
            CurrentRoomCode = lobby.LobbyCode;
            ownsCloudLobby = owned;
        }

        public void StartLocalHost(ushort port = 0) => ConnectLocal(true, port);

        public void JoinLocalClient(string ipAddress = Loopback, ushort port = 0)
        {
            if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Trim() != Loopback)
            {
                if (CanConnect()) SetStatus("Local testing connects to a host on this machine.");
                return;
            }
            ConnectLocal(false, port);
        }

        private void ConnectLocal(bool host, ushort port)
        {
            if (!TryBeginConnection(false)) return;
            ushort targetPort = port > 0 ? port : defaultLocalPort;
            CurrentRoomCode = $"LOCAL-{targetPort}";
            try
            {
                network.GetComponent<UnityTransport>().SetConnectionData(Loopback, targetPort, Loopback);
                StartTransport(host);
            }
            catch (Exception ex)
            {
                BeginDisconnect($"Unable to start localhost networking. {ex.Message}", true);
            }
        }

        private bool CanConnect() => State == RoomConnectionState.Idle || State == RoomConnectionState.Error;

        private bool TryBeginConnection(bool relay)
        {
            if (!CanConnect()) return false;
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.GetComponent<UnityTransport>() == null ||
                manager.IsListening || manager.ShutdownInProgress || manager.IsClient || manager.IsServer)
            {
                SetState(RoomConnectionState.Error);
                SetStatus("Networking is unavailable or still shutting down.");
                return false;
            }

            network = manager;
            IsUsingRelay = relay;
            connectionResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientConnectedCallback += HandleClientConnected;
            network.OnClientDisconnectCallback += HandleClientDisconnected;
            network.OnClientStopped += HandleClientStopped;
            SetState(RoomConnectionState.Connecting);
            return true;
        }

        private void StartTransport(bool host)
        {
            connectionDeadline = Time.unscaledTime + ConnectionTimeout;
            SetStatus(host ? "Starting host..." : "Connecting to host...");
            bool started = host ? network.StartHost() : network.StartClient();
            if (!started)
                BeginDisconnect(host ? "Could not start the host. The port may already be in use." : "Could not start the client.", true);
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var game = NetworkGameManager.Instance;
            int maxPlayers = game != null ? game.Settings.maxPlayers : 8;
            bool localHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            bool lobbyOpen = State == RoomConnectionState.InRoom && game != null && game.IsSpawned &&
                game.CurrentGamePhase.Value == GamePhase.Lobby;
            bool roomFull = network.ConnectedClientsIds.Count >= maxPlayers;
            response.Approved = localHost || (lobbyOpen && !roomFull);
            response.CreatePlayerObject = response.Approved;
            if (!response.Approved)
                response.Reason = roomFull ? "The room is full." : "The match has already started.";
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId != network.LocalClientId)
            {
                var game = NetworkGameManager.Instance;
                if (network.IsHost && game != null && game.IsSpawned &&
                    game.CurrentGamePhase.Value != GamePhase.Lobby && !game.IsInCurrentMatch(clientId))
                {
                    network.DisconnectClient(clientId, "The match has already started.");
                }
                return;
            }
            if (State != RoomConnectionState.Connecting) return;

            connectionDeadline = 0f;
            nextHeartbeat = Time.unscaledTime + HeartbeatInterval;
            SetState(RoomConnectionState.InRoom);
            SetStatus(IsUsingRelay ? $"Connected. Room code: {CurrentRoomCode}" : "Connected on localhost.");
            connectionResult.TrySetResult(true);
            OnRoomJoined?.Invoke(CurrentRoomCode);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == network.LocalClientId) ReportConnectionLost();
        }

        private void HandleClientStopped(bool wasHost) => ReportConnectionLost();

        private void ReportConnectionLost()
        {
            if (State != RoomConnectionState.Connecting && State != RoomConnectionState.InRoom) return;
            string reason = network.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
                reason = State == RoomConnectionState.InRoom ? "The connection to the host was lost." : "Could not connect to the host.";
            BeginDisconnect(reason, true);
        }

        public void LeaveRoom() => BeginDisconnect("Disconnected.", false);

        private async void BeginDisconnect(string finalStatus, bool error)
        {
            if (CanConnect() || State == RoomConnectionState.Leaving) return;
            SetState(RoomConnectionState.Leaving);
            SetStatus("Disconnecting...");
            connectionDeadline = 0f;
            var stoppingNetwork = network;
            if (stoppingNetwork != null && stoppingNetwork.IsListening && !stoppingNetwork.ShutdownInProgress)
                stoppingNetwork.Shutdown(error);

            // A stop callback can run before NGO has cleared its roles or disposed its transport.
            do
            {
                await Task.Yield();
            }
            while (settingUpRelay || cloudMaintenanceRunning ||
                (stoppingNetwork != null && (stoppingNetwork.IsListening || stoppingNetwork.IsClient ||
                    stoppingNetwork.IsServer || stoppingNetwork.ShutdownInProgress)));

            if (!string.IsNullOrEmpty(CurrentLobbyId))
            {
                try
                {
                    if (ownsCloudLobby)
                        await LobbyService.Instance.DeleteLobbyAsync(CurrentLobbyId);
                    else
                        await LobbyService.Instance.RemovePlayerAsync(CurrentLobbyId, AuthenticationService.Instance.PlayerId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkLobbyManager] Lobby cleanup failed: {ex.Message}");
                }
            }

            if (stoppingNetwork != null)
            {
                stoppingNetwork.OnClientConnectedCallback -= HandleClientConnected;
                stoppingNetwork.OnClientDisconnectCallback -= HandleClientDisconnected;
                stoppingNetwork.OnClientStopped -= HandleClientStopped;
                stoppingNetwork.ConnectionApprovalCallback = null;
            }
            network = null;
            LuckyNetworkPlayer.ClearSpawnedPlayers();
            CurrentLobbyId = string.Empty;
            CurrentRoomCode = string.Empty;
            IsUsingRelay = false;
            ownsCloudLobby = false;
            cloudLobbyLocked = false;
            nextLockAttempt = 0f;
            connectionResult.TrySetResult(false);
            connectionResult = null;
            SetState(error ? RoomConnectionState.Error : RoomConnectionState.Idle);
            SetStatus(finalStatus);
            OnLeftRoom?.Invoke();
        }

        private async void MaintainCloudLobbyAsync(bool lockLobby)
        {
            cloudMaintenanceRunning = true;
            try
            {
                if (lockLobby)
                {
                    nextLockAttempt = Time.unscaledTime + HeartbeatInterval;
                    await LobbyService.Instance.UpdateLobbyAsync(CurrentLobbyId, new UpdateLobbyOptions { IsLocked = true });
                    cloudLobbyLocked = true;
                }
                else
                {
                    nextHeartbeat = Time.unscaledTime + HeartbeatInterval;
                    await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobbyId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkLobbyManager] Lobby maintenance failed: {ex.Message}");
            }
            finally
            {
                cloudMaintenanceRunning = false;
            }
        }

        private void SetState(RoomConnectionState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        private void SetStatus(string status)
        {
            LastStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            LeaveRoom();
        }
    }
}
