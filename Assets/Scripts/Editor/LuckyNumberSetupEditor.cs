using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using LuckyNumber.Core;
using LuckyNumber.Network;
using LuckyNumber.Audio;
using LuckyNumber.UI;

namespace LuckyNumber.Editor
{
    public static partial class LuckyNumberSetupEditor
    {
        [MenuItem("Lucky Number/Development/Generate Missing Game Setup", false, 20)]
        public static void GenerateCompleteGameSetup()
        {
            if (File.Exists(MasterScenePath))
            {
                Debug.LogWarning("The authored master scene already exists. No setup assets were generated.");
                return;
            }

            Debug.Log("[LuckyNumberSetupEditor] Beginning automated project setup...");

            EnsureDirectories();
            var settings = CreateOrLoadGameSettings();
            var playerPrefab = CreatePlayerPrefab();
            var playerCardPrefab = CreatePlayerCardPrefab();
            BuildMasterScene(settings, playerPrefab, playerCardPrefab);
            BuildIndividualScenes();
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LuckyNumberSetupEditor] Complete Game Setup successfully built!");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/Scenes",
                "Assets/Prefabs",
                "Assets/ScriptableObjects",
                "Assets/Scripts/Editor"
            };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
        }

        private static GameSettings CreateOrLoadGameSettings()
        {
            string path = "Assets/ScriptableObjects/DefaultGameSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            return settings;
        }

        private static GameObject CreatePlayerPrefab()
        {
            string path = "Assets/Prefabs/LuckyNetworkPlayer.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("LuckyNetworkPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<LuckyNetworkPlayer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            GameObject.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreatePlayerCardPrefab()
        {
            string path = "Assets/Prefabs/PlayerCardPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var card = new GameObject("PlayerCard", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 100);

            var bgImg = card.GetComponent<Image>();
            bgImg.color = new Color(0.15f, 0.22f, 0.28f, 0.95f);

            // Portrait Image
            var portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObj.transform.SetParent(card.transform, false);
            var pRect = portraitObj.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0, 0.5f);
            pRect.anchorMax = new Vector2(0, 0.5f);
            pRect.pivot = new Vector2(0, 0.5f);
            pRect.anchoredPosition = new Vector2(10, 0);
            pRect.sizeDelta = new Vector2(80, 80);

            // Name Text
            var nameObj = new GameObject("PlayerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(card.transform, false);
            var nRect = nameObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 0.5f);
            nRect.anchorMax = new Vector2(1, 1);
            nRect.offsetMin = new Vector2(100, 10);
            nRect.offsetMax = new Vector2(-10, -10);
            var nTmp = nameObj.GetComponent<TextMeshProUGUI>();
            nTmp.text = "Player Name";
            nTmp.fontSize = 20;
            nTmp.enableAutoSizing = true;
            nTmp.fontSizeMin = 16;
            nTmp.fontSizeMax = 20;
            nTmp.textWrappingMode = TextWrappingModes.NoWrap;
            nTmp.overflowMode = TextOverflowModes.Ellipsis;
            nTmp.richText = false;
            nTmp.color = Color.white;
            nTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Status Text
            var statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(card.transform, false);
            var sRect = statusObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 0.5f);
            sRect.offsetMin = new Vector2(100, 10);
            sRect.offsetMax = new Vector2(-10, -10);
            var sTmp = statusObj.GetComponent<TextMeshProUGUI>();
            sTmp.text = "READY";
            sTmp.fontSize = 16;
            sTmp.color = Color.green;
            sTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var prefab = PrefabUtility.SaveAsPrefabAsset(card, path);
            GameObject.DestroyImmediate(card);
            return prefab;
        }

        private static void BuildMasterScene(GameSettings settings, GameObject playerPrefab, GameObject playerCardPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.11f, 0.15f);

            // 2. Network & Core Managers
            // NetworkManager + non-networked helpers on one GameObject (no NetworkObject here)
            var managersObj = new GameObject("NetworkManagers");
            var netManager = managersObj.AddComponent<NetworkManager>();
            var transport = managersObj.AddComponent<UnityTransport>();
            managersObj.AddComponent<NetworkLobbyManager>();
            managersObj.AddComponent<AudioManager>();

            // Configure NetManager
            netManager.NetworkConfig.NetworkTransport = transport;
            netManager.NetworkConfig.PlayerPrefab = playerPrefab;

            // NetworkGameManager MUST be on its OWN GameObject with a NetworkObject component.
            // In-scene NetworkObjects are automatically spawned by NGO when the host starts.
            // Without NetworkObject, RPCs are silently dropped and NetworkVariables never sync.
            var gameManagerObj = new GameObject("NetworkGameManager");
            gameManagerObj.AddComponent<NetworkObject>();
            var gameManager = gameManagerObj.AddComponent<NetworkGameManager>();

            // Assign settings
            var so = new SerializedObject(gameManager);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            // 3. UI Canvas
            var canvasObj = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var uiManager = canvasObj.AddComponent<LuckyUIManager>();

            // Background
            var bgObj = CreateUIElement("Background", canvasObj.transform);
            StretchFull(bgObj.GetComponent<RectTransform>());
            var bgImg = bgObj.AddComponent<Image>();
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/GameLoungeBackground.jpeg");
            if (bgSprite != null) bgImg.sprite = bgSprite;
            bgImg.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            // Sliced Sprites
            Sprite[] portraits = new Sprite[6];
            for (int i = 0; i < 6; i++) portraits[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Characters/Portraits/Portrait_{i}.png");

            Sprite[] diceFaces = new Sprite[6];
            for (int i = 0; i < 6; i++) diceFaces[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Dice/Slices/DieFace_{i+1}.png");

            Sprite cursedDice = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Dice/Slices/Die_Cursed.png");
            Sprite maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/Icon_ImposterMask.png");
            Sprite aliveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/Icon_Alive.png");

            // Panels
            var mainMenuPanel = CreateMainMenuPanel(canvasObj.transform, portraits);
            var lobbyPanel = CreateLobbyPanel(canvasObj.transform, portraits, playerCardPrefab);
            var gameplayPanel = CreateGameplayPanel(canvasObj.transform, diceFaces, cursedDice);
            var discussionAndVotingObj = CreateDiscussionAndVotingPanel(canvasObj.transform, portraits, playerCardPrefab, maskSprite, aliveSprite);
            var resultsPanel = CreateResultsPanel(canvasObj.transform);
            var howToPlayModal = CreateHowToPlayModal(canvasObj.transform);
            var settingsModal = CreateSettingsModal(canvasObj.transform);

            // Hook into LuckyUIManager
            var uiSo = new SerializedObject(uiManager);
            uiSo.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuPanel;
            uiSo.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanel;
            uiSo.FindProperty("gameplayPanel").objectReferenceValue = gameplayPanel;
            uiSo.FindProperty("discussionPanel").objectReferenceValue = discussionAndVotingObj.transform.Find("DiscussionView").gameObject;
            uiSo.FindProperty("votingPanel").objectReferenceValue = discussionAndVotingObj.transform.Find("VotingView").gameObject;
            uiSo.FindProperty("eliminationModal").objectReferenceValue = discussionAndVotingObj.transform.Find("EliminationRevealModal").gameObject;
            uiSo.FindProperty("resultsPanel").objectReferenceValue = resultsPanel;
            uiSo.FindProperty("howToPlayModal").objectReferenceValue = howToPlayModal;
            uiSo.FindProperty("settingsModal").objectReferenceValue = settingsModal;
            uiSo.ApplyModifiedProperties();
            WireMatchControls(uiManager);
            ApplyPresentation(uiManager);

            // Save Scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/LuckyNumberMaster.unity");
        }

        [MenuItem("Lucky Number/Development/Update Master Scene Match Controls", false, 21)]
        public static void UpdateMasterSceneMatchControls()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/LuckyNumberMaster.unity");
            var manager = UnityEngine.Object.FindFirstObjectByType<LuckyUIManager>();
            WireMatchControls(manager);
            ApplyPresentation(manager);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireMatchControls(LuckyUIManager manager)
        {
            var ui = new SerializedObject(manager);
            if (ui.FindProperty("roleRevealPanel").objectReferenceValue == null)
            {
                var role = CreateCardBox("RoleRevealPanel", manager.transform, new Vector2(0.5f, 0.5f), new Vector2(760, 360));
                CreateText("Heading", role.transform, "YOUR PRIVATE ROLE", 24, Color.white, new Vector2(0.5f, 0.85f), new Vector2(620, 40));
                ui.FindProperty("roleRevealPanel").objectReferenceValue = role;
                ui.FindProperty("roleNameText").objectReferenceValue = CreateText("Role", role.transform, "", 54, Color.yellow, new Vector2(0.5f, 0.64f), new Vector2(660, 72));
                ui.FindProperty("roleObjectiveText").objectReferenceValue = CreateText("Objective", role.transform, "", 28, Color.white, new Vector2(0.5f, 0.39f), new Vector2(620, 116));
                ui.FindProperty("roleTimerText").objectReferenceValue = CreateText("Countdown", role.transform, "", 28, Color.cyan, new Vector2(0.5f, 0.13f), new Vector2(180, 42));
                role.SetActive(false);
                ui.ApplyModifiedProperties();
            }
            var roleObjective = (TMP_Text)ui.FindProperty("roleObjectiveText").objectReferenceValue;
            roleObjective.rectTransform.anchorMin = roleObjective.rectTransform.anchorMax = new Vector2(0.5f, 0.39f);
            roleObjective.rectTransform.sizeDelta = new Vector2(620, 116);

            var gameplay = (GameObject)ui.FindProperty("gameplayPanel").objectReferenceValue;
            var controls = new SerializedObject(gameplay.GetComponent<GameplayController>());
            var drawer = (GameObject)controls.FindProperty("imposterDrawer").objectReferenceValue;
            controls.FindProperty("abilityStatusText").objectReferenceValue = drawer.transform.Find("ImpTitle").GetComponent<TMP_Text>();
            if (controls.FindProperty("abilityTargetDropdown").objectReferenceValue == null)
            {
                var parent = drawer.transform;
                parent.Find("ImpTitle").GetComponent<TMP_Text>().text = "SABOTAGE";
                controls.FindProperty("abilityTargetDropdown").objectReferenceValue =
                    CreateMatchDropdown("AbilityTarget", parent, new Vector2(0.52f, 0.76f), 210, "Upcoming player");
                controls.FindProperty("luckyShiftDropdown").objectReferenceValue =
                    CreateMatchDropdown("ShiftDelta", parent, new Vector2(0.77f, 0.76f), 86, "Shift");
                controls.FindProperty("luckyShiftDeltaText").objectReferenceValue =
                    CreateText("ShiftPreview", parent, "", 17, Color.white, new Vector2(0.9f, 0.35f), new Vector2(118, 40));
                parent.Find("LuckyShiftBtn").GetComponent<RectTransform>().anchorMin =
                    parent.Find("LuckyShiftBtn").GetComponent<RectTransform>().anchorMax = new Vector2(0.66f, 0.35f);
                parent.Find("CursedDiceBtn").GetComponent<RectTransform>().anchorMin =
                    parent.Find("CursedDiceBtn").GetComponent<RectTransform>().anchorMax = new Vector2(0.42f, 0.35f);
                drawer.SetActive(false);
            }

            var dice = (GameObject)controls.FindProperty("dicePanel").objectReferenceValue;
            for (int i = 0; i < 3; i++)
            {
                var die = dice.transform.Find($"DieBtn_{i}");
                var label = die.GetComponentInChildren<TMP_Text>(true);
                label.fontSize = 22;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, -0.15f);
                label.rectTransform.sizeDelta = new Vector2(50, 26);
                label.raycastTarget = false;
            }
            WireScrollableHistory(controls);
            controls.ApplyModifiedProperties();

            var voting = (GameObject)ui.FindProperty("votingPanel").objectReferenceValue;
            var discussion = (GameObject)ui.FindProperty("discussionPanel").objectReferenceValue;
            voting.transform.Find("SkipVoteBtn").GetComponentInChildren<TMP_Text>().text = "SKIP EJECTION";
            var grid = voting.transform.Find("VotingGrid").GetComponent<RectTransform>();
            grid.anchorMin = new Vector2(0.3f, 0.24f);
            grid.anchorMax = new Vector2(0.94f, 0.68f);
            grid.GetComponent<GridLayoutGroup>().constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.GetComponent<GridLayoutGroup>().constraintCount = 3;
            MoveHeading(voting.transform.Find("VTitle"), "CAST YOUR VOTE", 0.8f);
            MoveHeading(voting.transform.Find("VTimer"), "25s", 0.73f);
            MoveHeading(discussion.transform.Find("DTitle"), "DISCUSSION", 0.8f);
            MoveHeading(discussion.transform.Find("DSub"), "Discuss the match before voting starts.", 0.72f);
            MoveHeading(discussion.transform.Find("DTimer"), "45s", 0.61f);

            var menu = (GameObject)ui.FindProperty("mainMenuPanel").objectReferenceValue;
            menu.transform.Find("OnlineMatchCard/LocalSep").GetComponent<TMP_Text>().text = "SAME-MACHINE TESTING";
        }

        private static void MoveHeading(Transform heading, string text, float verticalAnchor)
        {
            var label = heading.GetComponent<TMP_Text>();
            label.text = text;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.62f, verticalAnchor);
        }

        private static TMP_Dropdown CreateMatchDropdown(string name, Transform parent, Vector2 anchor, float width, string placeholder)
        {
            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
            var view = TMP_DefaultControls.CreateDropdown(resources);
            view.name = name;
            view.transform.SetParent(parent, false);
            var rect = view.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(width, 36);
            var dropdown = view.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.captionText.text = placeholder;
            return dropdown;
        }

        private static void WireScrollableHistory(SerializedObject controls)
        {
            if (controls.FindProperty("moveHistoryScroll").objectReferenceValue != null) return;
            var text = (TMP_Text)controls.FindProperty("moveHistoryText").objectReferenceValue;
            var viewport = CreateUIElement("HistoryViewport", text.transform.parent);
            var rect = viewport.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.45f);
            rect.sizeDelta = new Vector2(285, 400);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            var scroll = viewport.AddComponent<ScrollRect>();
            text.transform.SetParent(viewport.transform, false);
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 1);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.raycastTarget = false;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = rect;
            scroll.content = text.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            controls.FindProperty("moveHistoryScroll").objectReferenceValue = scroll;
        }

        private static GameObject CreateMainMenuPanel(Transform parent, Sprite[] portraits)
        {
            var panel = CreateUIElement("MainMenuPanel", parent);
            StretchFull(panel.GetComponent<RectTransform>());
            var controller = panel.AddComponent<MainMenuController>();

            // Title
            var title = CreateText("Title", panel.transform, "LUCKY NUMBER", 56, Color.yellow, new Vector2(0.5f, 0.9f), new Vector2(700, 100));
            title.fontStyle = FontStyles.Bold;

            var subtitle = CreateText("Subtitle", panel.transform, "Online Multiplayer 2D Social Deduction Game", 24, Color.white, new Vector2(0.5f, 0.83f), new Vector2(800, 50));

            // Profile Card (Character Selection & Name)
            var profileBox = CreateCardBox("ProfileCard", panel.transform, new Vector2(0.28f, 0.45f), new Vector2(400, 480));
            CreateText("ProfileTitle", profileBox.transform, "PLAYER PROFILE", 24, Color.cyan, new Vector2(0.5f, 0.9f), new Vector2(350, 40));

            var previewObj = CreateUIElement("CharPreview", profileBox.transform);
            var pRect = previewObj.GetComponent<RectTransform>();
            pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.6f);
            pRect.sizeDelta = new Vector2(160, 160);
            var pImg = previewObj.AddComponent<Image>();
            if (portraits.Length > 0 && portraits[0] != null) pImg.sprite = portraits[0];

            var prevBtn = CreateButton("PrevBtn", profileBox.transform, "<", new Vector2(0.2f, 0.6f), new Vector2(50, 50));
            var nextBtn = CreateButton("NextBtn", profileBox.transform, ">", new Vector2(0.8f, 0.6f), new Vector2(50, 50));

            var nameInput = CreateInputField("NameInput", profileBox.transform, "Enter Name...", new Vector2(0.5f, 0.2f), new Vector2(320, 50));

            // Online Match Card
            var onlineBox = CreateCardBox("OnlineMatchCard", panel.transform, new Vector2(0.72f, 0.45f), new Vector2(480, 480));
            CreateText("OnlineTitle", onlineBox.transform, "PLAY ONLINE (RELAY)", 24, Color.yellow, new Vector2(0.5f, 0.9f), new Vector2(400, 40));

            var createRelayBtn = CreateButton("CreateRelayBtn", onlineBox.transform, "CREATE ONLINE ROOM", new Vector2(0.5f, 0.72f), new Vector2(400, 60));
            var roomInput = CreateInputField("RoomCodeInput", onlineBox.transform, "Enter 6-char Room Code...", new Vector2(0.5f, 0.52f), new Vector2(400, 50));
            var joinRelayBtn = CreateButton("JoinRelayBtn", onlineBox.transform, "JOIN ONLINE ROOM", new Vector2(0.5f, 0.35f), new Vector2(400, 60));

            CreateText("LocalSep", onlineBox.transform, "─── LOCALHOST / LAN MODE ───", 16, Color.gray, new Vector2(0.5f, 0.22f), new Vector2(400, 30));
            var hostLocalBtn = CreateButton("HostLocalBtn", onlineBox.transform, "HOST LOCAL TEST", new Vector2(0.3f, 0.1f), new Vector2(180, 45));
            var joinLocalBtn = CreateButton("JoinLocalBtn", onlineBox.transform, "JOIN LOCAL TEST", new Vector2(0.7f, 0.1f), new Vector2(180, 45));

            // Bottom controls
            var howToPlayBtn = CreateButton("HowToPlayBtn", panel.transform, "HOW TO PLAY", new Vector2(0.38f, 0.08f), new Vector2(200, 50));
            var settingsBtn = CreateButton("SettingsBtn", panel.transform, "SETTINGS", new Vector2(0.52f, 0.08f), new Vector2(200, 50));
            var exitBtn = CreateButton("ExitBtn", panel.transform, "EXIT", new Vector2(0.66f, 0.08f), new Vector2(160, 50));

            var statusText = CreateText("StatusBanner", panel.transform, "Ready.", 20, Color.white, new Vector2(0.5f, 0.15f), new Vector2(800, 40));

            // Serialized linking
            var so = new SerializedObject(controller);
            so.FindProperty("playerNameInput").objectReferenceValue = nameInput;
            so.FindProperty("roomCodeInput").objectReferenceValue = roomInput;
            so.FindProperty("characterPreviewImage").objectReferenceValue = pImg;
            so.FindProperty("prevCharButton").objectReferenceValue = prevBtn.GetComponent<Button>();
            so.FindProperty("nextCharButton").objectReferenceValue = nextBtn.GetComponent<Button>();
            so.FindProperty("createOnlineRoomButton").objectReferenceValue = createRelayBtn.GetComponent<Button>();
            so.FindProperty("joinOnlineRoomButton").objectReferenceValue = joinRelayBtn.GetComponent<Button>();
            so.FindProperty("createLocalRoomButton").objectReferenceValue = hostLocalBtn.GetComponent<Button>();
            so.FindProperty("joinLocalRoomButton").objectReferenceValue = joinLocalBtn.GetComponent<Button>();
            so.FindProperty("howToPlayButton").objectReferenceValue = howToPlayBtn.GetComponent<Button>();
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
            so.FindProperty("exitButton").objectReferenceValue = exitBtn.GetComponent<Button>();
            so.FindProperty("statusText").objectReferenceValue = statusText;

            var portraitsProp = so.FindProperty("characterPortraits");
            portraitsProp.arraySize = portraits.Length;
            for (int i = 0; i < portraits.Length; i++) portraitsProp.GetArrayElementAtIndex(i).objectReferenceValue = portraits[i];
            so.ApplyModifiedProperties();

            return panel;
        }

        private static GameObject CreateLobbyPanel(Transform parent, Sprite[] portraits, GameObject playerCardPrefab)
        {
            var panel = CreateUIElement("LobbyPanel", parent);
            StretchFull(panel.GetComponent<RectTransform>());
            var controller = panel.AddComponent<LobbyController>();

            var header = CreateText("LobbyTitle", panel.transform, "MATCH LOBBY", 42, Color.yellow, new Vector2(0.5f, 0.92f), new Vector2(500, 60));
            var roomCodeText = CreateText("RoomCodeText", panel.transform, "ROOM CODE: ------", 28, Color.cyan, new Vector2(0.5f, 0.85f), new Vector2(400, 40));
            var copyCodeBtn = CreateButton("CopyCodeBtn", panel.transform, "COPY CODE", new Vector2(0.7f, 0.85f), new Vector2(150, 40));
            var playerCountText = CreateText("PlayerCountText", panel.transform, "Players: 1 / 8 (Min: 3)", 22, Color.white, new Vector2(0.5f, 0.8f), new Vector2(400, 40));

            // Player List Container
            var listObj = CreateUIElement("PlayerListContainer", panel.transform);
            var listRect = listObj.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.2f, 0.25f);
            listRect.anchorMax = new Vector2(0.8f, 0.75f);
            listRect.offsetMin = listRect.offsetMax = Vector2.zero;
            var grid = listObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(260, 100);
            grid.spacing = new Vector2(20, 20);
            grid.childAlignment = TextAnchor.UpperCenter;

            // Controls
            var readyBtn = CreateButton("ReadyBtn", panel.transform, "READY", new Vector2(0.35f, 0.12f), new Vector2(220, 60));
            var startBtn = CreateButton("StartBtn", panel.transform, "START MATCH (HOST)", new Vector2(0.55f, 0.12f), new Vector2(260, 60));
            var leaveBtn = CreateButton("LeaveBtn", panel.transform, "LEAVE ROOM", new Vector2(0.75f, 0.12f), new Vector2(200, 60));
            var statusText = CreateText("StatusText", panel.transform, "Waiting for players to join...", 20, Color.white, new Vector2(0.5f, 0.19f), new Vector2(700, 30));

            var so = new SerializedObject(controller);
            so.FindProperty("roomCodeText").objectReferenceValue = roomCodeText;
            so.FindProperty("copyCodeButton").objectReferenceValue = copyCodeBtn.GetComponent<Button>();
            so.FindProperty("playerCountText").objectReferenceValue = playerCountText;
            so.FindProperty("playerListContainer").objectReferenceValue = listObj.transform;
            so.FindProperty("playerCardPrefab").objectReferenceValue = playerCardPrefab;
            so.FindProperty("readyButton").objectReferenceValue = readyBtn.GetComponent<Button>();
            so.FindProperty("readyButtonText").objectReferenceValue = readyBtn.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("startMatchButton").objectReferenceValue = startBtn.GetComponent<Button>();
            so.FindProperty("leaveRoomButton").objectReferenceValue = leaveBtn.GetComponent<Button>();
            so.FindProperty("statusMessageText").objectReferenceValue = statusText;

            var pProp = so.FindProperty("characterPortraits");
            pProp.arraySize = portraits.Length;
            for (int i = 0; i < portraits.Length; i++) pProp.GetArrayElementAtIndex(i).objectReferenceValue = portraits[i];
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateGameplayPanel(Transform parent, Sprite[] diceFaces, Sprite cursedDice)
        {
            var panel = CreateUIElement("GameplayPanel", parent);
            StretchFull(panel.GetComponent<RectTransform>());
            var controller = panel.AddComponent<GameplayController>();

            // Top Scoreboard
            var topBar = CreateCardBox("TopScoreboardBar", panel.transform, new Vector2(0.5f, 0.93f), new Vector2(1200, 90));
            var targetText = CreateText("TargetText", topBar.transform, "TARGET: 42", 28, Color.yellow, new Vector2(0.18f, 0.5f), new Vector2(220, 50));
            var scoreText = CreateText("ScoreText", topBar.transform, "SCORE: 0", 32, Color.cyan, new Vector2(0.42f, 0.5f), new Vector2(220, 50));
            var movesText = CreateText("MovesText", topBar.transform, "MOVES: 15", 28, Color.white, new Vector2(0.65f, 0.5f), new Vector2(200, 50));
            var timerText = CreateText("TimerText", topBar.transform, "30s", 28, Color.green, new Vector2(0.85f, 0.5f), new Vector2(100, 50));
            var roundText = CreateText("RoundText", topBar.transform, "ROUND 1", 20, Color.gray, new Vector2(0.95f, 0.5f), new Vector2(100, 40));

            // Turn Banner
            var turnBanner = CreateText("TurnBanner", panel.transform, "WAITING FOR TURN...", 26, Color.yellow, new Vector2(0.5f, 0.82f), new Vector2(800, 50));

            // Move History Log
            var logBox = CreateCardBox("MoveHistoryLog", panel.transform, new Vector2(0.18f, 0.45f), new Vector2(320, 500));
            CreateText("LogTitle", logBox.transform, "MATCH LOG", 20, Color.cyan, new Vector2(0.5f, 0.92f), new Vector2(280, 30));
            var logText = CreateText("LogText", logBox.transform, "Match started.", 16, Color.white, new Vector2(0.5f, 0.45f), new Vector2(280, 400));
            logText.alignment = TextAlignmentOptions.TopLeft;

            // Private Dice Panel
            var dicePanel = CreateCardBox("DicePanel", panel.transform, new Vector2(0.58f, 0.35f), new Vector2(650, 350));
            CreateText("DiceTitle", dicePanel.transform, "YOUR PRIVATE DICE (CHOOSE ONE)", 22, Color.yellow, new Vector2(0.5f, 0.88f), new Vector2(500, 30));

            Button[] diceBtns = new Button[3];
            Image[] diceImgs = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                var dBtnObj = CreateButton($"DieBtn_{i}", dicePanel.transform, "", new Vector2(0.25f + i * 0.25f, 0.65f), new Vector2(100, 100));
                diceBtns[i] = dBtnObj.GetComponent<Button>();
                diceImgs[i] = dBtnObj.GetComponent<Image>();
                diceImgs[i].color = Color.white;
                dBtnObj.AddComponent<Outline>().enabled = false;
            }

            var addBtn = CreateButton("AddBtn", dicePanel.transform, "+ ADD", new Vector2(0.3f, 0.35f), new Vector2(120, 50));
            var subBtn = CreateButton("SubBtn", dicePanel.transform, "- SUB", new Vector2(0.5f, 0.35f), new Vector2(120, 50));
            var skipBtn = CreateButton("SkipBtn", dicePanel.transform, "SKIP TURN", new Vector2(0.72f, 0.35f), new Vector2(140, 50));

            var previewText = CreateText("PreviewText", dicePanel.transform, "Selected: +0", 22, Color.white, new Vector2(0.35f, 0.15f), new Vector2(260, 40));
            var commitBtn = CreateButton("CommitBtn", dicePanel.transform, "COMMIT MOVE", new Vector2(0.75f, 0.15f), new Vector2(180, 50));

            // Imposter Abilities Drawer
            var impBox = CreateCardBox("ImposterDrawer", panel.transform, new Vector2(0.58f, 0.72f), new Vector2(650, 120));
            CreateText("ImpTitle", impBox.transform, "😈 IMPOSTER SABOTAGE", 18, Color.red, new Vector2(0.2f, 0.8f), new Vector2(220, 25));

            var forcedBtn = CreateButton("ForcedBlockBtn", impBox.transform, "Forced Block (1)", new Vector2(0.18f, 0.35f), new Vector2(140, 40));
            var shiftBtn = CreateButton("LuckyShiftBtn", impBox.transform, "Lucky Shift (1)", new Vector2(0.45f, 0.35f), new Vector2(140, 40));
            var cursedBtn = CreateButton("CursedDiceBtn", impBox.transform, "Cursed Dice (1)", new Vector2(0.72f, 0.35f), new Vector2(140, 40));

            // Serialized Linking
            var so = new SerializedObject(controller);
            so.FindProperty("luckyTargetText").objectReferenceValue = targetText;
            so.FindProperty("currentScoreText").objectReferenceValue = scoreText;
            so.FindProperty("remainingMovesText").objectReferenceValue = movesText;
            so.FindProperty("roundInfoText").objectReferenceValue = roundText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("turnBannerText").objectReferenceValue = turnBanner;
            so.FindProperty("dicePanel").objectReferenceValue = dicePanel;

            var dBtnProp = so.FindProperty("diceButtons");
            dBtnProp.arraySize = 3;
            for (int i = 0; i < 3; i++) dBtnProp.GetArrayElementAtIndex(i).objectReferenceValue = diceBtns[i];

            var dImgProp = so.FindProperty("diceImages");
            dImgProp.arraySize = 3;
            for (int i = 0; i < 3; i++) dImgProp.GetArrayElementAtIndex(i).objectReferenceValue = diceImgs[i];

            var dFaceProp = so.FindProperty("diceFaceSprites");
            dFaceProp.arraySize = diceFaces.Length;
            for (int i = 0; i < diceFaces.Length; i++) dFaceProp.GetArrayElementAtIndex(i).objectReferenceValue = diceFaces[i];

            so.FindProperty("cursedDiceSprite").objectReferenceValue = cursedDice;
            so.FindProperty("addOperationButton").objectReferenceValue = addBtn.GetComponent<Button>();
            so.FindProperty("subtractOperationButton").objectReferenceValue = subBtn.GetComponent<Button>();
            so.FindProperty("voluntarySkipButton").objectReferenceValue = skipBtn.GetComponent<Button>();
            so.FindProperty("commitMoveButton").objectReferenceValue = commitBtn.GetComponent<Button>();
            so.FindProperty("selectedOperationPreviewText").objectReferenceValue = previewText;

            so.FindProperty("imposterDrawer").objectReferenceValue = impBox;
            so.FindProperty("forcedBlockButton").objectReferenceValue = forcedBtn.GetComponent<Button>();
            so.FindProperty("forcedBlockText").objectReferenceValue = forcedBtn.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("luckyShiftButton").objectReferenceValue = shiftBtn.GetComponent<Button>();
            so.FindProperty("luckyShiftText").objectReferenceValue = shiftBtn.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("cursedDiceButton").objectReferenceValue = cursedBtn.GetComponent<Button>();
            so.FindProperty("cursedDiceText").objectReferenceValue = cursedBtn.GetComponentInChildren<TextMeshProUGUI>();

            so.FindProperty("moveHistoryText").objectReferenceValue = logText;
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateDiscussionAndVotingPanel(Transform parent, Sprite[] portraits, GameObject playerCardPrefab, Sprite maskSprite, Sprite normalBadge)
        {
            var root = CreateUIElement("DiscussionAndVotingRoot", parent);
            StretchFull(root.GetComponent<RectTransform>());
            var controller = root.AddComponent<DiscussionAndVotingController>();

            // 1. Discussion View
            var discView = CreateUIElement("DiscussionView", root.transform);
            StretchFull(discView.GetComponent<RectTransform>());
            var dTitle = CreateText("DTitle", discView.transform, "🗣️ DISCUSSION PHASE", 44, Color.yellow, new Vector2(0.5f, 0.85f), new Vector2(600, 60));
            var dSub = CreateText("DSub", discView.transform, "Discuss suspicious plays, targets, and rolls before voting starts!", 22, Color.white, new Vector2(0.5f, 0.78f), new Vector2(800, 40));
            var dTimer = CreateText("DTimer", discView.transform, "45s", 48, Color.green, new Vector2(0.5f, 0.65f), new Vector2(200, 60));

            // 2. Voting View
            var voteView = CreateUIElement("VotingView", root.transform);
            StretchFull(voteView.GetComponent<RectTransform>());
            var vTitle = CreateText("VTitle", voteView.transform, "🗳️ CAST YOUR VOTE", 44, Color.yellow, new Vector2(0.5f, 0.88f), new Vector2(600, 60));
            var vTimer = CreateText("VTimer", voteView.transform, "25s", 36, Color.green, new Vector2(0.5f, 0.81f), new Vector2(200, 50));

            var voteGrid = CreateUIElement("VotingGrid", voteView.transform);
            var vgRect = voteGrid.GetComponent<RectTransform>();
            vgRect.anchorMin = new Vector2(0.2f, 0.25f);
            vgRect.anchorMax = new Vector2(0.8f, 0.75f);
            vgRect.offsetMin = vgRect.offsetMax = Vector2.zero;
            var grid = voteGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(260, 100);
            grid.spacing = new Vector2(20, 20);

            var skipVoteBtn = CreateButton("SkipVoteBtn", voteView.transform, "ABSTAIN / SKIP VOTE", new Vector2(0.5f, 0.15f), new Vector2(260, 55));
            var voteStatus = CreateText("VoteStatus", voteView.transform, "Vote Submitted!", 20, Color.green, new Vector2(0.5f, 0.08f), new Vector2(400, 30));

            // 3. Elimination Reveal Modal
            var elimModal = CreateCardBox("EliminationRevealModal", root.transform, new Vector2(0.5f, 0.5f), new Vector2(600, 400));
            var elimName = CreateText("ElimName", elimModal.transform, "PLAYER WAS ELIMINATED!", 32, Color.yellow, new Vector2(0.5f, 0.8f), new Vector2(500, 50));
            var elimRole = CreateText("ElimRole", elimModal.transform, "THEY WERE AN IMPOSTER!", 28, Color.red, new Vector2(0.5f, 0.6f), new Vector2(500, 40));

            var elimIconObj = CreateUIElement("ElimIcon", elimModal.transform);
            var eiRect = elimIconObj.GetComponent<RectTransform>();
            eiRect.anchorMin = eiRect.anchorMax = new Vector2(0.5f, 0.35f);
            eiRect.sizeDelta = new Vector2(100, 100);
            var elimIcon = elimIconObj.AddComponent<Image>();
            if (maskSprite != null) elimIcon.sprite = maskSprite;

            // Serialized Linking
            var so = new SerializedObject(controller);
            so.FindProperty("discussionTimerText").objectReferenceValue = dTimer;
            so.FindProperty("votingTimerText").objectReferenceValue = vTimer;
            so.FindProperty("votingCandidatesContainer").objectReferenceValue = voteGrid.transform;
            so.FindProperty("voteCandidateCardPrefab").objectReferenceValue = playerCardPrefab;
            so.FindProperty("skipVoteButton").objectReferenceValue = skipVoteBtn.GetComponent<Button>();
            so.FindProperty("voteSubmittedStatusText").objectReferenceValue = voteStatus;

            var pProp = so.FindProperty("characterPortraits");
            pProp.arraySize = portraits.Length;
            for (int i = 0; i < portraits.Length; i++) pProp.GetArrayElementAtIndex(i).objectReferenceValue = portraits[i];

            so.FindProperty("eliminationModalPanel").objectReferenceValue = elimModal;
            so.FindProperty("eliminatedPlayerNameText").objectReferenceValue = elimName;
            so.FindProperty("revealedRoleText").objectReferenceValue = elimRole;
            so.FindProperty("revealedRoleIcon").objectReferenceValue = elimIcon;
            so.FindProperty("imposterMaskSprite").objectReferenceValue = maskSprite;
            so.FindProperty("normalBadgeSprite").objectReferenceValue = normalBadge;
            so.ApplyModifiedProperties();

            discView.SetActive(false);
            voteView.SetActive(false);
            elimModal.SetActive(false);
            return root;
        }

        private static GameObject CreateResultsPanel(Transform parent)
        {
            var panel = CreateUIElement("ResultsPanel", parent);
            StretchFull(panel.GetComponent<RectTransform>());
            var controller = panel.AddComponent<ResultsController>();

            var box = CreateCardBox("ResultsBox", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(700, 500));
            var winnerText = CreateText("WinnerText", box.transform, "🎉 TEAM VICTORY! 🎉", 42, Color.green, new Vector2(0.5f, 0.85f), new Vector2(600, 60));
            var reasonText = CreateText("ReasonText", box.transform, "Lucky Number target was successfully reached!", 24, Color.white, new Vector2(0.5f, 0.65f), new Vector2(600, 60));
            var scoreSummary = CreateText("ScoreSummary", box.transform, "Final Score: 42 | Target: 42", 28, Color.yellow, new Vector2(0.5f, 0.45f), new Vector2(600, 50));

            var playAgainBtn = CreateButton("PlayAgainBtn", box.transform, "PLAY AGAIN (HOST)", new Vector2(0.35f, 0.2f), new Vector2(240, 60));
            var menuBtn = CreateButton("MenuBtn", box.transform, "RETURN TO MENU", new Vector2(0.65f, 0.2f), new Vector2(240, 60));

            var so = new SerializedObject(controller);
            so.FindProperty("winnerTitleText").objectReferenceValue = winnerText;
            so.FindProperty("winReasonText").objectReferenceValue = reasonText;
            so.FindProperty("finalScoreSummaryText").objectReferenceValue = scoreSummary;
            so.FindProperty("playAgainButton").objectReferenceValue = playAgainBtn.GetComponent<Button>();
            so.FindProperty("returnToMenuButton").objectReferenceValue = menuBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateHowToPlayModal(Transform parent)
        {
            var modal = CreateCardBox("HowToPlayModal", parent, new Vector2(0.5f, 0.5f), new Vector2(850, 650));
            modal.AddComponent<HowToPlayModal>();

            CreateText("HTPTitle", modal.transform, "HOW TO PLAY LUCKY NUMBER", 32, Color.yellow, new Vector2(0.5f, 0.92f), new Vector2(700, 50));

            string rules = "<b>GOAL:</b> Normal players work together to reach the target exactly before the 15 shared moves run out.\n\n" +
                "• <b>Turns:</b> The active player privately rolls 3 dice and adds or subtracts one face. Every move spends one shared move. The exact target wins immediately, even on the final move. Overshoots and negative scores are allowed.\n" +
                "• <b>Skips:</b> Each player gets ceil(starting players ÷ 4) voluntary skips. A skip uses the turn but no shared move.\n" +
                "• <b>Roles:</b> There is 1 secret imposter in a 3–5 player game and 2 in a 6–8 player game.\n" +
                "• <b>Imposter abilities:</b> Each imposter can use one ability per round. Forced Block (1 charge) skips a living player's upcoming turn before their roll. Cursed Dice (2 charges) biases two dice while leaving one fair. Both effects can target self; neither reveals the user.\n" +
                "• <b>Lucky Shift:</b> The imposter team shares 1 charge to move the target by −5 to −1 or +1 to +5. It cannot set the target to the current score. The active player keeps their roll and gets one reconsideration grace per turn.\n" +
                "• <b>Discussion & voting:</b> After everyone takes a turn, discuss for 45 seconds, then vote for a living player or Skip Ejection. Tied suspects enter runoffs; after three identical ties, one tied suspect is chosen at random.\n" +
                "• <b>Voice:</b> Discussion uses external voice. Ejected players should stay silent. The game cannot mute external voice, so players must follow this rule themselves.\n" +
                "• <b>Win:</b> Normal players win by reaching the target or eliminating every imposter. Imposters win when shared moves run out or living imposters equal or outnumber living normal players.";

            var body = CreateText("HTPBody", modal.transform, rules, 18, Color.white, new Vector2(0.5f, 0.5f), new Vector2(780, 460));
            body.alignment = TextAlignmentOptions.TopLeft;

            var closeBtn = CreateButton("CloseBtn", modal.transform, "CLOSE", new Vector2(0.5f, 0.08f), new Vector2(180, 50));
            var so = new SerializedObject(modal.GetComponent<HowToPlayModal>());
            so.FindProperty("closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            modal.SetActive(false);
            return modal;
        }

        private static GameObject CreateSettingsModal(Transform parent)
        {
            var modal = CreateCardBox("SettingsModal", parent, new Vector2(0.5f, 0.5f), new Vector2(500, 350));
            modal.AddComponent<SettingsModal>();

            CreateText("SetTitle", modal.transform, "SETTINGS", 32, Color.cyan, new Vector2(0.5f, 0.85f), new Vector2(400, 50));
            CreateText("VolTitle", modal.transform, "Master Volume", 20, Color.white, new Vector2(0.5f, 0.65f), new Vector2(400, 30));

            var sliderObj = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(modal.transform, false);
            var sRect = sliderObj.GetComponent<RectTransform>();
            sRect.anchorMin = sRect.anchorMax = new Vector2(0.5f, 0.5f);
            sRect.sizeDelta = new Vector2(300, 30);
            var slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var closeBtn = CreateButton("CloseBtn", modal.transform, "SAVE & CLOSE", new Vector2(0.5f, 0.2f), new Vector2(200, 50));
            var so = new SerializedObject(modal.GetComponent<SettingsModal>());
            so.FindProperty("masterVolumeSlider").objectReferenceValue = slider;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            modal.SetActive(false);
            return modal;
        }

        private static void BuildIndividualScenes()
        {
            // Build separate individual scenes matching user's architecture specification
            string[] sceneNames = { "MainMenu", "Lobby", "Gameplay", "DiscussionAndVoting", "GameResults" };
            foreach (var sName in sceneNames)
            {
                string sPath = $"Assets/Scenes/{sName}.unity";
                var scn = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camObj.tag = "MainCamera";
                var cam = camObj.GetComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.11f, 0.15f);

                var canvasObj = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

                // Background
                var bgObj = CreateUIElement("Background", canvasObj.transform);
                StretchFull(bgObj.GetComponent<RectTransform>());
                var bgImg = bgObj.AddComponent<Image>();
                var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/GameLoungeBackground.jpeg");
                if (bgSprite != null) bgImg.sprite = bgSprite;

                // Title Banner
                CreateText("SceneBanner", canvasObj.transform, sName.ToUpper(), 42, Color.yellow, new Vector2(0.5f, 0.88f), new Vector2(800, 60));

                EditorSceneManager.SaveScene(scn, sPath);
            }
        }

        private static void UpdateBuildSettings()
        {
            const string masterScenePath = "Assets/Scenes/LuckyNumberMaster.unity";
            string[] allPaths = {
                masterScenePath,
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Lobby.unity",
                "Assets/Scenes/Gameplay.unity",
                "Assets/Scenes/DiscussionAndVoting.unity",
                "Assets/Scenes/GameResults.unity"
            };

            var scenesList = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var p in allPaths)
            {
                if (File.Exists(p))
                {
                    scenesList.Add(new EditorBuildSettingsScene(p, p == masterScenePath));
                }
            }
            EditorBuildSettings.scenes = scenesList.ToArray();
        }

        #region UI Generation Helpers

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateCardBox(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var box = CreateUIElement(name, parent);
            var rect = box.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = box.AddComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            return box;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color col, Vector2 anchor, Vector2 delta)
        {
            var go = CreateUIElement(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = delta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = col;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static GameObject CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size)
        {
            var go = CreateUIElement(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.52f, 0.6f, 1f); // Teal button style
            var btn = go.AddComponent<Button>();

            var textObj = CreateUIElement("Label", go.transform);
            StretchFull(textObj.GetComponent<RectTransform>());
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 anchor, Vector2 size)
        {
            var go = CreateUIElement(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.24f, 0.32f, 1f);

            var input = go.AddComponent<TMP_InputField>();

            var textObj = CreateUIElement("Text", go.transform);
            StretchFull(textObj.GetComponent<RectTransform>());
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 20;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            var phObj = CreateUIElement("Placeholder", go.transform);
            StretchFull(phObj.GetComponent<RectTransform>());
            var phTmp = phObj.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 20;
            phTmp.color = Color.gray;
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            input.textComponent = tmp;
            input.placeholder = phTmp;

            return input;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
