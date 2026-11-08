using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LuckyNumber.Network
{
    /// <summary>
    /// Manages Unity Gaming Services (Authentication, Lobby, Relay)
    /// and provides direct Localhost fallback for testing.
    /// </summary>
    public class NetworkLobbyManager : MonoBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private string lobbyName = "LuckyNumberRoom";
        [SerializeField] private ushort defaultLocalPort = 7777;

        public string CurrentRoomCode { get; private set; } = string.Empty;
        public string CurrentLobbyId { get; private set; } = string.Empty;
        public bool IsUsingRelay { get; private set; } = true;
        public bool IsServicesInitialized { get; private set; } = false;

        public event Action<string> OnStatusChanged;
        public event Action<string> OnRoomJoined;
        public event Action OnLeftRoom;

        private float heartbeatTimer = 0f;
        private const float HeartbeatInterval = 15f;

        private void Awake()
        {
            if (Instance != null && Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeServicesAsync();
        }

        private void Update()
        {
            HandleLobbyHeartbeat();
        }

        public async Task<bool> InitializeServicesAsync()
        {
            if (IsServicesInitialized) return true;

            try
            {
                OnStatusChanged?.Invoke("Initializing Unity Services...");
                var initOptions = new InitializationOptions();
                // Use a unique profile per local instance to allow multiple clients on the same machine
                string profile = "Player_" + System.Guid.NewGuid().ToString().Substring(0, 8);
                initOptions.SetProfile(profile);

                await UnityServices.InitializeAsync(initOptions);

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    OnStatusChanged?.Invoke("Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                IsServicesInitialized = true;
                OnStatusChanged?.Invoke($"Ready. Signed in as {AuthenticationService.Instance.PlayerId.Substring(0, 6)}...");
                Debug.Log($"[NetworkLobbyManager] Services initialized with profile {profile}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkLobbyManager] Services Init warning (Localhost mode still available): {ex.Message}");
                OnStatusChanged?.Invoke("Services offline. Localhost mode available.");
                return false;
            }
        }

        /// <summary>
        /// Creates a room using Unity Relay and Unity Lobby (Internet Multiplayer).
        /// </summary>
        public async Task<bool> CreateRelayRoomAsync(int maxPlayers)
        {
            try
            {
                if (!IsServicesInitialized)
                {
                    bool init = await InitializeServicesAsync();
                    if (!init) throw new Exception("UGS initialization failed. Check internet/dashboard.");
                }

                OnStatusChanged?.Invoke("Allocating Relay server...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                OnStatusChanged?.Invoke("Creating public Lobby...");
                var createOptions = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                };

                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);
                CurrentLobbyId = lobby.Id;
                CurrentRoomCode = lobby.LobbyCode;
                IsUsingRelay = true;

                // Configure Netcode Transport with Relay data
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                var relayServerData = new RelayServerData(allocation, "dtls");
                transport.SetRelayServerData(relayServerData);

                NetworkManager.Singleton.StartHost();

                OnStatusChanged?.Invoke($"Room Created! Code: {CurrentRoomCode}");
                OnRoomJoined?.Invoke(CurrentRoomCode);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobbyManager] Create Relay Room error: {ex.Message}");
                OnStatusChanged?.Invoke($"Failed to create room: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Joins an existing room using the 6-character Lobby code (Internet Multiplayer).
        /// </summary>
        public async Task<bool> JoinRelayRoomAsync(string roomCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode))
                {
                    OnStatusChanged?.Invoke("Please enter a room code.");
                    return false;
                }

                if (!IsServicesInitialized)
                {
                    bool init = await InitializeServicesAsync();
                    if (!init) throw new Exception("UGS initialization failed.");
                }

                OnStatusChanged?.Invoke($"Finding room {roomCode.Trim()}...");
                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(roomCode.Trim());
                CurrentLobbyId = lobby.Id;
                CurrentRoomCode = lobby.LobbyCode;
                IsUsingRelay = true;

                string relayJoinCode = lobby.Data["RelayCode"].Value;
                OnStatusChanged?.Invoke("Joining Relay allocation...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                var relayServerData = new RelayServerData(joinAllocation, "dtls");
                transport.SetRelayServerData(relayServerData);

                NetworkManager.Singleton.StartClient();

                OnStatusChanged?.Invoke($"Joined room {CurrentRoomCode}!");
                OnRoomJoined?.Invoke(CurrentRoomCode);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLobbyManager] Join Relay Room error: {ex.Message}");
                OnStatusChanged?.Invoke($"Failed to join room: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts a local host directly without cloud dependencies (for local/LAN testing).
        /// </summary>
        public void StartLocalHost(ushort port = 0)
        {
            ushort targetPort = port > 0 ? port : defaultLocalPort;
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", targetPort);

            IsUsingRelay = false;
            CurrentRoomCode = "LOCAL-" + targetPort;
            NetworkManager.Singleton.StartHost();

            OnStatusChanged?.Invoke($"Local Host started on 127.0.0.1:{targetPort}");
            OnRoomJoined?.Invoke(CurrentRoomCode);
        }

        /// <summary>
        /// Joins a local host directly without cloud dependencies (for local/LAN testing).
        /// </summary>
        public void JoinLocalClient(string ipAddress = "127.0.0.1", ushort port = 0)
        {
            ushort targetPort = port > 0 ? port : defaultLocalPort;
            string targetIp = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim();

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(targetIp, targetPort);

            IsUsingRelay = false;
            CurrentRoomCode = "LOCAL-" + targetPort;
            NetworkManager.Singleton.StartClient();

            OnStatusChanged?.Invoke($"Connecting to {targetIp}:{targetPort}...");
            OnRoomJoined?.Invoke(CurrentRoomCode);
        }

        /// <summary>
        /// Leaves current match and cleanly shuts down networking.
        /// </summary>
        public async void LeaveRoom()
        {
            try
            {
                if (IsUsingRelay && !string.IsNullOrEmpty(CurrentLobbyId))
                {
                    if (NetworkManager.Singleton.IsHost)
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(CurrentLobbyId);
                    }
                    else
                    {
                        await LobbyService.Instance.RemovePlayerAsync(CurrentLobbyId, AuthenticationService.Instance.PlayerId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkLobbyManager] Leave lobby cleanup: {ex.Message}");
            }

            CurrentLobbyId = string.Empty;
            CurrentRoomCode = string.Empty;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            OnStatusChanged?.Invoke("Disconnected.");
            OnLeftRoom?.Invoke();
        }

        private async void HandleLobbyHeartbeat()
        {
            if (IsUsingRelay && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && !string.IsNullOrEmpty(CurrentLobbyId))
            {
                heartbeatTimer += Time.deltaTime;
                if (heartbeatTimer >= HeartbeatInterval)
                {
                    heartbeatTimer = 0f;
                    try
                    {
                        await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobbyId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NetworkLobbyManager] Heartbeat ping failed: {ex.Message}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            LeaveRoom();
        }
    }
}
