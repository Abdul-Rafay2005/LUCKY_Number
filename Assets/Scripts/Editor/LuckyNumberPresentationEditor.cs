using System.Linq;
using LuckyNumber.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyNumber.Editor
{
    public static partial class LuckyNumberSetupEditor
    {
        private const string MasterScenePath = "Assets/Scenes/LuckyNumberMaster.unity";
        private const string UiPrefabs = "Assets/Prefabs/UI";
        private static readonly Color32 Cream = new(250, 237, 209, 255);
        private static readonly Color32 Gold = new(247, 205, 118, 255);
        private static readonly Color32 Mint = new(147, 230, 200, 255);
        private static readonly Color32 Slate = new(20, 35, 44, 255);
        private static readonly Color32 Teal = new(25, 87, 91, 255);
        private static readonly Color32 Coral = new(255, 147, 133, 255);

        [MenuItem("Lucky Number/Development/Apply Master Scene Presentation", false, 22)]
        public static void ApplyMasterScenePresentation()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MasterScenePath);
            ApplyPresentation(Object.FindFirstObjectByType<LuckyUIManager>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyPresentation(LuckyUIManager manager)
        {
            var root = manager.transform;
            if (!AssetDatabase.IsValidFolder(UiPrefabs))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            var surface = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/RoundedPanel.png");
            if (surface == null)
                surface = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            root.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var background = root.Find("Background").GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color32(98, 95, 98, 255);
            var aspect = background.GetComponent<AspectRatioFitter>();
            if (aspect == null) aspect = background.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;

            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponentInParent<SettingsModal>(true) != null) continue;
                label.raycastTarget = false;
                if (label.name == "Placeholder") label.color = new Color32(171, 198, 196, 255);
                else if (label.name != "Text" || label.GetComponentInParent<TMP_InputField>() != null)
                    label.color = Cream;
            }
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
                Skin(input.GetComponent<Image>(), surface, new Color32(30, 51, 62, 255));
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                Skin(dropdown.GetComponent<Image>(), surface, Teal);
                dropdown.captionText.fontSize = 18;
                dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                bool die = button.name.StartsWith("DieBtn_");
                bool primary = button.name is "CreateRelayBtn" or "StartBtn" or "CommitBtn" or "PlayAgainBtn" ||
                    (button.name == "CloseBtn" && button.GetComponentInParent<SettingsModal>(true) != null);
                bool exit = button.name is "ExitBtn" or "LeaveBtn" or "MenuBtn";
                StyleButton(button, surface, primary, exit, die);
            }

            var menu = root.Find("MainMenuPanel");
            if (menu.GetComponent<CanvasGroup>() == null) menu.gameObject.AddComponent<CanvasGroup>();
            foreach (var name in new[] { "ProfileCard", "OnlineMatchCard" })
                Panel(menu.Find(name), surface, 0);
            Label(menu, "Title", "LUCKY NUMBER", Gold, 88);
            Place(menu.Find("Title"), new Vector2(.5f, .9f), new Vector2(55, 0), new Vector2(800, 105));
            Artwork(menu, "TitleDie", "Assets/Art/Dice/Slices/Die_Golden.png",
                new Vector2(.5f, .9f), new Vector2(100, 100)).rectTransform.anchoredPosition = new Vector2(-445, 0);
            Label(menu, "Subtitle", "Online social deduction for 3–8 players", Cream, 30);
            Label(menu, "ProfileCard/ProfileTitle", "YOUR PROFILE", Mint, 27);
            Label(menu, "OnlineMatchCard/OnlineTitle", "PLAY ONLINE", Gold, 27);
            Place(menu.Find("ProfileCard"), new Vector2(.5f, .48f), new Vector2(-285, 0), new Vector2(430, 480));
            Place(menu.Find("OnlineMatchCard"), new Vector2(.5f, .48f), new Vector2(240, 0), new Vector2(560, 480));
            var status = Label(menu, "StatusBanner", "Ready.", Cream, 23);
            Place(status.transform, new Vector2(.5f, .185f), Vector2.zero, new Vector2(1050, 72));
            status.enableAutoSizing = true;
            status.fontSizeMin = 18;
            status.fontSizeMax = 23;
            var preview = menu.Find("ProfileCard/CharPreview").GetComponent<Image>();
            preview.rectTransform.sizeDelta = new Vector2(230, 230);
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            var online = menu.Find("OnlineMatchCard");
            var testing = online.Find("LocalSep").GetComponent<TMP_Text>();
            var localControls = online.Find("LocalControls");
            if (localControls == null)
            {
                localControls = CreateUIElement("LocalControls", online).transform;
                StretchFull((RectTransform)localControls);
                online.Find("HostLocalBtn").SetParent(localControls, false);
                online.Find("JoinLocalBtn").SetParent(localControls, false);
            }
            var devToggle = online.Find("LocalToggle");
            if (devToggle == null)
            {
                devToggle = CreateButton("LocalToggle", online, "LOCAL TEST OPTIONS", new Vector2(.5f, .225f), new Vector2(270, 40)).transform;
                var disclosure = online.gameObject.AddComponent<LocalTestDisclosure>();
                Set(disclosure, "toggle", devToggle.GetComponent<Button>());
                Set(disclosure, "controls", localControls.gameObject);
            }
            testing.gameObject.SetActive(false);
            localControls.gameObject.SetActive(false);
            StyleButton(devToggle.GetComponent<Button>(), surface, false, false, false);

            var lobby = root.Find("LobbyPanel");
            Label(lobby, "LobbyTitle", "MATCH LOBBY", Gold, 52);
            Label(lobby, "RoomCodeText", null, Mint, 28);
            Place(lobby.Find("RoomCodeText"), new Vector2(.5f, .84f), new Vector2(-105, 0), new Vector2(420, 46));
            Place(lobby.Find("CopyCodeBtn"), new Vector2(.5f, .84f), new Vector2(195, 0), new Vector2(155, 44));
            var lobbyList = lobby.Find("PlayerListContainer").GetComponent<GridLayoutGroup>();
            lobbyList.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            lobbyList.constraintCount = 4;
            lobbyList.cellSize = new Vector2(260, 108);
            lobbyList.spacing = new Vector2(20, 20);
            Place(lobbyList.transform, new Vector2(.5f, .52f), Vector2.zero, new Vector2(1110, 260));
            Backplate(lobby, "RosterSurface", surface, new Vector2(.5f, .52f), new Vector2(1210, 345))
                .GetComponent<Image>().enabled = false;

            var gameplay = root.Find("GameplayPanel");
            var header = gameplay.Find("TopScoreboardBar");
            var history = gameplay.Find("MoveHistoryLog");
            var dice = gameplay.Find("DicePanel");
            var sabotage = gameplay.Find("ImposterDrawer");
            Panel(header, surface, 192);
            Panel(history, surface, 211);
            Panel(dice, surface, 175);
            Panel(sabotage, surface, 167);
            Place(header, new Vector2(.5f, .935f), Vector2.zero, new Vector2(1420, 96));
            var emblem = Artwork(header, "ScoreboardArt", "Assets/Art/Props/Scoreboards/Scoreboard_Normal.png",
                new Vector2(.065f, .5f), new Vector2(130, 73));
            Set(gameplay.GetComponent<GameplayController>(), "scoreboardImage", emblem);
            Set(gameplay.GetComponent<GameplayController>(), "targetEffect",
                Effect(header, "TargetEffect", "Shift_", new Vector2(.33f, .54f), new Vector2(85, 85)));
            Label(header, "TargetText", null, Gold, 38);
            Label(header, "ScoreText", null, Mint, 38);
            Label(header, "MovesText", null, Cream, 31);
            Artwork(header, "TimerIcon", "Assets/Art/UI/Icons/Icon_Clock.png",
                new Vector2(.755f, .5f), new Vector2(40, 40));
            Place(header.Find("TargetText"), new Vector2(.235f, .5f), Vector2.zero, new Vector2(260, 60));
            Place(header.Find("ScoreText"), new Vector2(.45f, .5f), Vector2.zero, new Vector2(240, 60));
            Place(header.Find("MovesText"), new Vector2(.63f, .5f), Vector2.zero, new Vector2(210, 60));
            Place(header.Find("TimerText"), new Vector2(.8f, .5f), Vector2.zero, new Vector2(130, 60));
            Place(header.Find("RoundText"), new Vector2(.92f, .5f), Vector2.zero, new Vector2(145, 60));
            Place(history, new Vector2(.5f, .43f), new Vector2(-520, 0), new Vector2(345, 510));
            Label(history, "LogTitle", "MATCH HISTORY", Mint, 23);
            var viewport = history.Find("HistoryViewport");
            Place(viewport, new Vector2(.5f, .45f), Vector2.zero, new Vector2(296, 413));
            viewport.Find("LogText").GetComponent<TMP_Text>().fontSize = 21;
            Place(gameplay.Find("TurnBanner"), new Vector2(.5f, .735f), new Vector2(180, 0), new Vector2(940, 76));
            Place(sabotage, new Vector2(.5f, .62f), new Vector2(180, 0), new Vector2(720, 132));
            Place(sabotage.Find("ForcedBlockBtn"), new Vector2(.17f, .35f), Vector2.zero, new Vector2(158, 40));
            Place(sabotage.Find("CursedDiceBtn"), new Vector2(.404f, .35f), Vector2.zero, new Vector2(158, 40));
            Place(sabotage.Find("LuckyShiftBtn"), new Vector2(.638f, .35f), Vector2.zero, new Vector2(158, 40));
            Place(sabotage.Find("AbilityTarget"), new Vector2(.54f, .76f), Vector2.zero, new Vector2(250, 36));
            Place(sabotage.Find("ShiftDelta"), new Vector2(.82f, .76f), Vector2.zero, new Vector2(94, 36));
            Place(dice, new Vector2(.5f, .34f), new Vector2(180, 0), new Vector2(720, 370));
            Label(dice, "DiceTitle", "YOUR PRIVATE DICE", Gold, 25);
            var curse = Artwork(dice, "PrivateCurse", "Assets/Art/Dice/Slices/Die_Cursed.png",
                new Vector2(.8f, .88f), new Vector2(55, 55));
            curse.enabled = false;
            Set(gameplay.GetComponent<GameplayController>(), "privateCurseIcon", curse);
            for (int i = 0; i < 3; i++)
            {
                var die = dice.Find($"DieBtn_{i}");
                die.GetComponent<Image>().preserveAspect = true;
                die.GetComponent<Outline>().effectColor = Gold;
                die.GetComponent<Outline>().effectDistance = new Vector2(3, -3);
            }
            var rollFrames = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Dice/DiceRollSprites.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (rollFrames.Length == 0)
                rollFrames = Enumerable.Range(1, 6).Select(i =>
                    AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Dice/Slices/DieRoll_{i}.png")).ToArray();
            SetArray(gameplay.GetComponent<GameplayController>(), "diceRollFrames", rollFrames);
            Set(gameplay.GetComponent<GameplayController>(), "dieSelectionEffect",
                Effect(dice, "DieSelectionEffect", "Sparkle_", new Vector2(.5f, .65f), new Vector2(144, 144)));
            var strip = Backplate(gameplay, "PublicRoster", surface, new Vector2(.5f, .825f), new Vector2(1420, 68));
            strip.GetComponent<Image>().color = new Color32(20, 35, 44, 138);
            var slots = new TMP_Text[8];
            var rosterPortraits = new Image[8];
            for (int i = 0; i < slots.Length; i++)
            {
                var name = $"RosterSlot{i}";
                float center = (i + .5f) / slots.Length;
                rosterPortraits[i] = Artwork(strip, $"RosterPortrait{i}", "", new Vector2(center, .5f), new Vector2(37, 44));
                rosterPortraits[i].rectTransform.anchoredPosition = new Vector2(-63, 0);
                rosterPortraits[i].enabled = false;
                slots[i] = strip.Find(name)?.GetComponent<TMP_Text>() ??
                    CreateText(name, strip, "", 18, Cream, new Vector2(center, .5f), new Vector2(134, 45));
                Place(slots[i].transform, new Vector2(center, .5f), new Vector2(21, 0), new Vector2(134, 45));
                slots[i].alignment = TextAlignmentOptions.MidlineLeft;
                slots[i].enableAutoSizing = true;
                slots[i].fontSizeMin = 13;
                slots[i].fontSizeMax = 18;
                slots[i].overflowMode = TextOverflowModes.Ellipsis;
                slots[i].textWrappingMode = TextWrappingModes.NoWrap;
                slots[i].richText = false;
                slots[i].raycastTarget = false;
            }
            SetArray(gameplay.GetComponent<GameplayController>(), "publicRosterSlots", slots);
            SetArray(gameplay.GetComponent<GameplayController>(), "publicRosterPortraits", rosterPortraits);
            SetArray(gameplay.GetComponent<GameplayController>(), "characterPortraits", Enumerable.Range(0, 6)
                .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Characters/Portraits/Portrait_{i}.png")).ToArray());

            var match = root.Find("DiscussionAndVotingRoot");
            var discussion = match.Find("DiscussionView");
            Backplate(discussion, "DiscussionSurface", surface, new Vector2(.6f, .54f), new Vector2(850, 350))
                .GetComponent<Image>().enabled = false;
            var voting = match.Find("VotingView");
            Backplate(voting, "VotingSurface", surface, new Vector2(.6f, .485f), new Vector2(890, 610))
                .GetComponent<Image>().enabled = false;
            Place(discussion.Find("DTitle"), new Vector2(.6f, .65f), Vector2.zero, new Vector2(700, 65));
            Place(discussion.Find("DSub"), new Vector2(.6f, .55f), Vector2.zero, new Vector2(780, 55));
            Place(discussion.Find("DTimer"), new Vector2(.6f, .43f), Vector2.zero, new Vector2(220, 80));
            Label(discussion, "DTitle", "DISCUSSION", Gold, 48);
            Label(discussion, "DSub", "Review the match history and discuss over voice.", Cream, 28);
            Label(discussion, "DTimer", null, Mint, 60);
            Artwork(discussion, "DiscussionClock", "Assets/Art/UI/Icons/Icon_Clock.png",
                new Vector2(.6f, .5f), new Vector2(75, 75));
            Label(voting, "VTitle", "CAST YOUR VOTE", Gold, 44);
            Label(voting, "VTimer", null, Mint, 29);
            Place(voting.Find("VTitle"), new Vector2(.6f, .72f), Vector2.zero, new Vector2(760, 58));
            Place(voting.Find("VTimer"), new Vector2(.6f, .665f), Vector2.zero, new Vector2(220, 43));
            Place(voting.Find("VotingGrid"), new Vector2(.6f, .47f), Vector2.zero, new Vector2(804, 300));
            var voteGrid = voting.Find("VotingGrid").GetComponent<GridLayoutGroup>();
            voteGrid.childAlignment = TextAnchor.UpperCenter;
            voteGrid.constraintCount = 3;
            voteGrid.cellSize = new Vector2(248, 88);
            voteGrid.spacing = new Vector2(16, 12);
            Place(voting.Find("SkipVoteBtn"), new Vector2(.6f, .29f), Vector2.zero, new Vector2(240, 48));
            Place(voting.Find("VoteStatus"), new Vector2(.6f, .243f), Vector2.zero, new Vector2(785, 40));
            Set(match.GetComponent<DiscussionAndVotingController>(), "acceptedVoteEffect",
                Effect(voting, "AcceptedVoteEffect", "Vote_", new Vector2(.6f, .29f), new Vector2(100, 100)));

            var role = root.Find("RoleRevealPanel");
            var ejection = match.Find("EliminationRevealModal");
            var result = root.Find("ResultsPanel/ResultsBox");
            var help = root.Find("HowToPlayModal");
            var settings = root.Find("SettingsModal");
            help.SetAsLastSibling();
            settings.SetAsLastSibling();
            foreach (var panel in new[] { role, ejection, result, help, settings }) Panel(panel, surface);
            Place(role, new Vector2(.5f, .5f), Vector2.zero, new Vector2(810, 470));
            Label(role, "Heading", "YOUR PRIVATE ROLE", Mint, 26);
            Place(role.Find("Heading"), new Vector2(.5f, .88f), Vector2.zero, new Vector2(640, 45));
            Place(role.Find("Role"), new Vector2(.5f, .59f), Vector2.zero, new Vector2(730, 72));
            Place(role.Find("Objective"), new Vector2(.5f, .34f), Vector2.zero, new Vector2(685, 126));
            var roleIcon = Artwork(role, "RoleIcon", "Assets/Art/UI/Icons/Icon_Alive.png", new Vector2(.5f, .74f), new Vector2(60, 60));
            Set(manager, "roleIcon", roleIcon);
            Set(manager, "normalRoleSprite", roleIcon.sprite);
            Set(manager, "imposterRoleSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/Icon_ImposterMask.png"));
            Label(role, "Role", null, Gold, 54);
            Label(role, "Countdown", null, Mint, 30);

            Place(ejection, new Vector2(.5f, .5f), Vector2.zero, new Vector2(730, 435));
            Label(ejection, "ElimName", null, Gold, 37).enableAutoSizing = true;
            Label(ejection, "ElimRole", null, Coral, 29);
            ejection.Find("ElimIcon").GetComponent<Image>().preserveAspect = true;
            Set(manager, "ejectionEffect",
                Effect(ejection, "EjectionEffect", "Reveal_", new Vector2(.5f, .35f), new Vector2(250, 175)));

            Place(result, new Vector2(.5f, .5f), Vector2.zero, new Vector2(790, 570));
            Artwork(result, "ResultEmblem", "Assets/Art/Dice/Slices/Die_Golden.png", new Vector2(.5f, .84f), new Vector2(92, 92));
            Place(result.Find("WinnerText"), new Vector2(.5f, .68f), Vector2.zero, new Vector2(710, 66));
            Place(result.Find("ReasonText"), new Vector2(.5f, .47f), Vector2.zero, new Vector2(675, 74));
            Place(result.Find("ScoreSummary"), new Vector2(.5f, .34f), Vector2.zero, new Vector2(680, 52));
            Place(result.Find("PlayAgainBtn"), new Vector2(.32f, .16f), Vector2.zero, new Vector2(240, 59));
            Place(result.Find("MenuBtn"), new Vector2(.68f, .16f), Vector2.zero, new Vector2(240, 59));
            var localOutcome = result.Find("LocalOutcome")?.GetComponent<TMP_Text>() ??
                CreateText("LocalOutcome", result, "", 25, Cream, new Vector2(.5f, .58f), new Vector2(680, 42));
            localOutcome.raycastTarget = false;
            Set(result.parent.GetComponent<ResultsController>(), "localOutcomeText", localOutcome);
            Label(result, "ScoreSummary", null, Gold, 28);

            var shade = new Color32(8, 17, 23, 220);
            foreach (var modal in new[] { help, settings, role, ejection, result })
                ModalBackdrop(modal, shade);
            WriteRules(help);
            ConfigureSettings(settings, surface);
            ConfigureUiActions();

            SaveUiPrefabs(surface, dice, header, ejection);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiPrefabs}/PlayerCard.prefab");
            Set(lobby.GetComponent<LobbyController>(), "playerCardPrefab", card);
            Set(match.GetComponent<DiscussionAndVotingController>(), "voteCandidateCardPrefab", card);
        }

        private static void WriteRules(Transform help)
        {
            Place(help, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1010, 860));
            Label(help, "HTPTitle", "HOW TO PLAY", Gold, 40);
            var body = Label(help, "HTPBody",
                "<b>THE TARGET</b>  Normal players win by reaching the target exactly or voting out every imposter. " +
                "Imposters win when moves run out or they reach parity after a vote or departure. An exact hit wins even on the final move.\n\n" +
                "<b>YOUR TURN</b>  Choose one of three private dice, then add or subtract it. A number move spends one shared move. " +
                "Overshoots and negative scores are legal. On timeout, the server picks a die and sign. " +
                "You start with one skip per four players, rounded up. A skip spends your turn and preserves shared moves.\n\n" +
                "<b>SABOTAGE</b>  Each imposter gets one use per round. Forced Block (one charge) skips an upcoming turn. " +
                "Cursed Dice (two charges) biases two dice and leaves one fair. Both can target yourself before the roll. " +
                "Lucky Shift (one team charge) moves the target by 1–5 in either direction, with a short grace period for the current player. " +
                "A shift cannot set the target to the current score.\n\n" +
                "<b>DISCUSS AND VOTE</b>  After each round, vote for a living player or Skip Ejection. No ballot is an abstention. " +
                "A winning or tied skip means no ejection. Tied suspects face up to three runoffs. Three identical tied results can trigger a random ejection. " +
                "Ejected players become spectators. Use external voice and stay silent after ejection.", Cream, 24);
            Place(body.transform, new Vector2(.5f, .49f), Vector2.zero, new Vector2(885, 625));
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20;
            body.fontSizeMax = 24;
        }

        private static void ConfigureVolumeSlider(Slider slider, Sprite surface)
        {
            var track = Backplate(slider.transform, "Track", surface, new Vector2(.5f, .5f), new Vector2(425, 14));
            track.GetComponent<Image>().color = Teal;
            track.GetComponent<Image>().raycastTarget = true;
            if (slider.handleRect != null) return;
            var area = CreateUIElement("HandleArea", slider.transform);
            StretchFull(area.GetComponent<RectTransform>());
            var thumb = CreateUIElement("Handle", area.transform);
            Place(thumb.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 34));
            var handleImage = thumb.AddComponent<Image>();
            Skin(handleImage, surface, Gold);
            slider.handleRect = thumb.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
        }

        private static void SaveUiPrefabs(Sprite surface, Transform dice, Transform header, Transform modal)
        {
            var button = CreateButton("Button", null, "CONTINUE", new Vector2(.5f, .5f), new Vector2(210, 56));
            StyleButton(button.GetComponent<Button>(), surface, true, false, false);
            PrefabUtility.SaveAsPrefabAsset(button, $"{UiPrefabs}/Button.prefab");
            Object.DestroyImmediate(button);
            var panel = CreateCardBox("Panel", null, new Vector2(.5f, .5f), new Vector2(430, 280));
            Panel(panel.transform, surface);
            PrefabUtility.SaveAsPrefabAsset(panel, $"{UiPrefabs}/Panel.prefab");
            Object.DestroyImmediate(panel);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCardPrefab.prefab");
            var cardCopy = Object.Instantiate(card);
            cardCopy.name = "PlayerCard";
            StyleButton(cardCopy.GetComponent<Button>(), surface, false, false, false);
            var portrait = cardCopy.transform.Find("Portrait").GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.rectTransform.sizeDelta = new Vector2(64, 72);
            var name = cardCopy.transform.Find("PlayerNameText").GetComponent<TMP_Text>();
            name.fontStyle = FontStyles.Bold;
            name.fontSizeMin = 13;
            name.fontSizeMax = 19;
            name.rectTransform.offsetMin = new Vector2(80, 5);
            name.rectTransform.offsetMax = new Vector2(-8, -5);
            var status = cardCopy.transform.Find("StatusText").GetComponent<TMP_Text>();
            status.color = Mint;
            status.rectTransform.offsetMin = new Vector2(80, 5);
            status.rectTransform.offsetMax = new Vector2(-8, -5);
            foreach (var graphic in cardCopy.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != cardCopy) graphic.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(cardCopy, $"{UiPrefabs}/PlayerCard.prefab");
            Object.DestroyImmediate(cardCopy);

            SaveCopy(dice, "DiceControls");
            SaveCopy(header, "ScoreHeader");
            SaveCopy(modal, "RevealModal");
        }

        private static void SaveCopy(Transform original, string name)
        {
            var copy = Object.Instantiate(original.gameObject);
            copy.name = name;
            copy.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(copy, $"{UiPrefabs}/{name}.prefab");
            Object.DestroyImmediate(copy);
        }

        private static void ModalBackdrop(Transform panel, Color color)
        {
            var dim = panel.Find("ModalBackdrop");
            if (dim == null) dim = CreateUIElement("ModalBackdrop", panel).transform;
            dim.SetAsFirstSibling();
            Place(dim, new Vector2(.5f, .5f), Vector2.zero, new Vector2(5000, 5000));
            var img = dim.GetComponent<Image>() ?? dim.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            // Render the dimmer behind the modal surface, but keep it in the modal lifecycle.
            var bg = panel.Find("ModalSurface");
            if (bg == null) bg = CreateUIElement("ModalSurface", panel).transform;
            bg.SetSiblingIndex(1);
            StretchFull((RectTransform)bg);
            var source = panel.GetComponent<Image>();
            var surface = bg.GetComponent<Image>() ?? bg.gameObject.AddComponent<Image>();
            Skin(surface, source.sprite, Slate);
            surface.raycastTarget = false;
            source.color = Color.clear;
            source.raycastTarget = false;
        }

        private static Transform Backplate(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size)
        {
            var plate = parent.Find(name) ?? CreateUIElement(name, parent).transform;
            plate.SetAsFirstSibling();
            Place(plate, anchor, Vector2.zero, size);
            var image = plate.GetComponent<Image>() ?? plate.gameObject.AddComponent<Image>();
            Skin(image, sprite, Slate);
            image.raycastTarget = false;
            return plate;
        }

        private static Image Artwork(Transform parent, string name, string path, Vector2 anchor, Vector2 size)
        {
            var artwork = parent.Find(name) ?? CreateUIElement(name, parent).transform;
            Place(artwork, anchor, Vector2.zero, size);
            var image = artwork.GetComponent<Image>() ?? artwork.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static SpriteBurst Effect(Transform parent, string name, string prefix, Vector2 anchor, Vector2 size)
        {
            var image = Artwork(parent, name, "", anchor, size);
            image.enabled = false;
            var effect = image.GetComponent<SpriteBurst>() ?? image.gameObject.AddComponent<SpriteBurst>();
            var frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/VFX/MatchEffectSprites.png")
                .OfType<Sprite>().Where(sprite => sprite.name.StartsWith(prefix, System.StringComparison.Ordinal))
                .OrderBy(sprite => sprite.name).ToArray();
            SetArray(effect, "frames", frames);
            return effect;
        }

        private static void Panel(Transform panel, Sprite surface, byte alpha = 255)
        {
            var image = panel.GetComponent<Image>();
            var color = Slate;
            color.a = alpha;
            Skin(image, surface, color);
            image.raycastTarget = false;
        }

        private static void StyleButton(Button button, Sprite surface, bool primary, bool exit, bool die)
        {
            var image = button.image;
            if (image == null) return;
            button.targetGraphic = image;
            if (!die) Skin(image, surface, primary ? Gold : exit ? new Color32(106, 56, 53, 255) : Teal);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 240, 185, 255);
            colors.pressedColor = new Color32(190, 194, 190, 255);
            colors.selectedColor = new Color32(255, 223, 151, 255);
            colors.disabledColor = new Color32(158, 165, 171, 177);
            colors.fadeDuration = .10f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.color = primary ? Slate : Cream;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
        }

        private static void Skin(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static TMP_Text Label(Transform parent, string name, string text, Color color, float size)
        {
            var label = parent.Find(name).GetComponent<TMP_Text>();
            if (text != null) label.text = text;
            label.color = color;
            label.fontSize = size;
            label.raycastTarget = false;
            return label;
        }

        private static void Place(Transform transform, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void Set(Component target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"Missing {target.GetType().Name}.{field}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Component target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
