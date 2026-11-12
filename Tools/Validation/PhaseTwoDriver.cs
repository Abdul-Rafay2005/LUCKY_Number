using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LuckyNumber.Network;
using LuckyNumber.UI;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PhaseTwoDriver : MonoBehaviour
{
    [Serializable]
    private class Snapshot
    {
        public bool listening;
        public bool connected;
        public bool menuVisible;
        public bool lobbyVisible;
        public string localId;
        public string phase;
        public string winner;
        public string reason;
        public string activeId;
        public string stamp;
        public uint match;
        public uint action;
        public int score;
        public int moves;
        public int target;
        public int round;
        public int joined;
        public int left;
        public string status;
        public string roomCode;
        public int runoff;
        public string outcome;
        public string ejectedRole;
        public bool revealVisible;
        public string revealText;
        public string[] publicActors;
        public string historyText;
        public bool roleVisible;
        public string roleText;
        public string roleObjective;
        public bool diceVisible;
        public bool abilityVisible;
        public string[] targetOptions;
        public string[] shiftOptions;
        public int[] lobbyCards;
        public Player[] players;
        public Ack[] acks;
    }

    [Serializable]
    private class Player
    {
        public string id;
        public string name;
        public bool alive;
        public bool owner;
        public string role;
        public int skips;
        public int block;
        public int curse;
        public int shift;
        public int teammates;
        public int die;
        public int diceCount;
        public bool ready;
        public bool voted;
        public string rollStamp;
        public bool receivedSecret;
        public bool receivedRoll;
    }

    [Serializable]
    private class Ack
    {
        public string stamp;
        public string kind;
        public bool accepted;
        public string message;
    }

    private readonly Dictionary<string, ActionStamp> stamps = new();
    private readonly List<Ack> acknowledgments = new();
    private NetworkGameManager game;
    private string mailbox;
    private string peer;
    private int joined;
    private int left;
    private string status;
    private bool quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-phase2Driver");
        if (index < 0)
            return;

        var lobby = NetworkLobbyManager.Instance;
        if (!args.Contains("-phase2Relay"))
            typeof(NetworkLobbyManager).GetProperty("IsServicesInitialized")?.GetSetMethod(true)?.Invoke(lobby, new object[] { true });
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
        var driver = new GameObject("Isolated Phase 2 integration driver").AddComponent<PhaseTwoDriver>();
        driver.mailbox = args[index + 1];
        driver.peer = args[index + 2];
        lobby.OnRoomJoined += _ => driver.joined++;
        lobby.OnLeftRoom += () => driver.left++;
        lobby.OnStatusChanged += value => driver.status = value;
        driver.Write("ready", "ready");
    }

    private void Update()
    {
        if (NetworkGameManager.Instance != game)
        {
            if (game != null)
                game.OnActionFeedback -= ReceiveAck;
            game = NetworkGameManager.Instance;
            if (game != null)
                game.OnActionFeedback += ReceiveAck;
        }

        string request = Path.Combine(mailbox, peer + ".request");
        if (!File.Exists(request))
            return;

        string[] command;
        try
        {
            command = File.ReadAllText(request).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            File.Delete(request);
        }
        catch (IOException)
        {
            return;
        }
        try
        {
            string response = Execute(command.Skip(1).ToArray());
            Write(command[0] + ".response", response);
        }
        catch (Exception error)
        {
            Write(command[0] + ".response", "ERROR: " + error);
        }

        if (quitting)
            Application.Quit(0);
    }

    private string Execute(string[] command)
    {
        var lobby = NetworkLobbyManager.Instance;
        if (command[0] == "host")
        {
            lobby.StartLocalHost(ushort.Parse(command[1]));
            return "OK";
        }
        if (command[0] == "join")
        {
            lobby.JoinLocalClient("127.0.0.1", ushort.Parse(command[1]));
            return "OK";
        }
        if (command[0] == "leave")
        {
            lobby.LeaveRoom();
            return "OK";
        }
        if (command[0] == "quit")
        {
            quitting = true;
            return "OK";
        }
        if (command[0] == "state")
            return JsonUtility.ToJson(ReadSnapshot());
        if (command[0] == "relay-host")
        {
            Click<MainMenuController>("createOnlineRoomButton");
            return "OK";
        }
        if (command[0] == "relay-join")
        {
            ReadField<MainMenuController, TMP_InputField>("roomCodeInput").text = command[1];
            Click<MainMenuController>("joinOnlineRoomButton");
            return "OK";
        }
        if (command[0] == "capture")
        {
            CaptureFrame(command[1]);
            return "OK";
        }
        if (command[0] == "ui-menu-qa")
        {
            CheckMenuPresentation();
            return "OK";
        }
        if (command[0] == "duplicate-lobby")
        {
            new GameObject("Test duplicate lobby manager").AddComponent<NetworkLobbyManager>();
            return "OK";
        }
        if (game == null || !game.IsSpawned)
            throw new InvalidOperationException("Game manager has not spawned.");

        var stamp = game.ActionContext.Value;
        if (command.Length > 1 && command[^1].StartsWith("@", StringComparison.Ordinal))
            stamp = stamps[command[^1][1..]];

        switch (command[0])
        {
            case "cache":
                stamps[command[1]] = stamp;
                break;
            case "profile":
                LuckyNetworkPlayer.LocalPlayer.SetProfileServerRpc(
                    new FixedString64Bytes(command.Length > 1 ? command[1] : peer), 0);
                break;
            case "fill-history":
                var record = typeof(NetworkGameManager).GetMethod("RecordPublicEvent",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 35; i++)
                    record.Invoke(game, new object[] { PublicEventKind.NumberMove, NetworkManager.Singleton.LocalClientId, i % 6 + 1 });
                break;
            case "ui-ready":
                Click<LobbyController>("readyButton");
                break;
            case "ui-move":
                var dice = ReadField<GameplayController, Button[]>("diceButtons");
                Click(dice[0]);
                if (command.Length > 1 && command[1] == "-")
                    Click<GameplayController>("subtractOperationButton");
                Click<GameplayController>("commitMoveButton");
                break;
            case "ui-shift":
                var deltas = ReadField<GameplayController, TMP_Dropdown>("luckyShiftDropdown");
                deltas.value = deltas.options.FindIndex(option => option.text == command[1]);
                Click<GameplayController>("luckyShiftButton");
                break;
            case "ui-block":
                Click<GameplayController>("forcedBlockButton");
                break;
            case "ui-curse":
                Click<GameplayController>("cursedDiceButton");
                break;
            case "ui-skip":
                Click<GameplayController>("voluntarySkipButton");
                break;
            case "ui-vote-skip":
                Click<DiscussionAndVotingController>("skipVoteButton");
                break;
            case "ui-vote":
                var target = game.GetPlayer(ulong.Parse(command[1]));
                string playerName = PlayerNameLabel.Compact(target.PlayerName.Value.ToString());
                var candidates = ReadField<DiscussionAndVotingController, Transform>("votingCandidatesContainer");
                var candidate = candidates.Cast<Transform>().First(card => card.gameObject.activeInHierarchy &&
                    card.GetComponentInChildren<TMP_Text>().text == playerName);
                Click(candidate.GetComponentInChildren<Button>());
                break;
            case "ui-rematch":
                Click<ResultsController>("playAgainButton");
                break;
            case "ui-return":
                Click<ResultsController>("returnToMenuButton");
                break;
            case "ui-reactivate-gameplay":
                var panel = ReadField<LuckyUIManager, GameObject>("gameplayPanel");
                panel.SetActive(false);
                panel.SetActive(true);
                break;
            case "start":
                game.RequestStartMatchServerRpc(stamp);
                break;
            case "rematch":
                game.RequestRematchServerRpc(stamp);
                break;
            case "move":
                game.SubmitMoveServerRpc(int.Parse(command[1]), true, stamp);
                break;
            case "move-twice":
                int die = int.Parse(command[1]);
                game.SubmitMoveServerRpc(die, true, stamp);
                game.SubmitMoveServerRpc(die, true, stamp);
                break;
            case "skip":
                game.RequestVoluntarySkipServerRpc(stamp);
                break;
            case "block":
                game.ActivateForcedBlockServerRpc(ulong.Parse(command[1]), stamp);
                break;
            case "curse":
                game.ActivateCursedDiceServerRpc(ulong.Parse(command[1]), stamp);
                break;
            case "shift":
                game.ActivateLuckyShiftServerRpc(int.Parse(command[1]), stamp);
                break;
            case "vote":
                game.SubmitVoteServerRpc(ulong.Parse(command[1]), stamp);
                break;
            case "vote-skip":
                game.SkipVoteServerRpc(stamp);
                break;
            case "begin-vote":
                if (!game.IsHost) throw new InvalidOperationException("Host fixture only.");
                typeof(NetworkGameManager).GetMethod("ClearActiveTurn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
                typeof(NetworkGameManager).GetMethod("StartVotingPhase", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
                break;
            case "finish-turn":
                if (!game.IsHost) throw new InvalidOperationException("Host fixture only.");
                typeof(NetworkGameManager).GetMethod("ClearActiveTurn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
                typeof(NetworkGameManager).GetMethod("StartNextTurn", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
                break;
            case "advance-timer":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can adjust timers.");
                game.PhaseTimer.Value = 0.02f;
                break;
            case "seed":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can seed the scenario.");
                UnityEngine.Random.InitState(int.Parse(command[1]));
                break;
            case "seed-host-imposter":
            case "seed-remote-imposter":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can seed the scenario.");
                var clients = NetworkManager.Singleton.ConnectedClientsIds;
                for (int seed = 1; seed <= 100; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    UnityEngine.Random.Range(game.Settings.minLuckyTarget, game.Settings.maxLuckyTarget + 1);
                    foreach (ulong _ in clients)
                    {
                        var unused = UnityEngine.Random.value;
                    }
                    bool hostIsImposter = clients.OrderBy(_ => UnityEngine.Random.value)
                        .Take(game.Settings.GetImposterCount(clients.Count)).Contains(NetworkManager.ServerClientId);
                    if (hostIsImposter != (command[0] == "seed-host-imposter")) continue;
                    UnityEngine.Random.InitState(seed);
                    return "OK";
                }
                throw new InvalidOperationException("Could not seed the requested imposter configuration.");
            case "schedule":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can inspect the scheduled queue.");
                var schedule = (IEnumerable<ulong>)typeof(NetworkGameManager).GetField("turnOrder", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                return string.Join(",", schedule);
            case "capacity":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can adjust capacity.");
                game.Settings.maxPlayers = int.Parse(command[1]);
                break;
            case "lengthen-turns":
                if (!game.IsHost)
                    throw new InvalidOperationException("Only the host fixture can adjust timers.");
                game.Settings.turnDurationSeconds = 180f;
                game.Settings.discussionDurationSeconds = 180f;
                game.Settings.votingDurationSeconds = 180f;
                break;
            default:
                throw new InvalidOperationException("Unknown command " + command[0]);
        }
        return "OK";
    }

    private void ReceiveAck(ActionStamp stamp, MatchAction action, bool accepted, string message)
    {
        acknowledgments.Add(new Ack { stamp = Stamp(stamp), kind = action.ToString(), accepted = accepted, message = message });
    }

    private Snapshot ReadSnapshot()
    {
        var net = NetworkManager.Singleton;
        var current = game != null && game.IsSpawned ? game : null;
        return new Snapshot
        {
            listening = net != null && net.IsListening,
            connected = net != null && net.IsConnectedClient,
            menuVisible = FindFirstObjectByType<MainMenuController>() != null,
            lobbyVisible = FindFirstObjectByType<LobbyController>() != null,
            localId = net == null ? "" : net.LocalClientId.ToString(),
            phase = current == null ? "" : current.CurrentGamePhase.Value.ToString(),
            winner = current == null ? "" : current.WinningTeam.Value.ToString(),
            reason = current == null ? "" : current.MatchWinReason.Value.ToString(),
            activeId = current == null ? "" : current.ActivePlayerClientId.Value.ToString(),
            stamp = current == null ? "" : Stamp(current.ActionContext.Value),
            match = current == null ? 0 : current.ActionContext.Value.MatchId,
            action = current == null ? 0 : current.ActionContext.Value.ActionId,
            score = current == null ? 0 : current.CurrentScore.Value,
            moves = current == null ? 0 : current.RemainingMoves.Value,
            target = current == null ? 0 : current.TargetLuckyNumber.Value,
            round = current == null ? 0 : current.CurrentRound.Value,
            joined = joined,
            left = left,
            status = status,
            roomCode = NetworkLobbyManager.Instance?.CurrentRoomCode,
            runoff = current == null ? 0 : current.RunoffBallotsPlayed,
            outcome = current == null ? "" : current.LastVoteOutcome.Value.Kind.ToString(),
            ejectedRole = current == null ? "" : current.LastVoteOutcome.Value.PlayerRole.ToString(),
            revealVisible = ReadRevealObject()?.activeInHierarchy == true,
            revealText = ReadText<DiscussionAndVotingController>("revealedRoleText"),
            publicActors = ReadPublicActors(current),
            historyText = ReadText<GameplayController>("moveHistoryText"),
            roleVisible = IsVisible(ReadField<LuckyUIManager, GameObject>("roleRevealPanel")),
            roleText = ReadText<LuckyUIManager>("roleNameText"),
            roleObjective = ReadText<LuckyUIManager>("roleObjectiveText"),
            diceVisible = IsVisible(ReadField<GameplayController, GameObject>("dicePanel")),
            abilityVisible = IsVisible(ReadField<GameplayController, GameObject>("imposterDrawer")),
            targetOptions = ReadOptions("abilityTargetDropdown"),
            shiftOptions = ReadOptions("luckyShiftDropdown"),
            lobbyCards = ReadField<LobbyController, Transform>("playerListContainer")?.Cast<Transform>()
                .Where(card => card.gameObject.activeInHierarchy).Select(card => card.GetInstanceID()).ToArray() ?? Array.Empty<int>(),
            acks = acknowledgments.ToArray(),
            players = LuckyNetworkPlayer.SpawnedPlayers.OrderBy(entry => entry.Key).Select(entry =>
            {
                var player = entry.Value;
                var roll = player.CurrentRoll;
                return new Player
                {
                    id = entry.Key.ToString(),
                    name = player.PlayerName.Value.ToString(),
                    alive = player.IsAlive.Value,
                    owner = player.IsOwner,
                    role = player.HasSecretRole ? player.LocalRole.ToString() : "",
                    skips = player.RemainingSkips.Value,
                    block = player.ForcedBlockCharges,
                    curse = player.CursedDiceCharges,
                    shift = player.LuckyShiftCharges,
                    teammates = player.KnownImposters.Length,
                    die = roll.Die1,
                    diceCount = new[] { roll.Die1, roll.Die2, roll.Die3 }.Count(die => die >= 1 && die <= 6),
                    ready = player.IsReady.Value,
                    voted = player.HasVoted.Value,
                    rollStamp = roll.HasDice ? Stamp(roll.Stamp) : "",
                    receivedSecret = ReadVariable<PlayerSecret>(player, "secret").HasRole,
                    receivedRoll = ReadVariable<PrivateRoll>(player, "roll").HasDice
                };
            }).ToArray()
        };
    }

    private static string[] ReadPublicActors(NetworkGameManager current)
    {
        if (current == null) return Array.Empty<string>();
        var actors = new List<string>();
        foreach (var e in current.PublicHistory) actors.Add($"{e.Kind}|{e.ActorClientId}|{e.ActorName}");
        return actors.ToArray();
    }
    private static GameObject ReadRevealObject()
    {
        var ui = FindFirstObjectByType<DiscussionAndVotingController>(FindObjectsInactive.Include);
        return ui == null ? null : (GameObject)typeof(DiscussionAndVotingController).GetField("eliminationModalPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
    }
    private static string ReadText<TController>(string name) where TController : MonoBehaviour =>
        ReadField<TController, TMP_Text>(name)?.text ?? "";

    private static bool IsVisible(GameObject view) => view != null && view.activeInHierarchy;

    private static string[] ReadOptions(string field) =>
        ReadField<GameplayController, TMP_Dropdown>(field)?.options.Select(option => option.text).ToArray()
        ?? Array.Empty<string>();

    private static TField ReadField<TController, TField>(string name)
        where TController : MonoBehaviour where TField : class
    {
        var ui = FindFirstObjectByType<TController>(FindObjectsInactive.Include);
        return ui == null ? null : (TField)typeof(TController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
    }

    private static void Click<TController>(string name) where TController : MonoBehaviour =>
        Click(ReadField<TController, Button>(name));

    private static void Click(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.interactable)
            throw new InvalidOperationException($"Button {button?.name} is unavailable.");
        button.onClick.Invoke();
    }

    private void CheckMenuPresentation()
    {
        var manager = LuckyUIManager.Instance;
        var menu = ReadField<LuckyUIManager, GameObject>("mainMenuPanel");
        var menuGroup = menu.GetComponent<CanvasGroup>();
        var help = ReadField<LuckyUIManager, GameObject>("howToPlayModal");
        var settings = ReadField<LuckyUIManager, GameObject>("settingsModal");
        var helpButton = ReadField<MainMenuController, Button>("howToPlayButton");
        var eventSystem = EventSystem.current;
        if (manager.transform.Find("Background").GetComponent<Image>().raycastTarget)
            throw new InvalidOperationException("Decorative background intercepts input.");
        CaptureFrame("menu");
        eventSystem.SetSelectedGameObject(helpButton.gameObject);
        Click(helpButton);
        if (!help.activeInHierarchy || menuGroup.interactable ||
            !eventSystem.currentSelectedGameObject.transform.IsChildOf(help.transform))
            throw new InvalidOperationException("Help did not contain keyboard focus.");
        var blocker = help.transform.Find("ModalBackdrop").GetComponent<Image>();
        Canvas.ForceUpdateCanvases();
        var results = new List<RaycastResult>();
        var location = new Vector2(Screen.width * .38f, Screen.height * .08f);
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = location }, results);
        if (!blocker.raycastTarget || menuGroup.blocksRaycasts ||
            results.Any(result => result.gameObject.transform.IsChildOf(menu.transform)))
            throw new InvalidOperationException("Help failed to block the underlying menu.");
        CaptureFrame("help");
        Click<HowToPlayModal>("closeButton");
        if (!menuGroup.interactable || eventSystem.currentSelectedGameObject != helpButton.gameObject)
            throw new InvalidOperationException("Menu focus did not return after closing Help.");
        Click<MainMenuController>("settingsButton");
        var slider = ReadField<SettingsModal, Slider>("masterVolumeSlider");
        if (slider.handleRect == null || !settings.activeInHierarchy)
            throw new InvalidOperationException("Settings volume slider is not wired.");
        float volume = AudioListener.volume;
        float effectsVolume = LuckyNumber.Audio.AudioManager.EffectsVolume;
        slider.value = .42f;
        ReadField<SettingsModal, Slider>("effectsVolumeSlider").value = .65f;
        if (!Mathf.Approximately(AudioListener.volume, .42f) ||
            !Mathf.Approximately(LuckyNumber.Audio.AudioManager.EffectsVolume, .65f))
            throw new InvalidOperationException("Audio sliders did not preview.");
        CaptureFrame("settings-audio");
        var tabs = ReadField<SettingsModal, Button[]>("tabs");
        Click(tabs[1]);
        var mode = ReadField<SettingsModal, TMP_Dropdown>("displayMode");
        var resolution = ReadField<SettingsModal, TMP_Dropdown>("resolution");
        if (mode.options.Count != 3 || resolution.options.Count == 0)
            throw new InvalidOperationException("Display modes or resolutions are missing.");
        mode.value = 1;
        if (resolution.interactable || resolution.options.Count != 1)
            throw new InvalidOperationException("Borderless did not lock to the desktop resolution.");
        CaptureFrame("settings-display");
        if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
        {
            Click<SettingsModal>("closeButton");
            var confirm = ReadField<SettingsModal, GameObject>("confirmation");
            if (!confirm.activeInHierarchy)
                throw new InvalidOperationException("Display changes skipped confirmation.");
            CaptureFrame("settings-confirmation");
            Click<SettingsModal>("revertDisplay");
            if (confirm.activeSelf || !settings.activeInHierarchy)
                throw new InvalidOperationException("Display revert did not return to settings.");
        }

        Click(tabs[2]);
        var bindings = ReadField<SettingsModal, Button[]>("bindingButtons");
        var binding = bindings[0];
        var input = eventSystem.GetComponent<InputSystemUIInputModule>();
        var navigation = input.move.action;
        var submit = input.submit.action;
        int up = Enumerable.Range(0, navigation.bindings.Count)
            .First(index => navigation.bindings[index].path == "<Keyboard>/upArrow");
        string originalPath = navigation.bindings[up].effectivePath;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            Click(binding);
            PressTestKey(keyboard, Key.S);
            if (navigation.bindings[up].effectivePath != originalPath || !navigation.enabled)
                throw new InvalidOperationException("Conflicting keyboard binding was accepted.");
            Click(binding);
            PressTestKey(keyboard, Key.K);
            if (navigation.bindings[up].effectivePath != "<Keyboard>/k" || !navigation.enabled)
                throw new InvalidOperationException("The UI action did not accept the keyboard rebind.");
            Click(bindings[4]);
            PressTestKey(keyboard, Key.J);
            if (!submit.enabled || !submit.bindings.Any(b => b.effectivePath == "<Keyboard>/j"))
                throw new InvalidOperationException("Keyboard activation could not be rebound.");
            CaptureFrame("settings-controls");
            Click<SettingsModal>("resetBindings");
            if (navigation.bindings[up].effectivePath != navigation.bindings[up].path)
                throw new InvalidOperationException("Reset did not restore the default binding.");
            Click(binding);
            PressTestKey(keyboard, Key.L);
            if (navigation.bindings[up].effectivePath != "<Keyboard>/l")
                throw new InvalidOperationException("Could not stage a keyboard rebind before cancelling.");
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        manager.ToggleSettings(false);
        if (!Mathf.Approximately(AudioListener.volume, volume) ||
            !Mathf.Approximately(LuckyNumber.Audio.AudioManager.EffectsVolume, effectsVolume) ||
            navigation.bindings[up].effectivePath != originalPath)
            throw new InvalidOperationException("Closing without Save retained unsaved changes.");
        // Feed the production controller the same connection-error status it uses for a failed join.
        typeof(MainMenuController).GetMethod("UpdateStatus", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(menu.GetComponent<MainMenuController>(), new object[] { "Could not join the room. Verify the room code and check your connection, then try again." });
        CaptureFrame("menu-error");
    }

    private static void PressTestKey(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
    }

    private void CaptureFrame(string name)
    {
        var camera = Camera.main;
        var canvas = LuckyUIManager.Instance.GetComponent<Canvas>();
        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var frame = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
        var screenshot = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = frame;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = frame;
            screenshot.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(Path.Combine(mailbox, $"{peer}-{name}.png"), screenshot.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(frame);
            Destroy(screenshot);
        }
    }
    private static T ReadVariable<T>(LuckyNetworkPlayer player, string name)
    {
        var field = typeof(LuckyNetworkPlayer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return ((NetworkVariable<T>)field.GetValue(player)).Value;
    }

    private void Write(string suffix, string contents)
    {
        string path = Path.Combine(mailbox, peer + "." + suffix);
        File.WriteAllText(path + ".tmp", contents);
        File.Move(path + ".tmp", path);
    }

    private static string Stamp(ActionStamp stamp) => $"{stamp.SessionId}/{stamp.MatchId}/{stamp.ActionId}";
}
