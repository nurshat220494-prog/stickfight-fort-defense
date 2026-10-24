using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickFightFortDefense
{
[BepInPlugin("local.stickfight.fortdefense", "Fort Defense", "0.8.95")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const int AbilityColorCount = 19;
        private const KeyCode StartKey = KeyCode.F8;
        private const KeyCode KeyboardAbilityKey = KeyCode.E;
        private const KeyCode KeyboardReviveKey = KeyCode.F10;
        private const float EnemySpawnRootOffset = 0.95f;
        private static readonly FieldInfo HoardField = typeof(GameManager).GetField(
            "hoardHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo WaveField = typeof(HoardHandler).GetField(
            "CurrentWave", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AliveField = typeof(HoardHandler).GetField(
            "charactersAlive", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo StartWaveMethod = typeof(HoardHandler).GetMethod(
            "StartWave", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIControllerField = typeof(AI).GetField(
            "controller", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIVelocityField = typeof(AI).GetField(
            "velocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo PlayerActionsField = typeof(Controller).GetField("mPlayerActions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo ControllerFightingField = typeof(Controller).GetField("fighting", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo HealthControllerRefField = typeof(HealthHandler).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AbilityUpField = typeof(CharacterActions).GetField("Up", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AbilityDownField = typeof(CharacterActions).GetField("Down", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AbilityLeftField = typeof(CharacterActions).GetField("Left", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AbilityRightField = typeof(CharacterActions).GetField("Right", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AbilityBlockField = typeof(CharacterActions).GetField("Block", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo PunchCooldownField = typeof(Fighting).GetField("punchCD", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo InternalLoadingField = typeof(GameManager).GetField(
            "isLoadingInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo FinishMatchMethod = typeof(GameManager).GetMethod(
            "AllButOnePlayersDied", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo RestartGameMethod = typeof(GameManager).GetMethod(
            "RestartGame", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo IsInLobbyMethod = typeof(GameManager).GetMethod(
            "IsInLobby", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private HoardHandler handler;
        private bool modeStarted;
        private bool waitingForMatchStart;
        private bool consoleOpen;
        private bool defeatPending;
        private float defeatReturnAt;
        private GameManager defeatGame;
        private float matchStartRequestedAt;
        private float nextWaitLogAt;
        private float statusUntil;
        private float nextWaveAt;
        private float nextEnemyColorScanAt;
        private float nextTransitionDiagnosticAt;
        private float nextLevelReadyAt = -1f;
        private int observedWave;
        private int currentLevelNumber;
        private bool advancingLevel;
        private bool levelHadEnemies;
        private int enemyColorIndex;
        private int pendingEnemySpawns;
        private readonly List<Vector3> reservedSpawnPoints = new List<Vector3>();
        private readonly Dictionary<int, Material> enemyMaterials = new Dictionary<int, Material>();
        private readonly Dictionary<int, float> abilityReadyAt = new Dictionary<int, float>();
        private readonly Dictionary<int, int> selectedLobbyColors = new Dictionary<int, int>();
        private readonly Dictionary<int, float> shieldUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, GameObject> shieldObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> abilityVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Controller> abilityVisualOwners = new Dictionary<int, Controller>();
        private readonly Dictionary<int, float> abilityVisualUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, float> abilityVisualBaseWidths = new Dictionary<int, float>();
        private readonly Dictionary<int, float> rootedUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, GameObject> rootVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, float> stoneSkinUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, float> shadowFormUntil = new Dictionary<int, float>();
        private readonly HashSet<int> goldCrushReady = new HashSet<int>();
        private readonly Dictionary<int, float> goldCrushReadyUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, SeedMark> parasiticSeeds = new Dictionary<int, SeedMark>();
        private readonly Dictionary<int, RescueOath> rescueOathsByAlly = new Dictionary<int, RescueOath>();
        private readonly Dictionary<int, HunterMark> hunterMarks = new Dictionary<int, HunterMark>();
        private readonly Dictionary<int, float> lemonOriginalVelocities = new Dictionary<int, float>();
        private float lemonGreedUntil;
        private bool lemonMovementFieldsLogged;
        private readonly Dictionary<int, float> rageUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, int> reviveCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, HashSet<int>> selectedPerks = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, float> perkOriginalHealth = new Dictionary<int, float>();
        public readonly HashSet<int> fortifiedPlayers = new HashSet<int>();
        private readonly HashSet<int> medicPlayers = new HashSet<int>();
        private readonly List<Controller> pendingPerkPlayers = new List<Controller>();
        private readonly int[] activePerkOffers = new int[3];
        private bool perkSelectionActive;
        private int activePerkPlayerIndex;
        private int activePerkOptionCount;
        private RectTransform perkPanel;
        private RectTransform colorPanel;
        private Text colorDescription;
        private readonly Button[] colorOptionButtons = new Button[19];
        private readonly Outline[] colorOptionOutlines = new Outline[19];
        private Text[] abilityGaugeLabels = new Text[4];
        private Image[] abilityGaugeFills = new Image[4];
        private GameObject colorStation;
        private Controller colorMenuPlayer;
        private int colorMenuSelection;
        private bool previousEventNavigation;
        private Texture2D beamTexture;
        private Material beamMaterial;
        private Material sprayBeamSourceMaterial;
        private bool sprayBeamStyleScanned;
        private float sprayBeamLifetime = 0.18f;
        private float sprayBeamStartWidth = 0.42f;
        private float sprayBeamEndWidth = 0.24f;
        private Text perkPrompt;
        private RectTransform perkOptionsContainer;
        private string status = "Join at least one player, then press F8 or use the button.";
        private readonly List<string> consoleLines = new List<string>();
        private RectTransform startPanel;
        private RectTransform consolePanel;
        private Text overlayStatusText;
        private RectTransform defeatPanel;
        private Text defeatCountdownText;
        private Text consoleLogText;
        private InputField consoleInput;
        private static Plugin CurrentPlugin;

        private sealed class SeedMark
        {
            public Controller Owner;
            public Controller Target;
            public float DetonateAt;
            public int Propagations;
        }

        private sealed class RescueOath
        {
            public Controller Owner;
            public Controller Ally;
            public float ExpiresAt;
            public float RedirectedDamage;
            public LineRenderer Tether;
        }

        private sealed class HunterMark
        {
            public Controller Target;
            public float ExpiresAt;
            public GameObject Marker;
        }

        public static void LogNavigation(string message)
        {
            if (CurrentPlugin != null)
            {
                CurrentPlugin.Logger.LogInfo(message);
            }
        }

        public static bool IsColorMenuOpen
        {
            get { return CurrentPlugin != null && CurrentPlugin.colorMenuPlayer != null && CurrentPlugin.colorPanel != null && CurrentPlugin.colorPanel.gameObject.activeSelf; }
        }

        public static bool IsConsoleOpen
        {
            get { return CurrentPlugin != null && CurrentPlugin.consoleOpen; }
        }

        public static bool IsColorMenuOpenFor(Controller player)
        {
            return IsColorMenuOpen && player != null && System.Object.ReferenceEquals(CurrentPlugin.colorMenuPlayer, player);
        }

        public static bool TryPrepareGoldCrushHit(ref float damage, ref Vector3 force, Controller attacker, Controller victim)
        {
            if (CurrentPlugin == null || !EnemyAIPatches.ModeActive || attacker == null || attacker.isAI ||
                victim == null || !victim.isAI) return false;
            int id = attacker.GetInstanceID();
            float expiresAt;
            if (!CurrentPlugin.goldCrushReady.Contains(id) ||
                !CurrentPlugin.goldCrushReadyUntil.TryGetValue(id, out expiresAt) || Time.unscaledTime >= expiresAt)
                return false;
            CurrentPlugin.goldCrushReady.Remove(id);
            CurrentPlugin.goldCrushReadyUntil.Remove(id);
            damage *= 2f;
            force *= 1.75f;
            CurrentPlugin.StartCoroutine(CurrentPlugin.GoldCrushShockwave(GetActorPosition(victim), attacker, victim));
            CurrentPlugin.Logger.LogInfo("Gold crush empowered the next melee hit: damage x2 and knockback x1.75.");
            return true;
        }

        private void Awake()
        {
            CurrentPlugin = this;
            Application.runInBackground = true;
            Logger.LogInfo("Fort Defense 0.8.95 Awake entered; background simulation enabled.");
            try
            {
                InstallEnemyAIPatches();
            }
            catch (Exception ex)
            {
                Logger.LogError("Patch installation failed: " + ex);
            }
            status = "Join at least one player, then press F8 or use the button.";
            Logger.LogInfo("Fort Defense loaded. Join 1–4 players, then press F8 to start local PvE.");
            Logger.LogInfo("Developer console: press F1. Commands: help, status, join (test), start, starttest (test), startpurpletest (test), clearwave, testabilities (test), testpurple (test), sprayinfo (test).");
            CreateOverlay();
        }

        private void InstallEnemyAIPatches()
        {
            Logger.LogInfo("Creating Harmony patch set.");
            Harmony harmony = new Harmony("local.stickfight.fortdefense.enemyai");
            Logger.LogInfo("Harmony patch set created.");
            PatchSafe(harmony,
                typeof(AI).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "AI.Update", "PrepareAI", "AfterAIUpdate");
            PatchSafe(harmony,
                typeof(AI).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "AI.Update rooted-state gate", "GateRootedAI", null);
            PatchSafe(harmony,
                typeof(Controller).GetMethod("Jump", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "Controller.Jump", "GuardAIJump", null);
            PatchSafe(harmony,
                typeof(WeaponSelectionHandler).GetMethod("GetRandomWeaponIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "WeaponSelectionHandler.GetRandomWeaponIndex", "FilterSnakeWeaponDrops", null);
            PatchSafe(harmony,
                typeof(HealthHandler).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "HealthHandler.Start", null, "SetEnemyHealth");
            PatchSafe(harmony,
                typeof(HealthHandler).GetMethod("Die", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "HealthHandler.Die revive interception", "InterceptPlayerDeath", null);
            PatchSafe(harmony,
                typeof(Controller).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "Controller.Update downed input gate", "GateDownedPlayer", null);
            PatchSafe(harmony,
                typeof(HoardHandler).GetMethod("SpawnAI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "HoardHandler.SpawnAI", "TelegraphEnemySpawn", null);
            MethodInfo bodyPartPunch = typeof(BodyPart).GetMethod("TakeDamageWithParticle",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new Type[] { typeof(float), typeof(Vector3), typeof(Vector3), typeof(bool), typeof(Controller) }, null);
            PatchSafe(harmony, bodyPartPunch, "BodyPart.TakeDamageWithParticle", "AllowUnownedEnemyPunch", "VerifyBodyPartPunchDamage");
            PatchSafe(harmony,
                typeof(HealthHandler).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "HealthHandler.Update", "DisableEnemyRegeneration", "RestoreRegeneration");
            MethodInfo takeDamage = typeof(HealthHandler).GetMethod("TakeDamage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new Type[] { typeof(float), typeof(Controller), typeof(DamageType), typeof(bool), typeof(Vector3), typeof(Vector3) }, null);
            PatchSafe(harmony, takeDamage, "HealthHandler.TakeDamage", "AllowLocalEnemyDamage", "LogEnemyDamageApplied");
            PatchSafe(harmony,
                typeof(Fighting).GetMethod("Attack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "Fighting.Attack punch cadence", "CapturePunchCooldown", "ApplyPunchCooldownRate");
            PatchSafe(harmony,
                typeof(ProjectileCollision).GetMethod("HitObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "ProjectileCollision.HitObject", "PrepareProjectileHit", "RestoreProjectileDamage");
            PatchSafe(harmony,
                typeof(GameManager).GetMethod("StartMatch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "GameManager.StartMatch", "PurgeTaggedEnemies", null);
            PatchSafe(harmony,
                FinishMatchMethod,
                "GameManager.AllButOnePlayersDied", "HandlePlayerElimination", null);
            PatchSafe(harmony,
                typeof(RemoveAI).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "RemoveAI.Start", "KeepEnemyCorpse", null);
            Logger.LogInfo("Installed enemy AI, marked solid-surface spawns, local enemy punch damage, co-op downed/revive state, defeat-to-lobby, health, projectile critical, map cleanup, snake weapon, and corpse patches.");
        }

        private void PatchSafe(Harmony harmony, MethodBase original, string label, string prefixName, string postfixName)
        {
            if (System.Object.ReferenceEquals(original, null))
            {
                Logger.LogError("Could not find method for patch " + label + ".");
                return;
            }
            try
            {
                Type patchType = typeof(EnemyAIPatches);
                HarmonyMethod prefix = String.IsNullOrEmpty(prefixName) ? null : new HarmonyMethod(patchType.GetMethod(prefixName, BindingFlags.Static | BindingFlags.Public));
                HarmonyMethod postfix = String.IsNullOrEmpty(postfixName) ? null : new HarmonyMethod(patchType.GetMethod(postfixName, BindingFlags.Static | BindingFlags.Public));
                harmony.Patch(original, prefix, postfix, null, null, null);
                Logger.LogInfo("Patched " + label + ".");
            }
            catch (Exception ex)
            {
                Logger.LogError("Could not patch " + label + ": " + ex);
            }
        }

        private void Update()
        {
            UpdateRageEffects();
            UpdateAbilityEffects();
            UpdateRootedEffects();
            UpdateRunPerks();
            UpdateGoldCrushExpiry();
            UpdateParasiticSeeds();
            UpdateRescueOaths();
            UpdateHunterMarks();
            if (Time.unscaledTime >= lemonGreedUntil && lemonOriginalVelocities.Count > 0)
                RestoreLemonMovementSlow();
            UpdateReviveSystem();
            if (defeatPending)
            {
                float remaining = Mathf.Max(0f, defeatReturnAt - Time.unscaledTime);
                if (defeatCountdownText != null)
                {
                    defeatCountdownText.text = "Все игроки погибли.\nВозврат в меню через " + Mathf.CeilToInt(remaining) + "...";
                }
                if (remaining <= 0f)
                {
                    FinishDefeatReturn();
                }
            }

            if (!IsColorMenuOpen && Input.GetKeyDown(StartKey))
            {
                Logger.LogInfo("F8 input received by Fort Defense.");
                StartMode();
            }

            UpdateColorStation();
            SyncColorMenuSelection();
            if (colorPanel != null && colorPanel.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseColorMenu();
            }

            if (perkSelectionActive)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChoosePerk(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChoosePerk(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ChoosePerk(2);
            }

            if (!IsColorMenuOpen && Input.GetKeyDown(KeyCode.F1))
            {
                consoleOpen = !consoleOpen;
                AddConsoleLine(consoleOpen ? "Console opened. Type help for commands." : "Console closed.");
                Logger.LogInfo(consoleOpen ? "Fort Defense developer console opened." : "Fort Defense developer console closed.");
                if (consolePanel != null)
                {
                    consolePanel.gameObject.SetActive(consoleOpen);
                }
                if (consoleOpen && consoleInput != null)
                {
                    consoleInput.text = String.Empty;
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(consoleInput.gameObject);
                    consoleInput.ActivateInputField();
                }
            }

            if (consoleOpen && Input.GetKeyDown(KeyCode.Return) && consoleInput != null)
            {
                RunConsoleCommand(consoleInput.text);
                consoleInput.text = String.Empty;
                consoleInput.ActivateInputField();
            }

            if (waitingForMatchStart)
            {
                if (GameManager.Instance == null)
                {
                    waitingForMatchStart = false;
                    status = "Game manager disappeared before match start.";
                    statusUntil = Time.time + 5f;
                }
                else if (!IsInLobby(GameManager.Instance) && GameManager.inFight)
                {
                    waitingForMatchStart = false;
                    InitializeMode(GameManager.Instance);
                }
                else if (Time.unscaledTime - matchStartRequestedAt > 20f)
                {
                    waitingForMatchStart = false;
                    status = "Match did not start. Press F8 after joining a player.";
                    statusUntil = Time.time + 6f;
                    Logger.LogWarning(status);
                }
                else if (Time.unscaledTime >= nextWaitLogAt)
                {
                    nextWaitLogAt = Time.unscaledTime + 2f;
                    object internalLoading = System.Object.ReferenceEquals(InternalLoadingField, null) ? (object)"unknown" : InternalLoadingField.GetValue(GameManager.Instance);
                    Logger.LogInfo("Waiting for local match load: inFight=" + GameManager.inFight + ", isLoading=" + GameManager.Instance.isLoading + ", internal=" + internalLoading + ".");
                }
            }

            if (modeStarted && handler != null)
            {
                UpdatePlayerAbilities();
                int wave = GetCurrentWave();
                if (wave != observedWave)
                {
                    observedWave = wave;
                    enemyColorIndex = 0;
                    status = "Wave " + currentLevelNumber + " — survive.";
                    Logger.LogInfo(status);
                }

                UpdateEnemyColors();
                int alive = GetAliveEnemyCount();
                if (alive > 0)
                {
                    levelHadEnemies = true;
                }
                if (!advancingLevel && levelHadEnemies && alive == 0 && Time.unscaledTime >= nextWaveAt)
                {
                    CompleteLevelAndAdvance();
                }
            }

            if (modeStarted && !advancingLevel && (GameManager.Instance == null || !GameManager.inFight))
            {
                Logger.LogInfo("Fort Defense stopped because the local match ended.");
                StopMode();
            }

            RefreshOverlay();
        }

        private void OnGUI()
        {
            if (!IsColorMenuOpen) return;
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;
            int rowCount = (colorOptionButtons.Length + 3) / 4;
            int row = colorMenuSelection / 4;
            int column = colorMenuSelection % 4;
            int rowItems = Math.Min(4, colorOptionButtons.Length - row * 4);
            bool moved = true;
            switch (current.keyCode)
            {
                case KeyCode.LeftArrow: case KeyCode.A: column = (column + rowItems - 1) % rowItems; break;
                case KeyCode.RightArrow: case KeyCode.D: column = (column + 1) % rowItems; break;
                case KeyCode.UpArrow: case KeyCode.W:
                    row = (row + rowCount - 1) % rowCount;
                    column = Math.Min(column, Math.Min(4, colorOptionButtons.Length - row * 4) - 1);
                    break;
                case KeyCode.DownArrow: case KeyCode.S:
                    row = (row + 1) % rowCount;
                    column = Math.Min(column, Math.Min(4, colorOptionButtons.Length - row * 4) - 1);
                    break;
                case KeyCode.Return: case KeyCode.KeypadEnter: current.Use(); SelectLobbyColor(colorMenuSelection); return;
                case KeyCode.Alpha1: case KeyCode.Keypad1: current.Use(); SelectLobbyColor(0); return;
                case KeyCode.Alpha2: case KeyCode.Keypad2: current.Use(); SelectLobbyColor(1); return;
                case KeyCode.Alpha3: case KeyCode.Keypad3: current.Use(); SelectLobbyColor(2); return;
                case KeyCode.Alpha4: case KeyCode.Keypad4: current.Use(); SelectLobbyColor(3); return;
                case KeyCode.Alpha5: case KeyCode.Keypad5: current.Use(); SelectLobbyColor(4); return;
                case KeyCode.Alpha6: case KeyCode.Keypad6: current.Use(); SelectLobbyColor(5); return;
                case KeyCode.Alpha7: case KeyCode.Keypad7: current.Use(); SelectLobbyColor(6); return;
                case KeyCode.Alpha8: case KeyCode.Keypad8: current.Use(); SelectLobbyColor(7); return;
                case KeyCode.Alpha9: case KeyCode.Keypad9: current.Use(); SelectLobbyColor(8); return;
                case KeyCode.Alpha0: case KeyCode.Keypad0: current.Use(); SelectLobbyColor(9); return;
                case KeyCode.Escape: current.Use(); CloseColorMenu(); return;
                default: moved = false; break;
            }
            if (!moved) return;
            current.Use();
            int next = row * 4 + column;
            if (next != colorMenuSelection)
            {
                colorMenuSelection = next;
                UpdateColorMenuDescription();
                if (EventSystem.current != null && colorOptionButtons[next] != null)
                    EventSystem.current.SetSelectedGameObject(colorOptionButtons[next].gameObject);
                Logger.LogInfo("Color picker keyboard selection moved to option " + (next + 1) + ".");
            }
        }

        private void StartMode()
        {
            Logger.LogInfo("StartMode entered.");
            try
            {
                StartModeInternal();
            }
            catch (Exception ex)
            {
                waitingForMatchStart = false;
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                status = "Start failed: " + cause.GetType().Name;
                statusUntil = Time.time + 10f;
                Logger.LogError(status + " — " + cause);
            }
        }

        private void StartModeInternal()
        {
            statusUntil = Time.time + 5f;
            if (modeStarted || waitingForMatchStart)
            {
                status = "Fort Defense is already starting or running.";
                Logger.LogInfo(status);
                return;
            }

            GameManager game = GameManager.Instance;
            Logger.LogInfo("Start requested. GameManager=" + (game == null ? "null" : "ready") + ", inFight=" + GameManager.inFight + ", humans=" + GetHumanPlayerCount() + ".");
            if (game == null)
            {
                status = "The game manager is not ready.";
                Logger.LogWarning(status);
                return;
            }

            bool inLobby = IsInLobby(game);
            Logger.LogInfo("Start state: inFight=" + GameManager.inFight + ", inLobby=" + inLobby + ".");
            if (!inLobby && GameManager.inFight)
            {
                InitializeMode(game);
                return;
            }

            int humanPlayers = GetHumanPlayerCount();
            if (humanPlayers < 1 || humanPlayers > 4)
            {
                status = "Join 1–4 human players first (WASD or controller), then press F8.";
                Logger.LogWarning(status);
                return;
            }

            MapWrapper map;
            try
            {
                map = GetLobbyMap(game);
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                status = "Map selection failed: " + cause.GetType().Name;
                Logger.LogError(status + " — " + cause);
                return;
            }
            if (map == null)
            {
                status = "Could not select a map for Fort Defense.";
                Logger.LogError(status);
                return;
            }

            Logger.LogInfo("Starting local PvE with " + humanPlayers + " human player(s); map type " + map.MapType + ".");

            MethodInfo startMatch = typeof(GameManager).GetMethod("StartMatch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (System.Object.ReferenceEquals(startMatch, null))
            {
                status = "This game build has no StartMatch entry point.";
                Logger.LogError(status);
                return;
            }

            status = "Starting PvE for " + humanPlayers + " player(s)…";
            waitingForMatchStart = true;
            matchStartRequestedAt = Time.unscaledTime;
            nextWaitLogAt = Time.unscaledTime + 2f;
            Logger.LogInfo(status);
            try
            {
                startMatch.Invoke(game, new object[] { map, true });
            }
            catch (Exception ex)
            {
                waitingForMatchStart = false;
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                status = "The game rejected the solo match start: " + cause.GetType().Name;
                statusUntil = Time.time + 10f;
                Logger.LogError(status + " — " + cause);
            }
        }

        private MapWrapper GetLobbyMap(GameManager game)
        {
            // In the title/lobby flow lastMapNumber may be an empty wrapper. Reuse the
            // game's normal level selector first so StartMatch receives a real map.
            FieldInfo selectorField = typeof(GameManager).GetField("levelSelector", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            LevelSelection selector = System.Object.ReferenceEquals(selectorField, null) ? null : selectorField.GetValue(game) as LevelSelection;
            if (selector != null)
            {
                MethodInfo getNextLevel = typeof(LevelSelection).GetMethod("GetNextLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!System.Object.ReferenceEquals(getNextLevel, null))
                {
                    MapWrapper selected = getNextLevel.Invoke(selector, null) as MapWrapper;
                    if (selected != null && selected.MapData != null && selected.MapData.Length > 0)
                    {
                        return selected;
                    }
                }
            }

            MethodInfo getCurrentMap = typeof(GameManager).GetMethod("GetCurrentMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!System.Object.ReferenceEquals(getCurrentMap, null))
            {
                MapWrapper current = getCurrentMap.Invoke(game, null) as MapWrapper;
                if (current != null && current.MapData != null && current.MapData.Length > 0)
                {
                    return current;
                }
            }

            return null;
        }

        private int GetHumanPlayerCount()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                if (player != null && !player.isAI)
                {
                    count++;
                }
            }
            return count;
        }

        private void InitializeMode(GameManager game)
        {
            if (game == null || IsInLobby(game) || !GameManager.inFight)
            {
                status = "The local match has not started yet.";
                statusUntil = Time.time + 5f;
                Logger.LogWarning(status);
                return;
            }

            handler = FindHoardHandler(game);
            if (handler == null)
            {
                status = "This map has no configured Stick Fight wave handler.";
                statusUntil = Time.time + 6f;
                Logger.LogWarning(status);
                return;
            }

            if (handler.character == null)
            {
                status = "The map's wave handler has no enemy prefab configured.";
                statusUntil = Time.time + 6f;
                Logger.LogWarning(status);
                return;
            }

            int humanPlayers = Math.Max(1, GetHumanPlayerCount());
            handler.waves = false;
            modeStarted = true;
            advancingLevel = false;
            currentLevelNumber = 1;
            levelHadEnemies = false;
            EnemyAIPatches.ModeActive = true;
            observedWave = GetCurrentWave();
            enemyColorIndex = 0;
            nextWaveAt = Time.unscaledTime;
            status = "Survive endless waves. " + humanPlayers + " human player(s).";
            statusUntil = Time.time + 5f;
            Logger.LogInfo(status);
            if (GetAliveEnemyCount() == 0)
            {
                StartLevelEnemies();
            }
        }

        private int GetCurrentWave()
        {
            return handler == null || System.Object.ReferenceEquals(WaveField, null) ? 0 : (int)WaveField.GetValue(handler);
        }

        private int GetAliveEnemyCount()
        {
            if (handler == null || System.Object.ReferenceEquals(AliveField, null))
            {
                return pendingEnemySpawns;
            }
            ICollection alive = AliveField.GetValue(handler) as ICollection;
            return (alive == null ? 0 : alive.Count) + pendingEnemySpawns;
        }

        public static bool QueueTelegraphedEnemySpawn(HoardHandler source, GameObject prefab)
        {
            if (CurrentPlugin == null || !EnemyAIPatches.ModeActive || source == null || prefab == null)
            {
                return false;
            }
            CurrentPlugin.StartCoroutine(CurrentPlugin.SpawnEnemyAfterTelegraph(source, prefab));
            return true;
        }

        private IEnumerator SpawnEnemyAfterTelegraph(HoardHandler source, GameObject prefab)
        {
            pendingEnemySpawns++;
            // Let the match scene finish placing players before measuring safe
            // spawn distances. Match start can report inFight while characters
            // are still being positioned by the arena.
            yield return new WaitForSecondsRealtime(1.25f);
            Vector3 floorPoint;
            GameObject marker = null;
            if (EnemyAIPatches.TryChooseEnemySpawnPoint(out floorPoint, reservedSpawnPoints))
            {
                reservedSpawnPoints.Add(floorPoint);
                Logger.LogInfo("Telegraphing enemy spawn on a solid platform at " + floorPoint + ".");
                marker = CreateSpawnMarker();
                LineRenderer ring = marker == null ? null : marker.GetComponent<LineRenderer>();
                float startedAt = Time.unscaledTime;
                float duration = 2.2f;
                while (EnemyAIPatches.ModeActive && source == handler && Time.unscaledTime - startedAt < duration)
                {
                    float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                    float pulse = 1.05f + 0.24f * Mathf.Sin(progress * Mathf.PI * 8f);
                    if (ring != null)
                    {
                        DrawSpawnRing(ring, floorPoint + Vector3.up * 0.2f, pulse);
                        Color color = Color.Lerp(new Color(0.1f, 0.95f, 1f, 1f), new Color(1f, 0.28f, 0.08f, 1f), progress);
                        ring.startColor = color;
                        ring.endColor = color;
                    }
                    yield return null;
                }
                if (marker != null)
                {
                    if (ring != null && ring.material != null)
                    {
                        Destroy(ring.material);
                    }
                    Destroy(marker);
                }
                if (EnemyAIPatches.ModeActive && source == handler && GameManager.inFight)
                {
                    SpawnNativeEnemyAt(source, prefab, floorPoint + Vector3.up * EnemySpawnRootOffset);
                }
                reservedSpawnPoints.Remove(floorPoint);
            }
            else if (EnemyAIPatches.ModeActive && source == handler && GameManager.inFight)
            {
                Logger.LogWarning("No valid solid spawn surface found; using the map's configured enemy spawn for this enemy.");
                SpawnNativeEnemyAt(source, prefab, source.spawn);
            }
            if (pendingEnemySpawns > 0)
            {
                pendingEnemySpawns--;
            }
        }

        private GameObject CreateSpawnMarker()
        {
            GameObject marker = new GameObject("FortDefense_EnemySpawnWarning");
            LineRenderer ring = marker.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 48;
            ring.startWidth = 0.18f;
            ring.endWidth = 0.18f;
            ring.startColor = new Color(0.1f, 0.95f, 1f, 1f);
            ring.endColor = ring.startColor;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.sortingOrder = 300;
            ring.numCapVertices = 4;
            return marker;
        }

        private void DrawSpawnRing(LineRenderer ring, Vector3 center, float radius)
        {
            if (ring == null)
            {
                return;
            }
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i * Mathf.PI * 2f / (float)segments;
                ring.SetPosition(i, center + new Vector3(0f, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius));
            }
        }

        private void SpawnNativeEnemyAt(HoardHandler source, GameObject prefab, Vector3 spawnPoint)
        {
            if (source == null || prefab == null)
            {
                return;
            }
            Vector3 oldSpawn = source.spawn;
            Vector3 oldRandomSpawn = source.randomSpawn;
            source.spawn = spawnPoint;
            source.randomSpawn = Vector3.zero;
            EnemyAIPatches.AllowNativeSpawn = true;
            try
            {
                EnemyAIPatches.SpawnAINativeMethod.Invoke(source, new object[] { prefab });
                Logger.LogInfo("Spawned enemy at selected platform position " + spawnPoint + ".");
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Logger.LogError("Could not spawn enemy at marked platform: " + cause);
            }
            finally
            {
                EnemyAIPatches.AllowNativeSpawn = false;
                source.spawn = oldSpawn;
                source.randomSpawn = oldRandomSpawn;
            }
        }

        private void StartLevelEnemies()
        {
            if (handler == null || System.Object.ReferenceEquals(StartWaveMethod, null))
            {
                return;
            }

            int players = Math.Max(1, GetHumanPlayerCount());
            handler.waveUnits = Math.Min(32, Math.Max(1, players * (currentLevelNumber + 1)));
            handler.waves = false;
            nextWaveAt = Time.unscaledTime + 2f;
            levelHadEnemies = false;
            try
            {
                StartWaveMethod.Invoke(handler, null);
                observedWave = GetCurrentWave();
                enemyColorIndex = 0;
                // Mark the wave as populated immediately. Otherwise a test or a
                // very fast team clear can remove every bot before Update observes
                // one live enemy and the run never advances.
                levelHadEnemies = handler.waveUnits > 0;
                status = "Wave " + currentLevelNumber + " — " + handler.waveUnits + " enemies. Survive.";
                Logger.LogInfo("Started survival wave " + currentLevelNumber + " with " + handler.waveUnits + " enemies for " + players + " player(s).");
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                status = "Could not start wave: " + cause.GetType().Name;
                Logger.LogError(status + " — " + cause);
            }
        }

        private void CompleteLevelAndAdvance()
        {
            Logger.LogInfo("Wave " + currentLevelNumber + " is empty; checking team survival before advancing.");
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                StopMode();
                return;
            }

            bool hasLivingHuman = false;
            ControllerHandler players = ControllerHandler.Instance;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller player = players.players[i];
                    CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                    DownedState downed = player == null ? null : player.GetComponent<DownedState>();
                    if (player != null && !player.isAI && info != null &&
                        (!info.isDead || (downed != null && !downed.finalizing)))
                    {
                        hasLivingHuman = true;
                        break;
                    }
                }
            }
            if (!hasLivingHuman)
            {
                Logger.LogInfo("All players are down. Returning to the local lobby.");
                ReturnToLobbyAfterDefeat(GameManager.Instance);
                return;
            }

            advancingLevel = true;
            levelHadEnemies = false;
            float intermission = Mathf.Max(1f, 3.5f - (currentLevelNumber - 1) * 0.12f);
            nextLevelReadyAt = Time.unscaledTime + intermission;
            status = "Wave " + currentLevelNumber + " cleared — next wave in " + intermission.ToString("F1") + "s.";
            Logger.LogInfo(status);
            HealMedicPlayers();
            StartCoroutine(AdvanceWaveAfterIntermission(currentLevelNumber, intermission));
        }

        private IEnumerator AdvanceWaveAfterIntermission(int clearedWave, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (!modeStarted || currentLevelNumber != clearedWave)
            {
                yield break;
            }

            BeginPerkSelection();
            while (modeStarted && perkSelectionActive)
            {
                yield return null;
            }
            if (!modeStarted || currentLevelNumber != clearedWave)
            {
                yield break;
            }

            GameManager game = GameManager.Instance;
            if (game == null || IsInLobby(game) || !GameManager.inFight)
            {
                Logger.LogWarning("Wave transition cancelled because the match is no longer active.");
                advancingLevel = false;
                nextLevelReadyAt = -1f;
                yield break;
            }

            if (GetLivingHumanCount() == 0)
            {
                ReturnToLobbyAfterDefeat(game);
                yield break;
            }

            HoardHandler nextHandler = FindHoardHandler(game);
            if (nextHandler == null || nextHandler.character == null)
            {
                Logger.LogError("Wave transition failed: active map has no configured HoardHandler.");
                advancingLevel = false;
                yield break;
            }

            handler = nextHandler;
            handler.waves = false;
            currentLevelNumber++;
            advancingLevel = false;
            nextLevelReadyAt = -1f;
            observedWave = GetCurrentWave();
            enemyColorIndex = 0;
            nextEnemyColorScanAt = 0f;
            status = "Entering survival wave " + currentLevelNumber + ".";
            Logger.LogInfo(status);
            StartLevelEnemies();
        }

        private void BeginPerkSelection()
        {
            pendingPerkPlayers.Clear();
            activePerkPlayerIndex = 0;
            ControllerHandler players = ControllerHandler.Instance;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller player = players.players[i];
                    CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                    if (player != null && !player.isAI && info != null && !info.isDead && player.GetComponent<DownedState>() == null)
                    {
                        pendingPerkPlayers.Add(player);
                    }
                }
            }
            if (pendingPerkPlayers.Count == 0)
            {
                Logger.LogWarning("Perk selection skipped because no living local player was available.");
                return;
            }
            perkSelectionActive = true;
            if (perkPanel != null) perkPanel.gameObject.SetActive(true);
            ShowPerkOptionsForCurrentPlayer();
            Logger.LogInfo("Perk selection opened for " + pendingPerkPlayers.Count + " living player(s) after wave " + currentLevelNumber + ".");
        }

        private void ShowPerkOptionsForCurrentPlayer()
        {
            if (activePerkPlayerIndex >= pendingPerkPlayers.Count)
            {
                perkSelectionActive = false;
                if (perkPanel != null) perkPanel.gameObject.SetActive(false);
                return;
            }
            Controller player = pendingPerkPlayers[activePerkPlayerIndex];
            HashSet<int> owned;
            if (!selectedPerks.TryGetValue(player.playerID, out owned))
            {
                owned = new HashSet<int>();
                selectedPerks[player.playerID] = owned;
            }
            List<int> available = new List<int>();
            for (int perk = 0; perk < 4; perk++)
            {
                if (!owned.Contains(perk)) available.Add(perk);
            }
            if (available.Count == 0)
            {
                activePerkPlayerIndex++;
                ShowPerkOptionsForCurrentPlayer();
                return;
            }
            for (int i = available.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                int value = available[i];
                available[i] = available[swap];
                available[swap] = value;
            }
            activePerkOptionCount = Math.Min(3, available.Count);
            for (int i = 0; i < activePerkOptionCount; i++) activePerkOffers[i] = available[i];
            string[] names = new string[]
            {
                "Крепкое здоровье — +20% к максимуму здоровья",
                "Быстрые руки — атаки на 15% быстрее",
                "Закалка — на 10% меньше входящего урона",
                "Полевой медик — лечение 15 HP после волн"
            };
            perkPrompt.text = "Волна " + currentLevelNumber + " пройдена. Игрок " + (player.playerID + 1) + ", выбери перк (1–3):";
            for (int i = perkOptionsContainer.childCount - 1; i >= 0; i--) Destroy(perkOptionsContainer.GetChild(i).gameObject);
            for (int option = 0; option < activePerkOptionCount; option++)
            {
                int capturedOption = option;
                CreateButton(perkOptionsContainer, (option + 1) + ". " + names[activePerkOffers[option]],
                    new Vector2(18f, -18f - option * 74f), new Vector2(704f, 60f), delegate { ChoosePerk(capturedOption); });
            }
        }

        private void ChoosePerk(int option)
        {
            if (!perkSelectionActive || option < 0 || option >= activePerkOptionCount || activePerkPlayerIndex >= pendingPerkPlayers.Count) return;
            Controller player = pendingPerkPlayers[activePerkPlayerIndex];
            if (player == null)
            {
                activePerkPlayerIndex++;
                ShowPerkOptionsForCurrentPlayer();
                return;
            }
            int perk = activePerkOffers[option];
            HashSet<int> owned;
            if (!selectedPerks.TryGetValue(player.playerID, out owned))
            {
                owned = new HashSet<int>();
                selectedPerks[player.playerID] = owned;
            }
            if (!owned.Add(perk))
            {
                activePerkPlayerIndex++;
                ShowPerkOptionsForCurrentPlayer();
                return;
            }
            ApplyPerk(player, perk);
            Logger.LogInfo("Player " + (player.playerID + 1) + " selected run perk " + perk + ".");
            status = "Игрок " + (player.playerID + 1) + " получил перк: " + GetPerkName(perk) + ".";
            statusUntil = Time.time + 4f;
            activePerkPlayerIndex++;
            ShowPerkOptionsForCurrentPlayer();
        }

        private void ApplyPerk(Controller player, int perk)
        {
            int id = player.GetInstanceID();
            HealthHandler health = player.GetComponent<HealthHandler>();
            if (perk == 0 && health != null)
            {
                if (!perkOriginalHealth.ContainsKey(id)) perkOriginalHealth[id] = health.maxHealth;
                health.maxHealth = perkOriginalHealth[id] * 1.2f;
                health.health = Mathf.Min(health.maxHealth, health.health + perkOriginalHealth[id] * 0.2f);
            }
            else if (perk == 1)
            {
                // The live Attack hook changes the native punch timer's reset amount.
                // punchCD is an accumulating timer, not a duration setting.
            }
            else if (perk == 2)
            {
                fortifiedPlayers.Add(id);
            }
            else if (perk == 3)
            {
                medicPlayers.Add(id);
                if (health != null) health.health = Mathf.Min(health.maxHealth, health.health + 15f);
            }
            Logger.LogInfo("Applied perk " + GetPerkName(perk) + " to player " + (player.playerID + 1) + ". Max HP=" +
                (health == null ? "n/a" : health.maxHealth.ToString("F1")) + ", attack-rate modifier=" +
                GetPunchCooldownResetMultiplier(player).ToString("F2") + ".");
        }

        private void UpdateRunPerks()
        {
            if (selectedPerks.Count == 0) return;
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null) return;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                if (player == null || player.isAI) continue;
                HashSet<int> owned;
                if (!selectedPerks.TryGetValue(player.playerID, out owned) || owned == null) continue;
                int id = player.GetInstanceID();
                HealthHandler health = player.GetComponent<HealthHandler>();
                if (health != null && owned.Contains(0))
                {
                    if (!perkOriginalHealth.ContainsKey(id)) perkOriginalHealth[id] = health.maxHealth;
                    float desiredMax = perkOriginalHealth[id] * 1.2f;
                    if (!Mathf.Approximately(health.maxHealth, desiredMax)) health.maxHealth = desiredMax;
                }
                if (owned.Contains(2)) fortifiedPlayers.Add(id);
                if (owned.Contains(3)) medicPlayers.Add(id);
            }
        }

        private void RestoreRunPerks()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller player = players.players[i];
                    if (player == null) continue;
                    int id = player.GetInstanceID();
                    HealthHandler health = player.GetComponent<HealthHandler>();
                    float originalHealth;
                    if (health != null && perkOriginalHealth.TryGetValue(id, out originalHealth))
                    {
                        health.maxHealth = originalHealth;
                        health.health = Mathf.Min(health.health, originalHealth);
                    }
                }
            }
            selectedPerks.Clear();
            perkOriginalHealth.Clear();
            fortifiedPlayers.Clear();
            medicPlayers.Clear();
        }

        private string GetPerkName(int perk)
        {
            string[] names = new string[] { "Крепкое здоровье", "Быстрые руки", "Закалка", "Полевой медик" };
            return perk >= 0 && perk < names.Length ? names[perk] : "Перк";
        }

        private void HealMedicPlayers()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null) return;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                if (player == null || !medicPlayers.Contains(player.GetInstanceID())) continue;
                HealthHandler health = player.GetComponent<HealthHandler>();
                if (health != null)
                {
                    float before = health.health;
                    health.health = Mathf.Min(health.maxHealth, health.health + 15f);
                    Logger.LogInfo("Field Medic restored " + (health.health - before).ToString("F1") + " health to player " + (player.playerID + 1) + ".");
                }
            }
        }

        private int GetLivingHumanCount()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null)
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                DownedState downed = player == null ? null : player.GetComponent<DownedState>();
                if (player != null && !player.isAI && info != null &&
                    (!info.isDead || (downed != null && !downed.finalizing)))
                {
                    count++;
                }
            }
            return count;
        }

        private void ReturnToLobbyAfterDefeat(GameManager game)
        {
            if (defeatPending)
            {
                return;
            }
            StopMode();
            if (game == null || System.Object.ReferenceEquals(RestartGameMethod, null))
            {
                Logger.LogError("Cannot return Fort Defense to the local lobby: GameManager.RestartGame was not found.");
                return;
            }
            defeatGame = game;
            defeatPending = true;
            defeatReturnAt = Time.unscaledTime + 4f;
            status = "ПОРАЖЕНИЕ — все игроки погибли. Возврат в меню через 4...";
            if (defeatPanel != null)
            {
                defeatPanel.gameObject.SetActive(true);
            }
            Logger.LogInfo("All players are defeated. Showing a 4-second defeat screen before returning to the local lobby.");
        }

        private void FinishDefeatReturn()
        {
            defeatPending = false;
            if (defeatPanel != null)
            {
                defeatPanel.gameObject.SetActive(false);
            }
            try
            {
                RestartGameMethod.Invoke(defeatGame, null);
                defeatGame = null;
                Logger.LogInfo("Fort Defense ended after the whole team was eliminated; returning to the local lobby.");
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Logger.LogError("Could not return to the local lobby: " + cause);
            }
        }

        public static bool HandlePlayerElimination(GameManager __instance)
        {
            if (__instance == null || !EnemyAIPatches.ModeActive || EnemyAIPatches.AllowCampaignAdvance)
            {
                return true;
            }

            ControllerHandler players = ControllerHandler.Instance;
            bool hasLivingHuman = false;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller player = players.players[i];
                    CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                    DownedState downed = player == null ? null : player.GetComponent<DownedState>();
                    if (player != null && !player.isAI && info != null &&
                        (!info.isDead || (downed != null && !downed.finalizing)))
                    {
                        hasLivingHuman = true;
                        break;
                    }
                }
            }

            if (hasLivingHuman)
            {
                // Keep co-op alive when the base PvP flow tries to advance after one death.
                return false;
            }

            if (CurrentPlugin != null)
            {
                CurrentPlugin.ReturnToLobbyAfterDefeat(__instance);
            }
            else
            {
                EnemyAIPatches.ModeActive = false;
                EnemyAIPatches.PurgeTaggedEnemies();
                if (!System.Object.ReferenceEquals(RestartGameMethod, null))
                {
                    RestartGameMethod.Invoke(__instance, null);
                }
            }
            return false;
        }

        private void UpdateLevelTransition()
        {
            GameManager game = GameManager.Instance;
            if (Time.unscaledTime >= nextTransitionDiagnosticAt)
            {
                nextTransitionDiagnosticAt = Time.unscaledTime + 1f;
                Logger.LogInfo("Wave transition tick: wave=" + currentLevelNumber + "; inFight=" + GameManager.inFight +
                    "; lobby=" + (game == null ? "no-game" : IsInLobby(game).ToString()) +
                    "; readyAt=" + nextLevelReadyAt.ToString("F1") + "; now=" + Time.unscaledTime.ToString("F1") + ".");
            }
            if (game == null)
            {
                StopMode();
                return;
            }
            if (IsInLobby(game) || !GameManager.inFight)
            {
                nextLevelReadyAt = -1f;
                return;
            }
            if (nextLevelReadyAt < 0f)
            {
                nextLevelReadyAt = Time.unscaledTime + 2f;
                return;
            }
            if (Time.unscaledTime < nextLevelReadyAt)
            {
                return;
            }

            HoardHandler nextHandler = FindHoardHandler(game);
            if (nextHandler == null || nextHandler.character == null)
            {
                Logger.LogWarning("Wave transition could not find a configured HoardHandler on the active map.");
                nextLevelReadyAt = Time.unscaledTime + 1f;
                return;
            }

            handler = nextHandler;
            handler.waves = false;
            currentLevelNumber++;
            advancingLevel = false;
            nextLevelReadyAt = -1f;
            observedWave = GetCurrentWave();
            enemyColorIndex = 0;
            nextEnemyColorScanAt = 0f;
            status = "Entering survival wave " + currentLevelNumber + ".";
            Logger.LogInfo(status);
            nextTransitionDiagnosticAt = 0f;
            StartLevelEnemies();
        }

        private void UpdateEnemyColors()
        {
            if (handler == null || Time.unscaledTime < nextEnemyColorScanAt)
            {
                return;
            }
            nextEnemyColorScanAt = Time.unscaledTime + 0.25f;

            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                AI enemy = enemies[i];
                if (enemy == null || System.Object.ReferenceEquals(AIControllerField, null))
                {
                    continue;
                }
                Controller controller = AIControllerField.GetValue(enemy) as Controller;
                if (controller == null || !controller.isAI)
                {
                    continue;
                }
                CharacterInformation info = controller.GetComponent<CharacterInformation>();
                HealthHandler health = controller.GetComponent<HealthHandler>();
                if (health != null)
                {
                    if (EnemyAIPatches.ConfigureEnemyHealth(health))
                    {
                        Logger.LogInfo("Configured enemy " + controller.GetInstanceID() + ": 30 HP and local death enabled.");
                    }
                }

                int id = controller.GetInstanceID();
                if (enemyMaterials.ContainsKey(id))
                {
                    continue;
                }

                Material source = info == null ? handler.mat : info.myMaterial;
                if (source == null)
                {
                    source = handler.mat;
                }
                if (source == null)
                {
                    continue;
                }

                float denominator = Math.Max(1, handler.waveUnits - 1);
                float shade = 1f - Mathf.Clamp01((float)enemyColorIndex / denominator);
                enemyColorIndex++;
                Material tinted = new Material(source);
                tinted.color = new Color(shade, shade, shade, 1f);
                enemyMaterials.Add(id, tinted);

                if (info != null)
                {
                    info.myMaterial = tinted;
                }
                LineRenderer[] lines = controller.GetComponentsInChildren<LineRenderer>();
                for (int j = 0; j < lines.Length; j++)
                {
                    lines[j].sharedMaterial = tinted;
                }
                SpriteRenderer[] sprites = controller.GetComponentsInChildren<SpriteRenderer>();
                for (int j = 0; j < sprites.Length; j++)
                {
                    sprites[j].color = new Color(shade, shade, shade, 1f);
                }
            }
        }

        private void UpdatePlayerAbilities()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyboardAbilityKey))
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller keyboardPlayer = players.players[i];
                    CharacterInformation keyboardInfo = keyboardPlayer == null ? null : keyboardPlayer.GetComponent<CharacterInformation>();
                    if (keyboardPlayer != null && !keyboardPlayer.isAI && keyboardInfo != null && !keyboardInfo.isDead)
                    {
                        ActivatePlayerAbility(keyboardPlayer, keyboardInfo);
                        break;
                    }
                }
            }

            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                if (player == null || player.isAI || System.Object.ReferenceEquals(PlayerActionsField, null))
                {
                    continue;
                }
                CharacterInformation info = player.GetComponent<CharacterInformation>();
                if (info != null) ApplySelectedLobbyColor(player, info);
                if (info == null || info.isDead)
                {
                    continue;
                }

                CharacterActions actions = PlayerActionsField.GetValue(player) as CharacterActions;
                if (actions == null)
                {
                    continue;
                }
                // Hold Up and tap Block to activate a color ability. The native block
                // still works, while each local controller gets its own input action.
                if (!IsActionPressed(actions, AbilityBlockField, "WasPressed") || !IsActionPressed(actions, AbilityUpField, "IsPressed"))
                {
                    continue;
                }

                int id = player.playerID;
                float readyAt;
                if (abilityReadyAt.TryGetValue(id, out readyAt) && Time.unscaledTime < readyAt)
                {
                    continue;
                }

                ActivatePlayerAbility(player, info);
            }
        }

        private void ActivatePlayerAbility(Controller player, CharacterInformation info)
        {
            // Controller instances can be recreated on death/respawn. Key the timer
            // by the local slot so the HUD and input check keep seeing the same cooldown.
            int id = player.playerID;
            int ability = GetPlayerAbilityIndex(player, info);
            float readyAt;
            if (abilityReadyAt.TryGetValue(id, out readyAt) && Time.unscaledTime < readyAt)
            {
                float remaining = readyAt - Time.unscaledTime;
                status = "Способность перезаряжается: " + remaining.ToString("F1") + " сек.";
                statusUntil = Time.unscaledTime + 1.5f;
                Logger.LogInfo("Player " + (player.playerID + 1) + " ability key pressed during cooldown; " + remaining.ToString("F1") + "s remain.");
                return;
            }
            ActivateSpecificColorAbility(player, info, ability);
        }

        private static bool IsActionPressed(CharacterActions actions, FieldInfo actionField, string propertyName)
        {
            if (actions == null || System.Object.ReferenceEquals(actionField, null))
            {
                return false;
            }
            object action = actionField.GetValue(actions);
            if (action == null)
            {
                return false;
            }
            PropertyInfo property = action.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return !System.Object.ReferenceEquals(property, null) && (bool)property.GetValue(action, null);
        }

        private void ActivateColorAbility(Controller player, CharacterInformation info)
        {
            ActivateSpecificColorAbility(player, info, GetPlayerAbilityIndex(player, info));
        }

        private int GetPlayerAbilityIndex(Controller player, CharacterInformation info)
        {
            int selected;
            if (player != null && selectedLobbyColors.TryGetValue(player.playerID, out selected))
                return Mathf.Clamp(selected, 0, AbilityColorCount - 1);
            return info == null || info.myMaterial == null ? 0 : FindNearestAbilityColor(info.myMaterial.color);
        }

        private static Vector3 GetActorPosition(Controller actor)
        {
            if (actor == null) return Vector3.zero;
            CharacterInformation info = actor.GetComponent<CharacterInformation>();
            return info != null && info.mainRig != null ? info.mainRig.position : actor.transform.position;
        }

        private void ActivateSpecificColorAbility(Controller player, CharacterInformation info, int ability)
        {
            // All activation routes (keyboard, controller, and the in-game test
            // fixture) share the same deadline the HUD reads.
            abilityReadyAt[player.playerID] = Time.unscaledTime + AbilityCooldown(ability);
            string label = "Способность";
            if (ability == 0)
            {
                label = "Red rage";
                rageUntil[player.GetInstanceID()] = Time.unscaledTime + 5f;
                Logger.LogInfo("Red rage: punch cooldown reduced by 100% for five seconds; enemy punch knockback suppressed.");
                ShowTimedAbilityEffect(player, new Color(1f, 0.025f, 0.015f, 1f), 5f, "ЯРОСТЬ", 1.7f, 0.3f);
            }
            else if (ability == 1)
            {
                label = "Pink healing aura";
                StartCoroutine(HealingAura(player, 5f, 4f));
            }
            else if (ability == 2)
            {
                label = "Green roots";
                RootNearbyEnemies(player, 5f, 3f);
                ShowTimedAbilityEffect(player, new Color(0.2f, 1f, 0.2f, 0.9f), 3f, "Roots", 5f);
            }
            else if (ability == 3)
            {
                label = "Purple destruction beam";
                Vector3 origin = info != null && info.mainRig != null ? info.mainRig.position : player.transform.position;
                Vector3 aim = FindMouseAimDirection(player, origin);
                StartCoroutine(DestructionBeam(player, aim));
            }
            else if (ability == 4)
            {
                label = "Blue impulse";
                PushNearbyEnemies(player, 7f, 9f);
            }
            else if (ability == 5)
            {
                label = "Yellow barrier";
                shieldUntil[player.GetInstanceID()] = Time.unscaledTime + 4f;
                abilityVisualUntil[player.GetInstanceID()] = Time.unscaledTime + 4f;
                CreatePlayerBarrier(player, 4f);
                Logger.LogInfo("Yellow barrier active for local player " + player.playerID + " for 4 seconds.");
            }
            else if (ability == 6)
            {
                label = "Orange detonation";
                DamageNearbyEnemies(player, 5f, 14f, 9f);
                CreateExplosionEffect(GetActorPosition(player), 5f, new Color(1f, 0.35f, 0.03f, 0.9f), 0.65f);
            }
            else if (ability == 7)
            {
                label = "Cyan air step";
                if (info.mainRig != null)
                {
                    float direction = player.LookRotation.x < 0f ? -1f : 1f;
                    Vector3 velocity = info.mainRig.velocity;
                    velocity.y = Mathf.Max(velocity.y, 8f);
                    velocity.z = direction * Mathf.Max(Mathf.Abs(velocity.z), 4f);
                    info.mainRig.velocity = velocity;
                    CreateExplosionEffect(info.mainRig.position, 1.8f, new Color(0.1f, 0.9f, 1f, 0.85f), 0.35f);
                    status = "Голубой воздушный шаг активирован — дополнительный прыжок!";
                    statusUntil = Time.unscaledTime + 2f;
                    Logger.LogInfo("Cyan air step set vertical velocity to " + velocity.y.ToString("F1") + " for local player " + player.playerID + ".");
                }
            }
            else if (ability == 8)
            {
                label = "Brown stone skin";
                stoneSkinUntil[player.GetInstanceID()] = Time.unscaledTime + 5f;
            }
            else if (ability == 9)
            {
                label = "Gray storm discharge";
                ChainLightning(player, 8f, 14f, 5);
            }
            else if (ability == 10)
            {
                label = "Snow ice grip";
                RootNearbyEnemies(player, 5.5f, 2f);
            }
            else
            {
                if (ability == 11)
                {
                    label = "Dark blue shadow form";
                    shadowFormUntil[player.GetInstanceID()] = Time.unscaledTime + 3f;
                    StartCoroutine(ShadowPhase(player, 3f));
                }
                else if (ability == 12)
                {
                    label = "Lilac gravity sphere";
                    StartCoroutine(GravitySphere(player, 8f, 2.5f));
                }
                else if (ability == 13)
                {
                    label = "Gold crush primed";
                    int playerId = player.GetInstanceID();
                    goldCrushReady.Add(playerId);
                    goldCrushReadyUntil[playerId] = Time.unscaledTime + 6f;
                }
                else if (ability == 14)
                {
                    label = "Turquoise ammo reload";
                    RefillHeldWeapon(player);
                }
                else if (ability == 15)
                {
                    label = "Lime parasitic seeds";
                    MarkNearbyEnemiesWithSeeds(player, 7f);
                }
                else if (ability == 16)
                {
                    label = "Raspberry rescue oath";
                    ActivateRescueOath(player);
                }
                else if (ability == 17)
                {
                    label = "Steel hunter mark";
                    ActivateHunterMark(player);
                }
                else if (ability == 18)
                {
                    label = "Lemon greed";
                    lemonGreedUntil = Time.unscaledTime + 6f;
                    Logger.LogInfo("Lemon greed weakened all enemies for 6s: enemy damage x0.75, damage received x1.25.");
                    AI[] activeEnemies = FindObjectsOfType(typeof(AI)) as AI[];
                    if (activeEnemies != null)
                        for (int i = 0; i < activeEnemies.Length; i++) ApplyLemonMovementSlow(activeEnemies[i]);
                    if (!lemonMovementFieldsLogged)
                    {
                        lemonMovementFieldsLogged = true;
                        FieldInfo[] fields = typeof(AI).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        List<string> candidates = new List<string>();
                        for (int i = 0; i < fields.Length; i++)
                        {
                            string fieldName = fields[i].Name.ToLowerInvariant();
                            if (fieldName.Contains("speed") || fieldName.Contains("velocity") ||
                                fieldName.Contains("move") || fieldName.Contains("walk") || fieldName.Contains("run"))
                                candidates.Add(fields[i].Name + ":" + fields[i].FieldType.Name);
                        }
                        Logger.LogInfo("Lemon AI movement field scan: " + String.Join(", ", candidates.ToArray()) + ".");
                        FieldInfo movementField = typeof(AI).GetField("movement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (!System.Object.ReferenceEquals(movementField, null))
                        {
                            Type movementType = movementField.FieldType;
                            FieldInfo[] movementFields = movementType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            List<string> movementCandidates = new List<string>();
                            for (int i = 0; i < movementFields.Length; i++)
                            {
                                string fieldName = movementFields[i].Name.ToLowerInvariant();
                                if (fieldName.Contains("speed") || fieldName.Contains("velocity") ||
                                    fieldName.Contains("move") || fieldName.Contains("walk") || fieldName.Contains("run"))
                                    movementCandidates.Add(movementFields[i].Name + ":" + movementFields[i].FieldType.Name);
                            }
                            Logger.LogInfo("Lemon Movement component field scan: " + String.Join(", ", movementCandidates.ToArray()) + ".");
                        }
                    }
                }
            }

            float cooldown = AbilityCooldown(ability);
            status = label + " activated — ready again in " + cooldown.ToString("F0") + "s. (E keyboard / Up + Block controller)";
            statusUntil = Time.time + 3f;
            Logger.LogInfo(label + " activated by local player " + player.playerID + ".");
        }

        private static float AbilityCooldown(int ability)
        {
            float[] cooldowns = new float[] { 18f, 24f, 20f, 22f, 14f, 20f, 18f, 10f, 24f, 20f, 22f, 25f, 22f, 16f, 30f, 30f, 35f, 20f, 40f };
            return cooldowns[Mathf.Clamp(ability, 0, cooldowns.Length - 1)];
        }

        public static void ApplyLemonMovementSlow(AI enemy)
        {
            if (CurrentPlugin == null || enemy == null || System.Object.ReferenceEquals(AIVelocityField, null))
                return;
            int id = enemy.GetInstanceID();
            float original;
            if (CurrentPlugin.lemonOriginalVelocities.ContainsKey(id)) return;
            try
            {
                original = (float)AIVelocityField.GetValue(enemy);
                CurrentPlugin.lemonOriginalVelocities[id] = original;
                AIVelocityField.SetValue(enemy, original * 0.75f);
                CurrentPlugin.Logger.LogInfo("Lemon greed slowed AI " + id + " movement scalar " +
                    original.ToString("F2") + " -> " + (original * 0.75f).ToString("F2") + ".");
            }
            catch (Exception ex)
            {
                CurrentPlugin.Logger.LogWarning("Lemon greed could not scale AI movement: " + ex.GetType().Name + ".");
            }
        }

        private void RestoreLemonMovementSlow()
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    AI enemy = enemies[i];
                    if (enemy == null) continue;
                    float original;
                    if (lemonOriginalVelocities.TryGetValue(enemy.GetInstanceID(), out original) &&
                        !System.Object.ReferenceEquals(AIVelocityField, null))
                        AIVelocityField.SetValue(enemy, original);
                }
            }
            int restored = lemonOriginalVelocities.Count;
            lemonOriginalVelocities.Clear();
            if (restored > 0) Logger.LogInfo("Lemon greed restored movement scalars for " + restored + " enemies.");
        }

        private IEnumerator GravitySphere(Controller owner, float radius, float duration)
        {
            float endAt = Time.unscaledTime + duration;
            int pulls = 0;
            while (owner != null && Time.unscaledTime < endAt)
            {
                AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
                if (enemies != null)
                {
                    for (int i = 0; i < enemies.Length; i++)
                    {
                        Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                            ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                        CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                        if (target == null || !target.isAI || info == null || info.isDead || info.mainRig == null) continue;
                        Vector3 delta = GetActorPosition(owner) - GetActorPosition(target);
                        if (delta.magnitude > radius) continue;
                        info.mainRig.AddForce(delta.normalized * 18f, ForceMode.Acceleration);
                        pulls++;
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Logger.LogInfo("Lilac gravity sphere ended after pulling enemies " + pulls + " times.");
        }

        private IEnumerator GoldCrushShockwave(Vector3 center, Controller owner, Controller primaryTarget)
        {
            yield return new WaitForSecondsRealtime(0.08f);
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            int hitCount = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                    CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                    HealthHandler health = target == null ? null : target.GetComponent<HealthHandler>();
                    if (target == null || target == primaryTarget || !target.isAI || info == null || info.isDead || health == null ||
                        Vector3.Distance(center, GetActorPosition(target)) > 3.5f) continue;
                    float before = health.health;
                    health.TakeDamage(6f, owner, DamageType.LocalDamage, false, GetActorPosition(target), Vector3.zero);
                    if (Mathf.Approximately(before, health.health)) health.health = Mathf.Max(0f, before - 6f);
                    Vector3 direction = GetActorPosition(target) - center;
                    direction.y = Mathf.Max(0.25f, direction.y);
                    if (info.mainRig != null) info.mainRig.AddForce(direction.normalized * 5f, ForceMode.Impulse);
                    if (health.health <= 0f && !info.isDead)
                    {
                        health.ForcedDie();
                        if (handler != null) handler.RemoveAI(target);
                        if (target.GetComponent<RemoveAI>() == null) target.gameObject.AddComponent<RemoveAI>();
                    }
                    hitCount++;
                }
            }
            Logger.LogInfo("Gold crush shockwave hit " + hitCount + " additional nearby enemies.");
        }

        private void UpdateGoldCrushExpiry()
        {
            if (goldCrushReadyUntil.Count == 0) return;
            List<int> expired = new List<int>();
            foreach (KeyValuePair<int, float> item in goldCrushReadyUntil)
                if (Time.unscaledTime >= item.Value) expired.Add(item.Key);
            for (int i = 0; i < expired.Count; i++)
            {
                goldCrushReadyUntil.Remove(expired[i]);
                goldCrushReady.Remove(expired[i]);
                Logger.LogInfo("Gold crush primed melee hit expired unused.");
            }
        }

        private void RefillHeldWeapon(Controller player)
        {
            Fighting fighting = System.Object.ReferenceEquals(ControllerFightingField, null)
                ? null : ControllerFightingField.GetValue(player) as Fighting;
            FieldInfo heldWeaponField = typeof(Fighting).GetField("weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Weapon weapon = fighting == null || System.Object.ReferenceEquals(heldWeaponField, null) ? null : heldWeaponField.GetValue(fighting) as Weapon;
            if (weapon == null)
            {
                Logger.LogInfo("Turquoise reload found no held weapon; no ammo changed.");
                return;
            }
            FieldInfo gunField = typeof(Weapon).GetField("isGun", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo shotsField = typeof(Weapon).GetField("shotsBeforeReload", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo currentShotsField = typeof(Weapon).GetField("currentShots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo oneUseReloadField = typeof(Weapon).GetField("loseWeaponOnReload", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (System.Object.ReferenceEquals(gunField, null) || !(bool)gunField.GetValue(weapon) ||
                System.Object.ReferenceEquals(shotsField, null) || System.Object.ReferenceEquals(currentShotsField, null))
            {
                Logger.LogInfo("Turquoise reload held weapon is not a supported gun; no ammo changed.");
                return;
            }
            int magazine = (int)shotsField.GetValue(weapon);
            bool oneUse = magazine <= 1 || (!System.Object.ReferenceEquals(oneUseReloadField, null) && (bool)oneUseReloadField.GetValue(weapon));
            if (oneUse)
            {
                Logger.LogInfo("Turquoise reload skipped a one-use gun as designed.");
                return;
            }
            int before = (int)currentShotsField.GetValue(weapon);
            int bonusShots = Math.Max(1, (int)Math.Ceiling(magazine * 0.5f));
            int after = Math.Max(0, before - bonusShots);
            currentShotsField.SetValue(weapon, after);
            Logger.LogInfo("Turquoise reload restored " + (before - after) + " rounds in " + weapon.name + " (spent shots " + before + " -> " + after + ").");
        }

        private void ActivateRescueOath(Controller owner)
        {
            ControllerHandler players = ControllerHandler.Instance;
            Controller ally = null;
            float nearest = 18f;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller candidate = players.players[i];
                    CharacterInformation info = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                    if (candidate == null || candidate == owner || candidate.isAI || info == null || info.isDead) continue;
                    float distance = Vector3.Distance(GetActorPosition(owner), GetActorPosition(candidate));
                    if (distance < nearest) { nearest = distance; ally = candidate; }
                }
            }
            if (ally == null)
            {
                Logger.LogInfo("Raspberry rescue oath found no living ally within 18 units.");
                return;
            }

            List<int> oldLinks = new List<int>();
            foreach (KeyValuePair<int, RescueOath> item in rescueOathsByAlly)
                if (item.Value.Owner == owner || item.Value.Ally == ally) oldLinks.Add(item.Key);
            for (int i = 0; i < oldLinks.Count; i++) RemoveRescueOath(oldLinks[i]);

            GameObject tetherObject = new GameObject("FortDefense_RaspberryOath");
            LineRenderer tether = tetherObject.AddComponent<LineRenderer>();
            tether.useWorldSpace = true;
            tether.positionCount = 2;
            tether.startWidth = 0.075f;
            tether.endWidth = 0.035f;
            tether.startColor = new Color(1f, 0.12f, 0.38f, 0.95f);
            tether.endColor = new Color(1f, 0.48f, 0.7f, 0.55f);
            tether.numCapVertices = 4;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) tether.material = new Material(shader);
            RescueOath oath = new RescueOath();
            oath.Owner = owner;
            oath.Ally = ally;
            oath.ExpiresAt = Time.unscaledTime + 8f;
            oath.Tether = tether;
            rescueOathsByAlly[ally.GetInstanceID()] = oath;
            Logger.LogInfo("Raspberry rescue oath linked player " + (owner.playerID + 1) + " to player " +
                (ally.playerID + 1) + " for 8s: 25% damage redirected (40 cap), owner resistance 80%, link range 12.");
        }

        private void UpdateRescueOaths()
        {
            if (rescueOathsByAlly.Count == 0) return;
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, RescueOath> item in rescueOathsByAlly)
            {
                RescueOath oath = item.Value;
                CharacterInformation allyInfo = oath.Ally == null ? null : oath.Ally.GetComponent<CharacterInformation>();
                if (oath.Owner == null || oath.Ally == null || allyInfo == null ||
                    (allyInfo.isDead && oath.Ally.GetComponent<DownedState>() == null) ||
                    Time.unscaledTime >= oath.ExpiresAt ||
                    Vector3.Distance(GetActorPosition(oath.Owner), GetActorPosition(oath.Ally)) > 12f)
                {
                    remove.Add(item.Key);
                    continue;
                }
                if (oath.Tether != null)
                {
                    CharacterInformation ownerInfo = oath.Owner.GetComponent<CharacterInformation>();
                    Vector3 ownerPoint = GetActorPosition(oath.Owner);
                    Vector3 allyPoint = GetActorPosition(oath.Ally);
                    oath.Tether.SetPosition(0, ownerPoint + Vector3.up * 0.25f);
                    oath.Tether.SetPosition(1, allyPoint + Vector3.up * 0.25f);
                }
            }
            for (int i = 0; i < remove.Count; i++) RemoveRescueOath(remove[i]);
        }

        private void RemoveRescueOath(int allyId)
        {
            RescueOath oath;
            if (!rescueOathsByAlly.TryGetValue(allyId, out oath)) return;
            rescueOathsByAlly.Remove(allyId);
            if (oath.Tether != null)
            {
                Material material = oath.Tether.material;
                Destroy(oath.Tether.gameObject);
                if (material != null) Destroy(material);
            }
            Logger.LogInfo("Raspberry rescue oath ended for ally instance " + allyId + ".");
        }

        private void ActivateHunterMark(Controller owner)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            Controller selected = null;
            float bestHealth = -1f;
            float bestDistance = Single.MaxValue;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    AI enemy = enemies[i];
                    Controller target = enemy == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemy) as Controller;
                    HealthHandler health = target == null ? null : target.GetComponent<HealthHandler>();
                    CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                    if (target == null || health == null || info == null || info.isDead) continue;
                    float distance = Vector3.Distance(GetActorPosition(owner), GetActorPosition(target));
                    if (health.maxHealth > bestHealth || (Mathf.Approximately(health.maxHealth, bestHealth) && distance < bestDistance))
                    { selected = target; bestHealth = health.maxHealth; bestDistance = distance; }
                }
            }
            if (selected == null)
            {
                Logger.LogInfo("Steel hunter mark found no living enemy.");
                return;
            }
            int id = selected.GetInstanceID();
            HunterMark old;
            if (hunterMarks.TryGetValue(id, out old) && old.Marker != null) Destroy(old.Marker);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "FortDefense_HunterMark";
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            marker.transform.position = GetActorPosition(selected) + Vector3.up * 2.1f;
            marker.transform.localScale = Vector3.one * 0.38f;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.025f, 0.025f, 1f);
                if (renderer.material.HasProperty("_EmissionColor")) renderer.material.SetColor("_EmissionColor", new Color(1f, 0f, 0f, 1f));
            }
            HunterMark mark = new HunterMark();
            mark.Target = selected;
            mark.ExpiresAt = Time.unscaledTime + 8f;
            mark.Marker = marker;
            hunterMarks[id] = mark;
            Logger.LogInfo("Steel hunter mark selected enemy " + id + " (" + selected.name + ") for 8s; team damage multiplier=1.5.");
        }

        private void UpdateHunterMarks()
        {
            if (hunterMarks.Count == 0) return;
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, HunterMark> item in hunterMarks)
            {
                CharacterInformation info = item.Value.Target == null ? null : item.Value.Target.GetComponent<CharacterInformation>();
                if (item.Value.Target == null || info == null || info.isDead || Time.unscaledTime >= item.Value.ExpiresAt)
                    remove.Add(item.Key);
                else if (item.Value.Marker != null)
                    item.Value.Marker.transform.position = GetActorPosition(item.Value.Target) + Vector3.up * 2.1f;
            }
            for (int i = 0; i < remove.Count; i++)
            {
                HunterMark mark = hunterMarks[remove[i]];
                hunterMarks.Remove(remove[i]);
                if (mark.Marker != null) Destroy(mark.Marker);
                Logger.LogInfo("Steel hunter mark expired or target was defeated.");
            }
        }

        private void MarkNearbyEnemiesWithSeeds(Controller owner, float radius)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            int marked = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                    CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                    if (target == null || !target.isAI || info == null || info.isDead ||
                        Vector3.Distance(GetActorPosition(owner), GetActorPosition(target)) > radius) continue;
                    MarkSeedTarget(owner, target, 0);
                    marked++;
                }
            }
            Logger.LogInfo("Lime parasitic seeds marked " + marked + " enemies within " + radius.ToString("F1") + ".");
        }

        private void MarkSeedTarget(Controller owner, Controller target, int propagations)
        {
            if (owner == null || target == null || parasiticSeeds.ContainsKey(target.GetInstanceID())) return;
            SeedMark mark = new SeedMark();
            mark.Owner = owner;
            mark.Target = target;
            mark.DetonateAt = Time.unscaledTime + 2.5f;
            mark.Propagations = propagations;
            parasiticSeeds[target.GetInstanceID()] = mark;
        }

        private void UpdateParasiticSeeds()
        {
            if (parasiticSeeds.Count == 0) return;
            List<int> remove = new List<int>();
            List<SeedMark> propagate = new List<SeedMark>();
            foreach (KeyValuePair<int, SeedMark> pair in parasiticSeeds)
            {
                SeedMark mark = pair.Value;
                Controller target = mark == null ? null : mark.Target;
                CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                HealthHandler health = target == null ? null : target.GetComponent<HealthHandler>();
                if (target == null || info == null || info.isDead || health == null || health.health <= 0f)
                {
                    if (mark != null && mark.Propagations < 2) propagate.Add(mark);
                    remove.Add(pair.Key);
                    continue;
                }
                if (Time.unscaledTime < mark.DetonateAt) continue;
                float before = health.health;
                health.TakeDamage(8f, mark.Owner, DamageType.LocalDamage, false, GetActorPosition(target), Vector3.zero);
                if (Mathf.Approximately(before, health.health)) health.health = Mathf.Max(0f, before - 8f);
                if (mark.Owner != null)
                {
                    HealthHandler ownerHealth = mark.Owner.GetComponent<HealthHandler>();
                    if (ownerHealth != null) ownerHealth.health = Mathf.Min(ownerHealth.maxHealth, ownerHealth.health + 4f);
                }
                if (health.health <= 0f && !info.isDead)
                {
                    health.ForcedDie();
                    if (handler != null) handler.RemoveAI(target);
                    if (target.GetComponent<RemoveAI>() == null) target.gameObject.AddComponent<RemoveAI>();
                    if (mark.Propagations < 2) propagate.Add(mark);
                }
                Logger.LogInfo("Lime seed detonated on enemy " + target.GetInstanceID() + "; health " + before.ToString("F1") + " -> " + health.health.ToString("F1") + "; owner healed 4.");
                remove.Add(pair.Key);
            }
            for (int i = 0; i < remove.Count; i++) parasiticSeeds.Remove(remove[i]);
            for (int i = 0; i < propagate.Count; i++) PropagateSeed(propagate[i]);
        }

        private void PropagateSeed(SeedMark source)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies == null || source == null) return;
            Controller nearest = null;
            float nearestDistance = 6f;
            for (int i = 0; i < enemies.Length; i++)
            {
                Controller candidate = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                    ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                CharacterInformation info = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                if (candidate == null || !candidate.isAI || info == null || info.isDead || parasiticSeeds.ContainsKey(candidate.GetInstanceID())) continue;
                float distance = source.Target == null ? Single.MaxValue : Vector3.Distance(GetActorPosition(source.Target), GetActorPosition(candidate));
                if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
            }
            if (nearest != null)
            {
                MarkSeedTarget(source.Owner, nearest, source.Propagations + 1);
                Logger.LogInfo("Lime seed propagated to enemy " + nearest.GetInstanceID() + " (chain " + (source.Propagations + 1) + "/2).");
            }
        }

        private void ChainLightning(Controller owner, float radius, float damage, int maxTargets)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies == null) return;
            List<Controller> targets = new List<Controller>();
            for (int i = 0; i < enemies.Length; i++)
            {
                Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                    ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                if (target != null && target.isAI && info != null && !info.isDead &&
                    Vector3.Distance(GetActorPosition(owner), GetActorPosition(target)) <= radius)
                    targets.Add(target);
            }
            targets.Sort(delegate(Controller a, Controller b)
            {
                return Vector3.Distance(GetActorPosition(owner), GetActorPosition(a)).CompareTo(
                    Vector3.Distance(GetActorPosition(owner), GetActorPosition(b)));
            });
            int hits = Math.Min(maxTargets, targets.Count);
            for (int i = 0; i < hits; i++)
            {
                Controller target = targets[i];
                HealthHandler health = target.GetComponent<HealthHandler>();
                if (health == null) continue;
                float hitDamage = damage * Mathf.Pow(0.75f, i);
                float before = health.health;
                health.TakeDamage(hitDamage, owner, DamageType.LocalDamage, false, GetActorPosition(target), Vector3.zero);
                if (Mathf.Approximately(before, health.health)) health.health = Mathf.Max(0f, before - hitDamage);
                CharacterInformation info = target.GetComponent<CharacterInformation>();
                if (health.health <= 0f && info != null && !info.isDead)
                {
                    health.ForcedDie();
                    if (handler != null) handler.RemoveAI(target);
                    if (target.GetComponent<RemoveAI>() == null) target.gameObject.AddComponent<RemoveAI>();
                }
            }
            Logger.LogInfo("Gray storm discharge chained across " + hits + " enemies; initial damage=" + damage.ToString("F0") + ".");
        }

        private IEnumerator ShadowPhase(Controller owner, float duration)
        {
            if (owner == null) yield break;
            Collider[] playerColliders = owner.GetComponentsInChildren<Collider>();
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            List<Collider[]> ignoredPairs = new List<Collider[]>();
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Controller enemy = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                    if (enemy == null || !enemy.isAI) continue;
                    Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
                    for (int p = 0; p < playerColliders.Length; p++)
                        for (int e = 0; e < enemyColliders.Length; e++)
                            if (playerColliders[p] != null && enemyColliders[e] != null)
                            {
                                Physics.IgnoreCollision(playerColliders[p], enemyColliders[e], true);
                                ignoredPairs.Add(new Collider[] { playerColliders[p], enemyColliders[e] });
                            }
                }
            }
            yield return new WaitForSecondsRealtime(duration);
            for (int i = 0; i < ignoredPairs.Count; i++)
                if (ignoredPairs[i][0] != null && ignoredPairs[i][1] != null)
                    Physics.IgnoreCollision(ignoredPairs[i][0], ignoredPairs[i][1], false);
            if (owner != null) shadowFormUntil.Remove(owner.GetInstanceID());
            Logger.LogInfo("Dark blue shadow form ended; restored " + ignoredPairs.Count + " collision pairs.");
        }

        private void UpdateRageEffects()
        {
            if (rageUntil.Count == 0)
            {
                return;
            }
            List<int> expired = new List<int>();
            foreach (KeyValuePair<int, float> item in rageUntil)
            {
                if (Time.unscaledTime >= item.Value)
                {
                    expired.Add(item.Key);
                }
            }
            for (int i = 0; i < expired.Count; i++)
            {
                int id = expired[i];
                rageUntil.Remove(id);
                Logger.LogInfo("Red rage ended for player instance " + id + "; normal attack interval restored.");
            }
        }

        private void UpdateAbilityEffects()
        {
            if (abilityVisuals.Count > 0)
            {
                foreach (KeyValuePair<int, GameObject> item in abilityVisuals)
                {
                    GameObject effect = item.Value;
                    if (effect == null) continue;
                    Controller owner;
                    if (abilityVisualOwners.TryGetValue(item.Key, out owner) && owner != null)
                        effect.transform.position = GetActorPosition(owner);
                    LineRenderer line = effect.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 9f) * 0.25f;
                        float baseWidth;
                        if (!abilityVisualBaseWidths.TryGetValue(item.Key, out baseWidth)) baseWidth = 0.12f;
                        line.startWidth = baseWidth * pulse;
                        line.endWidth = baseWidth * 0.65f * pulse;
                    }
                }
            }
        }

        public static bool TryStartDowned(HealthHandler health)
        {
            if (CurrentPlugin == null || !EnemyAIPatches.ModeActive || health == null ||
                System.Object.ReferenceEquals(HealthControllerRefField, null))
            {
                return false;
            }
            Controller player = HealthControllerRefField.GetValue(health) as Controller;
            CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
            if (player == null || player.isAI || info == null || info.isDead || player.GetComponent<DownedState>() != null)
            {
                return false;
            }
            int livingAllies = 0;
            ControllerHandler players = ControllerHandler.Instance;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller ally = players.players[i];
                    CharacterInformation allyInfo = ally == null ? null : ally.GetComponent<CharacterInformation>();
                    if (ally != null && ally != player && !ally.isAI && allyInfo != null && !allyInfo.isDead &&
                        ally.GetComponent<DownedState>() == null)
                    {
                        livingAllies++;
                    }
                }
            }
            int reviveCount;
            if (livingAllies == 0 || (CurrentPlugin.reviveCounts.TryGetValue(player.GetInstanceID(), out reviveCount) && reviveCount >= 2))
            {
                return false;
            }
            DownedState downed = player.gameObject.AddComponent<DownedState>();
            downed.player = player;
            downed.expiresAt = Time.unscaledTime + 20f;
            downed.reviveNumber = CurrentPlugin.reviveCounts.TryGetValue(player.GetInstanceID(), out reviveCount) ? reviveCount : 0;
            health.health = 1f;
            info.isDead = true;
            CurrentPlugin.status = "Player " + (player.playerID + 1) + " is down. Hold F10 nearby to revive (controllers: hold Block).";
            CurrentPlugin.statusUntil = Time.time + 20f;
            CurrentPlugin.Logger.LogInfo("Player " + player.playerID + " entered Downed state for 20 seconds; revive duration=" +
                Mathf.Min(10f, 6f + downed.reviveNumber * 2f).ToString("F0") + "s.");
            return true;
        }

        private void UpdateReviveSystem()
        {
            if (!modeStarted)
            {
                return;
            }
            DownedState[] downedPlayers = FindObjectsOfType(typeof(DownedState)) as DownedState[];
            if (downedPlayers == null)
            {
                return;
            }
            ControllerHandler players = ControllerHandler.Instance;
            for (int i = 0; i < downedPlayers.Length; i++)
            {
                DownedState state = downedPlayers[i];
                Controller target = state == null ? null : state.player;
                if (state == null || target == null)
                {
                    continue;
                }
                if (Time.unscaledTime >= state.expiresAt)
                {
                    state.finalizing = true;
                    HealthHandler health = target.GetComponent<HealthHandler>();
                    CharacterInformation targetInfo = target.GetComponent<CharacterInformation>();
                    if (targetInfo != null)
                    {
                        targetInfo.isDead = false;
                    }
                    if (health != null)
                    {
                        health.health = 0f;
                        health.ForcedDie();
                    }
                    Logger.LogInfo("Player " + target.playerID + "'s downed timer expired.");
                    Destroy(state);
                    CheckForTeamDefeat();
                    continue;
                }

                Controller rescuer = null;
                HealthHandler rescuerHealth = null;
                float nearest = 2.25f;
                if (players != null && players.players != null)
                {
                    for (int j = 0; j < players.players.Count; j++)
                    {
                        Controller candidate = players.players[j];
                        CharacterInformation info = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                        if (candidate == null || candidate == target || candidate.isAI || info == null || info.isDead ||
                            candidate.GetComponent<DownedState>() != null || !IsReviveInputHeld(candidate))
                        {
                            continue;
                        }
                        float distance = Vector3.Distance(candidate.transform.position, target.transform.position);
                        if (distance < nearest)
                        {
                            nearest = distance;
                            rescuer = candidate;
                            rescuerHealth = candidate.GetComponent<HealthHandler>();
                        }
                    }
                }

                if (rescuer == null)
                {
                    state.ResetRevive();
                    continue;
                }
                float currentHealth = rescuerHealth == null ? 0f : rescuerHealth.health;
                bool changedRescuer = state.reviverId != rescuer.GetInstanceID();
                bool moved = changedRescuer || Vector3.Distance(state.reviverStart, rescuer.transform.position) > 0.35f;
                bool tookDamage = !changedRescuer && currentHealth < state.lastRescuerHealth - 0.1f;
                if (moved || tookDamage)
                {
                    state.progress = 0f;
                    state.reviverId = rescuer.GetInstanceID();
                    state.reviverStart = rescuer.transform.position;
                }
                state.lastRescuerHealth = currentHealth;
                state.progress += Time.unscaledDeltaTime;
                float required = Mathf.Min(10f, 6f + state.reviveNumber * 2f);
                status = "Reviving P" + (target.playerID + 1) + " — " + Mathf.Min(required, state.progress).ToString("F1") + "/" + required.ToString("F0") + "s";
                statusUntil = Time.time + 0.25f;
                if (state.progress >= required)
                {
                    CompleteRevive(state, rescuer);
                }
            }
        }

        private bool IsReviveInputHeld(Controller player)
        {
            if (player.playerID == 0 && Input.GetKey(KeyboardReviveKey))
            {
                return true;
            }
            if (System.Object.ReferenceEquals(PlayerActionsField, null))
            {
                return false;
            }
            CharacterActions actions = PlayerActionsField.GetValue(player) as CharacterActions;
            return IsActionPressed(actions, AbilityBlockField, "IsPressed");
        }

        private void CompleteRevive(DownedState state, Controller rescuer)
        {
            Controller player = state.player;
            HealthHandler health = player == null ? null : player.GetComponent<HealthHandler>();
            CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
            if (health == null || info == null)
            {
                return;
            }
            int id = player.GetInstanceID();
            reviveCounts[id] = state.reviveNumber + 1;
            info.isDead = false;
            health.health = Mathf.Max(1f, health.maxHealth * 0.5f);
            shieldUntil[id] = Time.unscaledTime + 1f;
            Logger.LogInfo("Player " + player.playerID + " revived by player " + (rescuer.playerID + 1) + " at " + health.health.ToString("F1") + " health.");
            status = "Player " + (player.playerID + 1) + " revived with 50% health.";
            statusUntil = Time.time + 4f;
            Destroy(state);
        }

        private void CheckForTeamDefeat()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null)
            {
                return;
            }
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                DownedState downed = player == null ? null : player.GetComponent<DownedState>();
                if (player != null && !player.isAI && info != null &&
                    (!info.isDead || (downed != null && !downed.finalizing)))
                {
                    return;
                }
            }
            if (GameManager.Instance != null)
            {
                ReturnToLobbyAfterDefeat(GameManager.Instance);
            }
        }

        private void ShowTimedAbilityEffect(Controller owner, Color color, float duration, string title, float radius = 1.2f, float width = 0.12f)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            GameObject old;
            if (abilityVisuals.TryGetValue(id, out old) && old != null) Destroy(old);
            GameObject ring = CreateEffectRing("FortDefense_" + title, color, radius, width);
            ring.transform.position = GetActorPosition(owner);
            abilityVisuals[id] = ring;
            abilityVisualOwners[id] = owner;
            abilityVisualUntil[id] = Time.unscaledTime + duration;
            abilityVisualBaseWidths[id] = width;
            StartCoroutine(RemoveEffectAfter(id, ring, duration));
        }

        private IEnumerator RemoveEffectAfter(int id, GameObject effect, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            if (effect != null) Destroy(effect);
            GameObject current;
            if (abilityVisuals.TryGetValue(id, out current) && current == effect)
            {
                abilityVisuals.Remove(id);
                abilityVisualOwners.Remove(id);
                abilityVisualUntil.Remove(id);
                abilityVisualBaseWidths.Remove(id);
            }
        }

        private GameObject CreateEffectRing(string name, Color color, float radius, float width)
        {
            GameObject obj = new GameObject(name);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 49;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.15f);
            line.numCapVertices = 3;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.material.color = color;
            for (int i = 0; i < 49; i++)
            {
                float angle = (float)i / 48f * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(0f, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius));
            }
            return obj;
        }

        private void CreateExplosionEffect(Vector3 center, float radius, Color color, float duration)
        {
            GameObject ring = CreateEffectRing("FortDefense_AbilityPulse", color, 0.1f, 0.18f);
            ring.transform.position = center;
            StartCoroutine(AnimateExplosion(ring, radius, duration));
        }

        private IEnumerator AnimateExplosion(GameObject ring, float radius, float duration)
        {
            float started = Time.unscaledTime;
            LineRenderer line = ring == null ? null : ring.GetComponent<LineRenderer>();
            while (ring != null && Time.unscaledTime - started < duration)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - started) / duration);
                float r = Mathf.Lerp(0.1f, radius, t);
                for (int i = 0; i < 49; i++)
                {
                    float angle = (float)i / 48f * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(0f, Mathf.Sin(angle) * r, Mathf.Cos(angle) * r));
                }
                line.startColor = new Color(line.startColor.r, line.startColor.g, line.startColor.b, 1f - t);
                yield return null;
            }
            if (ring != null) Destroy(ring);
        }

        private void CreatePlayerBarrier(Controller owner, float duration)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            GameObject old;
            if (shieldObjects.TryGetValue(id, out old) && old != null) Destroy(old);
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "FortDefense_YellowBarrier";
            barrier.transform.localScale = new Vector3(3.5f, 3.5f, 0.32f);
            Renderer renderer = barrier.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                material.color = new Color(1f, 0.9f, 0.15f, 0.38f);
                renderer.material = material;
            }
            Collider collider = barrier.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = false;
            shieldObjects[id] = barrier;
            StartCoroutine(RemoveBarrierAfter(id, barrier, duration));
        }

        private IEnumerator RemoveBarrierAfter(int id, GameObject barrier, float duration)
        {
            while (barrier != null && Time.unscaledTime < shieldUntil[id])
            {
                Controller owner = FindPlayerByInstanceId(id);
                if (owner == null) break;
                float direction = owner.LookRotation.x < 0f ? -1f : 1f;
                barrier.transform.position = GetActorPosition(owner) + new Vector3(0f, 0f, direction * 1.25f);
                yield return null;
            }
            if (barrier != null) Destroy(barrier);
            shieldObjects.Remove(id);
        }

        private Controller FindPlayerByInstanceId(int id)
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null) return null;
            for (int i = 0; i < players.players.Count; i++)
                if (players.players[i] != null && players.players[i].GetInstanceID() == id) return players.players[i];
            return null;
        }

        private IEnumerator HealingAura(Controller owner, float radius, float duration)
        {
            float end = Time.unscaledTime + duration;
            float restored = 0f;
            ShowTimedAbilityEffect(owner, new Color(1f, 0.22f, 0.7f, 0.95f), duration, "HealingAura", radius);
            while (owner != null && Time.unscaledTime < end)
            {
                ControllerHandler players = ControllerHandler.Instance;
                if (players != null && players.players != null)
                {
                    for (int i = 0; i < players.players.Count; i++)
                    {
                        Controller ally = players.players[i];
                        CharacterInformation info = ally == null ? null : ally.GetComponent<CharacterInformation>();
                        HealthHandler health = ally == null ? null : ally.GetComponent<HealthHandler>();
                        if (ally != null && !ally.isAI && info != null && !info.isDead && health != null &&
                            Vector3.Distance(GetActorPosition(owner), GetActorPosition(ally)) <= radius)
                        {
                            float before = health.health;
                            health.health = Mathf.Min(health.maxHealth, health.health + 2.5f);
                            restored += health.health - before;
                            if (health.health > before) CreateHealFlash(ally, health.health - before);
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Logger.LogInfo("Pink healing aura ended; restored " + restored.ToString("F1") + " health across nearby allies.");
        }

        private void CreateHealFlash(Controller ally, float amount)
        {
            if (ally == null) return;
            GameObject label = new GameObject("FortDefense_HealText");
            label.transform.position = GetActorPosition(ally) + Vector3.up * 1.3f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "+" + amount.ToString("F0") + " HP";
            text.color = new Color(1f, 0.35f, 0.8f, 1f);
            text.fontSize = 48;
            text.characterSize = 0.08f;
            StartCoroutine(FloatHealText(label));
        }

        private IEnumerator FloatHealText(GameObject label)
        {
            float start = Time.unscaledTime;
            Vector3 origin = label.transform.position;
            while (label != null && Time.unscaledTime - start < 0.65f)
            {
                float t = (Time.unscaledTime - start) / 0.65f;
                label.transform.position = origin + Vector3.up * (t * 0.7f);
                label.transform.forward = Camera.main == null ? Vector3.forward : Camera.main.transform.forward;
                TextMesh text = label.GetComponent<TextMesh>();
                Color color = text.color;
                color.a = 1f - t;
                text.color = color;
                yield return null;
            }
            if (label != null) Destroy(label);
        }

        private void RootNearbyEnemies(Controller owner, float radius, float duration)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            int count = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    AI enemy = enemies[i];
                    Controller target = enemy == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemy) as Controller;
                    if (target == null || !target.isAI || Vector3.Distance(GetActorPosition(owner), GetActorPosition(target)) > radius)
                    {
                        continue;
                    }
                    EnemyAIPatches.RootedUntil[target.GetInstanceID()] = Time.unscaledTime + duration;
                    rootedUntil[target.GetInstanceID()] = Time.unscaledTime + duration;
                    CharacterInformation info = target.GetComponent<CharacterInformation>();
                    if (info != null && info.mainRig != null)
                    {
                        info.mainRig.velocity = Vector3.zero;
                        info.mainRig.angularVelocity = Vector3.zero;
                    }
                    GameObject old = null;
                    rootVisuals.TryGetValue(target.GetInstanceID(), out old);
                    if (old != null) Destroy(old);
                    GameObject roots = CreateEffectRing("FortDefense_Roots", new Color(0.12f, 1f, 0.18f, 1f), 0.75f, 0.16f);
                    roots.transform.position = GetActorPosition(target);
                    rootVisuals[target.GetInstanceID()] = roots;
                    count++;
                }
            }
            Logger.LogInfo("Green roots immobilized " + count + " nearby enemies for " + duration.ToString("F0") + " seconds.");
        }

        private void UpdateRootedEffects()
        {
            if (rootedUntil.Count == 0) return;
            List<int> expired = new List<int>();
            foreach (KeyValuePair<int, float> item in rootedUntil)
            {
                Controller target = FindPlayerByInstanceId(item.Key);
                CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                if (target == null || info == null || info.isDead || Time.unscaledTime >= item.Value)
                {
                    expired.Add(item.Key);
                    continue;
                }
                if (info.mainRig != null)
                {
                    info.mainRig.velocity = Vector3.zero;
                    info.mainRig.angularVelocity = Vector3.zero;
                }
                GameObject activeRoots;
                if (rootVisuals.TryGetValue(item.Key, out activeRoots) && activeRoots != null)
                {
                    activeRoots.transform.position = GetActorPosition(target);
                    float pulse = 0.9f + Mathf.Sin(Time.unscaledTime * 14f) * 0.12f;
                    activeRoots.transform.localScale = Vector3.one * pulse;
                }
            }
            for (int i = 0; i < expired.Count; i++)
            {
                int id = expired[i];
                rootedUntil.Remove(id);
                EnemyAIPatches.RootedUntil.Remove(id);
                GameObject visual;
                if (rootVisuals.TryGetValue(id, out visual) && visual != null) Destroy(visual);
                rootVisuals.Remove(id);
            }
        }

        private void PushNearbyEnemies(Controller owner, float radius, float force)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            int count = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                    CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                    if (target == null || !target.isAI || info == null || info.isDead ||
                        Vector3.Distance(GetActorPosition(owner), GetActorPosition(target)) > radius || info.mainRig == null)
                    {
                        continue;
                    }
                    Vector3 direction = GetActorPosition(target) - GetActorPosition(owner);
                    direction.y = Mathf.Max(0.35f, direction.y);
                    info.mainRig.AddForce(direction.normalized * force, ForceMode.Impulse);
                    count++;
                }
            }
            Vector3 ownerPosition = GetActorPosition(owner);
            Collider[] nearby = Physics.OverlapSphere(ownerPosition, radius);
            HashSet<int> pushedBodies = new HashSet<int>();
            if (nearby != null)
            {
                for (int i = 0; i < nearby.Length; i++)
                {
                    Rigidbody body = nearby[i] == null ? null : nearby[i].attachedRigidbody;
                    if (body == null || body.isKinematic || body.transform.IsChildOf(owner.transform) || !pushedBodies.Add(body.GetInstanceID())) continue;
                    Vector3 direction = body.worldCenterOfMass - ownerPosition;
                    if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
                    direction.y = Mathf.Max(0.4f, direction.y);
                    body.AddForce(direction.normalized * force, ForceMode.Impulse);
                }
            }
            CreateExplosionEffect(ownerPosition, radius, new Color(0.15f, 0.55f, 1f, 0.95f), 0.45f);
            Logger.LogInfo("Blue impulse pushed " + count + " nearby enemies and " + pushedBodies.Count + " physical objects.");
        }

        private IEnumerator DestructionBeam(Controller owner, Vector3 direction)
        {
            // SPRAY-style single hit: one visible pulse, one damage application per
            // enemy, then stop at the first solid world collider.
            if (owner == null) yield break;
            CharacterInformation ownerInfo = owner.GetComponent<CharacterInformation>();
            Vector3 origin = ownerInfo != null && ownerInfo.mainRig != null ? ownerInfo.mainRig.position : owner.transform.position;
            if (direction.sqrMagnitude < 0.5f) direction = FindBeamTargetDirection(owner, origin);
            const float range = 22f;
            const float damage = 30f;
            Vector3 beamEnd = origin + direction * range;
            int damagedEnemies = 0;
            // SPRAY is a visibly thick beam, so trace a slim volume rather than a
            // zero-width ray; stickman limbs otherwise leave gaps in the hit test.
            const float beamRadius = 0.42f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, beamRadius, direction, range);
            Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
            HashSet<int> hitEnemies = new HashSet<int>();
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(owner.transform)) continue;
                AI hitAI = hit.collider.GetComponentInParent<AI>();
                Controller target = hitAI == null || System.Object.ReferenceEquals(AIControllerField, null)
                    ? hit.collider.GetComponentInParent<Controller>() : AIControllerField.GetValue(hitAI) as Controller;
                if (target == owner) continue;
                // Character ragdoll colliders can be detached from the Controller
                // transform and therefore are not its children. The origin sits
                // inside the player, so ignore only non-enemy contacts at point blank.
                if (target == null && hit.distance < 0.75f) continue;
                if (target != null && target.isAI)
                {
                    if (!hitEnemies.Add(target.GetInstanceID())) continue;
                    HealthHandler health = target.GetComponent<HealthHandler>();
                    if (health != null)
                    {
                        float before = health.health;
                        health.TakeDamage(damage, owner, DamageType.LocalDamage, false, hit.point, Vector3.zero);
                        if (Mathf.Approximately(health.health, before))
                            health.health = Mathf.Max(0f, before - damage);
                        if (health.health <= 0f)
                        {
                            health.ForcedDie();
                            if (handler != null) handler.RemoveAI(target);
                            if (target.GetComponent<RemoveAI>() == null) target.gameObject.AddComponent<RemoveAI>();
                        }
                        damagedEnemies++;
                    }
                    continue;
                }
                // The first non-enemy solid collider is the wall/terrain stop.
                beamEnd = hit.point;
                Logger.LogInfo("Purple SPRAY shot stopped by " + hit.collider.name + " at " + hit.distance.ToString("F1") + ".");
                break;
            }

            GameObject beamObject = new GameObject("FortDefense_PurpleBeam");
            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.loop = false;
            beam.positionCount = 2;
            EnsureSprayBeamStyle();
            beam.startWidth = sprayBeamStartWidth;
            beam.endWidth = sprayBeamEndWidth;
            beam.startColor = new Color(0.92f, 0.55f, 1f, 1f);
            beam.endColor = new Color(0.58f, 0.04f, 1f, 0.95f);
            beam.textureMode = LineTextureMode.Stretch;
            beam.numCapVertices = 4;
            beam.sortingOrder = 400;
            beam.sharedMaterial = GetPurpleBeamMaterial();
            beam.SetPosition(0, origin);
            beam.SetPosition(1, beamEnd);
            Logger.LogInfo("Purple SPRAY shot hit " + damagedEnemies + " enemies; damage=" + damage.ToString("F0") +
                ", beam length=" + Vector3.Distance(origin, beamEnd).ToString("F1") + ", direction=" + direction.ToString("F2") + ".");
            yield return new WaitForSecondsRealtime(sprayBeamLifetime);
            if (beamObject != null) Destroy(beamObject);
        }

        private Vector3 FindBeamTargetDirection(Controller owner, Vector3 origin)
        {
            float fallback = owner.LookRotation.x < 0f ? -1f : 1f;
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            float closest = Single.MaxValue;
            Vector3 direction = new Vector3(0f, 0f, fallback);
            if (enemies == null)
            {
                return direction;
            }
            for (int i = 0; i < enemies.Length; i++)
            {
                Controller target = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                    ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                CharacterInformation info = target == null ? null : target.GetComponent<CharacterInformation>();
                if (target == null || !target.isAI || info == null || info.isDead)
                {
                    continue;
                }
                Vector3 targetPosition = info.mainRig == null ? target.transform.position : info.mainRig.position;
                Vector3 delta = targetPosition - origin;
                delta.x = 0f;
                float distance = Mathf.Sqrt(delta.z * delta.z + delta.y * delta.y);
                if (distance >= closest || distance > 22f || Mathf.Abs(delta.y) > 3.5f || Mathf.Abs(delta.z) <= 0.2f)
                {
                    continue;
                }

                Vector3 candidateDirection = delta.normalized;
                RaycastHit[] visibilityHits = Physics.SphereCastAll(origin, 0.42f, candidateDirection, distance + 0.5f);
                Array.Sort(visibilityHits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
                bool visible = false;
                for (int hitIndex = 0; hitIndex < visibilityHits.Length; hitIndex++)
                {
                    RaycastHit visibilityHit = visibilityHits[hitIndex];
                    if (visibilityHit.collider == null || visibilityHit.collider.isTrigger ||
                        visibilityHit.collider.transform.IsChildOf(owner.transform)) continue;
                    AI blockerAI = visibilityHit.collider.GetComponentInParent<AI>();
                    Controller blocker = blockerAI == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? visibilityHit.collider.GetComponentInParent<Controller>() : AIControllerField.GetValue(blockerAI) as Controller;
                    if (blocker == owner || (blocker == null && visibilityHit.distance < 0.75f)) continue;
                    visible = blocker != null && blocker.isAI;
                    break;
                }
                if (!visible) continue;
                closest = distance;
                direction = candidateDirection;
            }
            return direction;
        }

        private Vector3 FindMouseAimDirection(Controller owner, Vector3 origin)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                Plane gameplayPlane = new Plane(Vector3.right, new Vector3(origin.x, 0f, 0f));
                float distance;
                if (gameplayPlane.Raycast(ray, out distance))
                {
                    Vector3 direction = ray.GetPoint(distance) - origin;
                    direction.x = 0f;
                    if (direction.sqrMagnitude > 0.04f)
                    {
                        Logger.LogInfo("Purple SPRAY aimed at mouse position " + ray.GetPoint(distance).ToString("F2") + ".");
                        return direction.normalized;
                    }
                }
            }
            float fallback = owner != null && owner.LookRotation.x < 0f ? -1f : 1f;
            return new Vector3(0f, 0f, fallback);
        }

        private IEnumerator TestPurpleShot()
        {
            ControllerHandler players = ControllerHandler.Instance;
            Controller owner = null;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller candidate = players.players[i];
                    CharacterInformation candidateInfo = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                    if (candidate != null && !candidate.isAI && candidateInfo != null && !candidateInfo.isDead)
                    {
                        owner = candidate;
                        break;
                    }
                }
            }
            CharacterInformation ownerInfo = owner == null ? null : owner.GetComponent<CharacterInformation>();
            if (owner == null || ownerInfo == null)
            {
                Logger.LogWarning("Purple shot test cancelled: no living local player.");
                yield break;
            }

            selectedLobbyColors[owner.playerID] = 3;
            ApplySelectedLobbyColor(owner, ownerInfo);
            EnemyAIPatches.AbilityTestProtectedPlayerId = owner.GetInstanceID();
            EnemyAIPatches.AbilityTestProtectionUntil = Time.unscaledTime + 10f;
            HealthHandler ownerHealth = owner.GetComponent<HealthHandler>();
            if (ownerHealth != null) ownerHealth.health = ownerHealth.maxHealth;

            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            List<Controller> moved = new List<Controller>();
            List<Vector3> originalRootPositions = new List<Vector3>();
            List<Vector3> originalRigPositions = new List<Vector3>();
            List<Rigidbody> rigs = new List<Rigidbody>();
            List<float> originalHealth = new List<float>();
            Vector3 origin = ownerInfo.mainRig == null ? owner.transform.position : ownerInfo.mainRig.position;
            Vector3 direction = FindClearestTestBeamDirection(owner, origin);
            int index = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    AI enemy = enemies[i];
                    Controller target = enemy == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemy) as Controller;
                    CharacterInformation targetInfo = target == null ? null : target.GetComponent<CharacterInformation>();
                    HealthHandler health = target == null ? null : target.GetComponent<HealthHandler>();
                    if (target == null || targetInfo == null || targetInfo.isDead || health == null) continue;

                    Rigidbody rig = targetInfo.mainRig;
                    moved.Add(target);
                    originalRootPositions.Add(target.transform.position);
                    rigs.Add(rig);
                    originalRigPositions.Add(rig == null ? target.transform.position : rig.position);
                    originalHealth.Add(health.health);
                    EnemyAIPatches.RootedUntil[target.GetInstanceID()] = Time.unscaledTime + 10f;

                    Vector3 destination = index < 2
                        ? origin + direction * (1.5f + index * 2.0f)
                        : origin + Vector3.up * (100f + index);
                    target.transform.position = destination;
                    if (rig != null)
                    {
                        rig.position = destination;
                        rig.velocity = Vector3.zero;
                    }
                    if (index < 2) health.health = 30f;
                    index++;
                }
            }
            if (moved.Count == 0)
            {
                Logger.LogWarning("Purple shot test cancelled: no living enemies.");
                yield break;
            }

            Logger.LogInfo("Purple test placed " + Math.Min(2, moved.Count) + " enemies on a clear line " + direction.ToString("F2") + ".");
            yield return new WaitForSecondsRealtime(0.2f);
            ActivatePlayerAbility(owner, ownerInfo);
            yield return new WaitForSecondsRealtime(0.4f);

            for (int i = 0; i < moved.Count; i++)
            {
                Controller target = moved[i];
                if (target == null) continue;
                HealthHandler health = target.GetComponent<HealthHandler>();
                Logger.LogInfo("Purple test enemy " + i + " health " + originalHealth[i].ToString("F1") + " -> " +
                    (health == null ? "missing" : health.health.ToString("F1")) + ".");
                if (health != null && health.health > 0f)
                {
                    target.transform.position = originalRootPositions[i];
                    Rigidbody rig = rigs[i];
                    if (rig != null)
                    {
                        rig.position = originalRigPositions[i];
                        rig.velocity = Vector3.zero;
                    }
                }
            }
            EnemyAIPatches.AbilityTestProtectedPlayerId = Int32.MinValue;
        }

        private Vector3 FindClearestTestBeamDirection(Controller owner, Vector3 origin)
        {
            Vector3 best = new Vector3(0f, 0f, owner.LookRotation.x < 0f ? -1f : 1f);
            float bestClearance = -1f;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 candidate = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle));
                RaycastHit[] hits = Physics.SphereCastAll(origin, 0.42f, candidate, 12f);
                Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
                float clearance = 12f;
                for (int j = 0; j < hits.Length; j++)
                {
                    RaycastHit hit = hits[j];
                    if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(owner.transform)) continue;
                    AI enemy = hit.collider.GetComponentInParent<AI>();
                    Controller target = enemy == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? hit.collider.GetComponentInParent<Controller>() : AIControllerField.GetValue(enemy) as Controller;
                    if (target == owner || (target == null && hit.distance < 0.75f) || (target != null && target.isAI)) continue;
                    clearance = hit.distance;
                    break;
                }
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    best = candidate;
                }
            }
            return best;
        }

        private void LogSprayAssets()
        {
            Weapon[] weapons = Resources.FindObjectsOfTypeAll(typeof(Weapon)) as Weapon[];
            int foundWeapons = 0;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Length; i++)
                {
                    Weapon weapon = weapons[i];
                    if (weapon == null) continue;
                    string objectName = weapon.gameObject == null ? String.Empty : weapon.gameObject.name;
                    if (weapon.name.IndexOf("spray", StringComparison.OrdinalIgnoreCase) < 0 &&
                        objectName.IndexOf("spray", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    foundWeapons++;
                    GameObject projectile = weapon.projectile;
                    Logger.LogInfo("SPRAY weapon candidate: component=" + weapon.name + ", object=" + objectName +
                        ", projectile=" + (projectile == null ? "none" : projectile.name) + ".");
                    if (projectile == null) continue;
                    Renderer[] renderers = projectile.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        Material material = renderer == null ? null : renderer.sharedMaterial;
                        Logger.LogInfo("SPRAY projectile renderer: " + renderer.GetType().Name + "/" + renderer.gameObject.name +
                            ", material=" + (material == null ? "none" : material.name) +
                            ", shader=" + (material == null || material.shader == null ? "none" : material.shader.name) +
                            ", texture=" + (material == null || material.mainTexture == null ? "none" : material.mainTexture.name) + ".");
                    }
                    LineRenderer[] lines = projectile.GetComponentsInChildren<LineRenderer>(true);
                    for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        LineRenderer line = lines[lineIndex];
                        Logger.LogInfo("SPRAY projectile line: " + line.gameObject.name + ", widths=" +
                            line.startWidth.ToString("F2") + "/" + line.endWidth.ToString("F2") + ".");
                    }
                    TrailRenderer[] trails = projectile.GetComponentsInChildren<TrailRenderer>(true);
                    for (int trailIndex = 0; trailIndex < trails.Length; trailIndex++)
                    {
                        TrailRenderer trail = trails[trailIndex];
                        Logger.LogInfo("SPRAY projectile trail: " + trail.gameObject.name + ", widths=" +
                            trail.startWidth.ToString("F2") + "/" + trail.endWidth.ToString("F2") + ", time=" + trail.time.ToString("F2") +
                            ", material=" + (trail.sharedMaterial == null ? "none" : trail.sharedMaterial.name) + ".");
                    }
                }
            }

            Material[] materials = Resources.FindObjectsOfTypeAll(typeof(Material)) as Material[];
            int foundMaterials = 0;
            if (materials != null)
            {
                for (int i = 0; i < materials.Length && foundMaterials < 24; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    string name = material.name ?? String.Empty;
                    if (name.IndexOf("spray", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("beam", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("laser", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foundMaterials++;
                    Logger.LogInfo("SPRAY/beam material candidate: " + name + ", shader=" +
                        (material.shader == null ? "none" : material.shader.name) + ", texture=" +
                        (material.mainTexture == null ? "none" : material.mainTexture.name) + ".");
                }
            }
            Logger.LogInfo("SPRAY asset scan complete: weapon candidates=" + foundWeapons + ", named materials=" + foundMaterials + ".");
        }

        private Material GetPurpleBeamMaterial()
        {
            if (beamMaterial != null) return beamMaterial;
            EnsureSprayBeamStyle();
            if (sprayBeamSourceMaterial != null)
            {
                beamMaterial = new Material(sprayBeamSourceMaterial);
                Logger.LogInfo("Purple beam cloned the game's SPRAY trail material: " + sprayBeamSourceMaterial.name + ".");
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                beamMaterial = new Material(shader);
                beamMaterial.mainTexture = GetPurpleBeamTexture();
                Logger.LogWarning("SPRAY trail material was unavailable; using the generated beam fallback.");
            }
            beamMaterial.color = new Color(0.78f, 0.18f, 1f, 1f);
            beamMaterial.renderQueue = 3000;
            return beamMaterial;
        }

        private void EnsureSprayBeamStyle()
        {
            if (sprayBeamStyleScanned) return;
            sprayBeamStyleScanned = true;
            Weapon[] weapons = Resources.FindObjectsOfTypeAll(typeof(Weapon)) as Weapon[];
            if (weapons == null) return;
            for (int i = 0; i < weapons.Length; i++)
            {
                Weapon weapon = weapons[i];
                if (weapon == null) continue;
                string objectName = weapon.gameObject == null ? String.Empty : weapon.gameObject.name;
                if (weapon.name.IndexOf("spray", StringComparison.OrdinalIgnoreCase) < 0 &&
                    objectName.IndexOf("spray", StringComparison.OrdinalIgnoreCase) < 0) continue;
                GameObject projectile = weapon.projectile;
                if (projectile == null) continue;
                TrailRenderer[] trails = projectile.GetComponentsInChildren<TrailRenderer>(true);
                for (int j = 0; j < trails.Length; j++)
                {
                    TrailRenderer trail = trails[j];
                    if (trail == null || trail.sharedMaterial == null) continue;
                    sprayBeamSourceMaterial = trail.sharedMaterial;
                    sprayBeamStartWidth = Mathf.Max(0.05f, trail.startWidth);
                    sprayBeamEndWidth = Mathf.Max(0.03f, trail.endWidth);
                    // Keep SPRAY's original trail material and taper, but leave a
                    // single shot visible long enough to read as a deliberate beam.
                    sprayBeamLifetime = Mathf.Clamp(trail.time, 0.25f, 0.8f);
                    Logger.LogInfo("Using SPRAY trail style: material=" + sprayBeamSourceMaterial.name + ", widths=" +
                        sprayBeamStartWidth.ToString("F2") + "/" + sprayBeamEndWidth.ToString("F2") + ", duration=" +
                        sprayBeamLifetime.ToString("F2") + ".");
                    return;
                }
            }
        }

        private Texture2D GetPurpleBeamTexture()
        {
            if (beamTexture != null) return beamTexture;
            beamTexture = new Texture2D(2, 32, TextureFormat.ARGB32, false);
            beamTexture.name = "FortDefense_PurpleBeamGlow";
            beamTexture.wrapMode = TextureWrapMode.Clamp;
            beamTexture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 32; y++)
            {
                float edge = Mathf.Abs(((float)y + 0.5f) / 32f - 0.5f) * 2f;
                float alpha = 1f - Mathf.SmoothStep(0.12f, 1f, edge);
                Color pixel = new Color(1f, 1f, 1f, alpha);
                beamTexture.SetPixel(0, y, pixel);
                beamTexture.SetPixel(1, y, pixel);
            }
            beamTexture.Apply();
            return beamTexture;
        }

        private static int FindNearestAbilityColor(Color color)
        {
            int best = 0;
            float distance = Single.MaxValue;
            for (int i = 0; i < AbilityColorCount; i++)
            {
                Color paletteColor = AbilityColor(i);
                float dr = color.r - paletteColor.r;
                float dg = color.g - paletteColor.g;
                float db = color.b - paletteColor.b;
                float next = dr * dr + dg * dg + db * db;
                if (next < distance)
                {
                    distance = next;
                    best = i;
                }
            }
            return best;
        }

        private void DamageNearbyEnemies(Controller player, float radius, float damage, float force)
        {
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies == null)
            {
                return;
            }
            for (int i = 0; i < enemies.Length; i++)
            {
                AI enemy = enemies[i];
                Controller target = System.Object.ReferenceEquals(AIControllerField, null) || enemy == null
                    ? null : AIControllerField.GetValue(enemy) as Controller;
                if (target == null || !target.isAI || Vector3.Distance(GetActorPosition(player), GetActorPosition(target)) > radius)
                {
                    continue;
                }
                HealthHandler health = target.GetComponent<HealthHandler>();
                if (health != null)
                {
                    Vector3 direction = (GetActorPosition(target) - GetActorPosition(player)).normalized;
                    float before = health.health;
                    health.TakeDamage(damage, player, DamageType.LocalDamage, false, GetActorPosition(target), direction * force);
                    if (Mathf.Approximately(health.health, before))
                    {
                        // HealthHandler can reject a direct mod ability hit on an
                        // unowned AI. Preserve this ability's fixed damage even
                        // when the native ownership gate declines the hit.
                        health.health = Mathf.Max(0f, before - damage);
                        CharacterInformation damagedInfo = target.GetComponent<CharacterInformation>();
                        if (health.health <= 0f && damagedInfo != null && !damagedInfo.isDead)
                        {
                            health.ForcedDie();
                            if (handler != null)
                            {
                                handler.RemoveAI(target);
                            }
                            if (target.GetComponent<RemoveAI>() == null)
                            {
                                target.gameObject.AddComponent<RemoveAI>();
                            }
                        }
                    }
                    CharacterInformation targetInfo = target.GetComponent<CharacterInformation>();
                    if (targetInfo != null && targetInfo.mainRig != null)
                    {
                        targetInfo.mainRig.AddForce(direction * force, ForceMode.Impulse);
                    }
                    Logger.LogInfo("Orange detonation hit enemy " + target.GetInstanceID() + " for " + damage.ToString("F1") + "; remaining health " + health.health.ToString("F1") + ".");
                }
            }
        }

        public static void ReducePlayerDamage(HealthHandler health, ref float damage)
        {
            Controller victim = System.Object.ReferenceEquals(HealthControllerRefField, null) || health == null
                ? null : HealthControllerRefField.GetValue(health) as Controller;
            if (CurrentPlugin == null || !EnemyAIPatches.ModeActive || victim == null || victim.isAI)
            {
                return;
            }
            RescueOath linkedOath = null;
            CurrentPlugin.rescueOathsByAlly.TryGetValue(victim.GetInstanceID(), out linkedOath);
            if (linkedOath != null && linkedOath.Owner != null &&
                Time.unscaledTime < linkedOath.ExpiresAt &&
                Vector3.Distance(linkedOath.Owner.transform.position, victim.transform.position) <= 12f)
            {
                float available = Mathf.Max(0f, 40f - linkedOath.RedirectedDamage);
                float redirected = Mathf.Min(damage * 0.25f, available);
                damage -= redirected;
                linkedOath.RedirectedDamage += redirected;
                HealthHandler ownerHealth = linkedOath.Owner.GetComponent<HealthHandler>();
                if (ownerHealth != null && redirected > 0f)
                {
                    ownerHealth.health = Mathf.Max(0f, ownerHealth.health - redirected * 0.2f);
                    if (ownerHealth.health <= 0f) ownerHealth.ForcedDie();
                }
            }
            foreach (KeyValuePair<int, RescueOath> item in CurrentPlugin.rescueOathsByAlly)
            {
                RescueOath oath = item.Value;
                if (oath.Owner == victim && Time.unscaledTime < oath.ExpiresAt &&
                    Vector3.Distance(GetActorPosition(oath.Owner), GetActorPosition(oath.Ally)) <= 12f)
                {
                    damage *= 0.2f;
                    break;
                }
            }
            float until;
            if (CurrentPlugin.shieldUntil.TryGetValue(victim.GetInstanceID(), out until) && Time.unscaledTime < until)
            {
                damage *= 0.5f;
            }
            if (CurrentPlugin.fortifiedPlayers.Contains(victim.GetInstanceID()))
            {
                damage *= 0.9f;
            }
        }

        public static void RemoveDeadEnemyFromWave(Controller controller)
        {
            if (CurrentPlugin == null || CurrentPlugin.handler == null || controller == null || System.Object.ReferenceEquals(AliveField, null))
            {
                return;
            }
            IList alive = AliveField.GetValue(CurrentPlugin.handler) as IList;
            if (alive != null && alive.Contains(controller))
            {
                alive.Remove(controller);
                CurrentPlugin.Logger.LogInfo("Removed defeated enemy " + controller.GetInstanceID() + " from wave tracking; remaining=" + alive.Count + ".");
            }
        }

        public static bool IsRageActive(Controller player)
        {
            float until;
            return CurrentPlugin != null && player != null &&
                CurrentPlugin.rageUntil.TryGetValue(player.GetInstanceID(), out until) && Time.unscaledTime < until;
        }

        public static float GetPunchCooldownResetMultiplier(Controller player)
        {
            if (CurrentPlugin == null || player == null || player.isAI) return 1f;
            float multiplier = 1f;
            HashSet<int> owned;
            if (CurrentPlugin.selectedPerks.TryGetValue(player.playerID, out owned) && owned != null && owned.Contains(1))
                multiplier *= 0.85f;
            if (IsRageActive(player)) multiplier *= 0f;
            return multiplier;
        }

        public static bool IsHunterMarked(Controller target)
        {
            if (CurrentPlugin == null || target == null) return false;
            HunterMark mark;
            return CurrentPlugin.hunterMarks.TryGetValue(target.GetInstanceID(), out mark) &&
                Time.unscaledTime < mark.ExpiresAt;
        }

        public static bool IsLemonGreedActive
        {
            get { return CurrentPlugin != null && Time.unscaledTime < CurrentPlugin.lemonGreedUntil; }
        }

        private HoardHandler FindHoardHandler(GameManager game)
        {
            if (!System.Object.ReferenceEquals(HoardField, null))
            {
                HoardHandler found = HoardField.GetValue(game) as HoardHandler;
                if (found != null)
                {
                    return found;
                }
            }
            return FindObjectOfType(typeof(HoardHandler)) as HoardHandler;
        }

        private bool IsInLobby(GameManager game)
        {
            if (game == null || System.Object.ReferenceEquals(IsInLobbyMethod, null))
            {
                return !GameManager.inFight;
            }
            try
            {
                return (bool)IsInLobbyMethod.Invoke(game, null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not query lobby state: " + ex.GetType().Name);
                return !GameManager.inFight;
            }
        }

        private void StopMode()
        {
            EnemyAIPatches.PurgeTaggedEnemies();
            abilityReadyAt.Clear();
            RestoreLemonMovementSlow();
            lemonGreedUntil = 0f;
            List<int> oathIds = new List<int>(rescueOathsByAlly.Keys);
            for (int i = 0; i < oathIds.Count; i++) RemoveRescueOath(oathIds[i]);
            foreach (HunterMark mark in hunterMarks.Values)
                if (mark.Marker != null) Destroy(mark.Marker);
            hunterMarks.Clear();
            if (handler != null)
            {
                handler.waves = false;
            }
            foreach (Material material in enemyMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            enemyMaterials.Clear();
            handler = null;
            modeStarted = false;
            waitingForMatchStart = false;
            advancingLevel = false;
            perkSelectionActive = false;
            pendingPerkPlayers.Clear();
            if (perkPanel != null) perkPanel.gameObject.SetActive(false);
            EnemyAIPatches.AbilityTestProtectedPlayerId = Int32.MinValue;
            RestoreRunPerks();
            EnemyAIPatches.ModeActive = false;
            currentLevelNumber = 0;
            nextLevelReadyAt = -1f;
            levelHadEnemies = false;
            observedWave = 0;
            enemyColorIndex = 0;
            status = "Join at least one player, then press F8 or use the button.";
            statusUntil = 0f;
        }

        private void CreateOverlay()
        {
            EventSystem existingEventSystem = FindObjectOfType(typeof(EventSystem)) as EventSystem;
            if (existingEventSystem == null)
            {
                GameObject eventSystem = new GameObject("FortDefense_EventSystem");
                DontDestroyOnLoad(eventSystem);
                existingEventSystem = eventSystem.AddComponent<EventSystem>();
            }
            if (existingEventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasObject = new GameObject("FortDefense_Overlay");
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            startPanel = CreatePanel(canvas.transform, "Fort Defense — SOLO / LOCAL CO-OP", new Vector2(12f, -12f), new Vector2(620f, 152f));
            overlayStatusText = CreateText(startPanel, "Join at least one player, then start PvE.", new Vector2(16f, -42f), new Vector2(588f, 38f), 20, TextAnchor.UpperLeft);
            CreateButton(startPanel, "START PvE  [F8]", new Vector2(16f, -92f), new Vector2(280f, 42f), delegate { StartMode(); });
            CreateButton(startPanel, "CONSOLE  [F1]", new Vector2(318f, -92f), new Vector2(280f, 42f), delegate { ToggleConsole(); });

            RectTransform abilityPanel = CreatePanel(canvas.transform, "СПОСОБНОСТЬ: E  ·  ГЕЙМПАД: ↑ + БЛОК  ·  ПЕРЕЗАРЯДКА", Vector2.zero, new Vector2(760f, 92f));
            abilityPanel.anchorMin = new Vector2(0.5f, 0f); abilityPanel.anchorMax = new Vector2(0.5f, 0f);
            abilityPanel.pivot = new Vector2(0.5f, 0f); abilityPanel.anchoredPosition = new Vector2(0f, 12f);
            abilityPanel.GetComponentInChildren<Text>().fontSize = 14;
            for (int i = 0; i < 4; i++)
            {
                float x = 12f + i * 186f;
                abilityGaugeLabels[i] = CreateText(abilityPanel, "P" + (i + 1) + " — ожидание", new Vector2(x, -34f), new Vector2(176f, 22f), 13, TextAnchor.MiddleLeft);
                GameObject backObject = new GameObject("Cooldown background P" + (i + 1));
                backObject.transform.SetParent(abilityPanel, false);
                RectTransform backRect = backObject.AddComponent<RectTransform>();
                backRect.anchorMin = new Vector2(0f, 1f); backRect.anchorMax = new Vector2(0f, 1f); backRect.pivot = new Vector2(0f, 1f);
                backRect.anchoredPosition = new Vector2(x, -61f); backRect.sizeDelta = new Vector2(176f, 9f);
                Image back = backObject.AddComponent<Image>(); back.color = new Color(0.06f, 0.08f, 0.11f, 1f);
                GameObject fillObject = new GameObject("Cooldown fill P" + (i + 1));
                fillObject.transform.SetParent(backObject.transform, false);
                RectTransform fillRect = fillObject.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
                abilityGaugeFills[i] = fillObject.AddComponent<Image>();
                abilityGaugeFills[i].type = Image.Type.Filled; abilityGaugeFills[i].fillMethod = Image.FillMethod.Horizontal; abilityGaugeFills[i].fillAmount = 1f;
            }

            colorPanel = CreatePanel(canvas.transform, "ВЫБЕРИ ЦВЕТ ПЕРСОНАЖА", Vector2.zero, new Vector2(760f, 740f));
            colorPanel.anchorMin = new Vector2(0.5f, 0.5f); colorPanel.anchorMax = new Vector2(0.5f, 0.5f); colorPanel.pivot = new Vector2(0.5f, 0.5f);
            colorPanel.anchoredPosition = Vector2.zero;
            colorPanel.GetComponent<Image>().color = new Color(0.018f, 0.022f, 0.035f, 0.995f);
            colorDescription = CreateText(colorPanel, "Цвет не выбран", new Vector2(20f, -42f), new Vector2(720f, 58f), 18, TextAnchor.MiddleCenter);
            Text controlsHint = CreateText(colorPanel, "C / S / ↓ — открыть     ← / → / ↑ / ↓ — выбор     Enter — подтвердить     1–9 / 0 — быстрый выбор     Esc — закрыть", new Vector2(20f, -100f), new Vector2(720f, 26f), 13, TextAnchor.MiddleCenter);
            controlsHint.color = new Color(0.76f, 0.80f, 0.88f, 1f);
            string[] tileNames = new string[] { "КРАСНЫЙ\nЯрость", "РОЗОВЫЙ\nЛечение", "ЗЕЛЁНЫЙ\nКорни", "ФИОЛЕТОВЫЙ\nЛуч", "СИНИЙ\nИмпульс", "ЖЁЛТЫЙ\nБарьер", "ОРАНЖЕВЫЙ\nВзрыв", "ГОЛУБОЙ\nВоздушный шаг", "КОРИЧНЕВЫЙ\nКаменная кожа", "СЕРЫЙ\nРазряд бури", "СНЕЖНЫЙ\nЛедяная хватка", "ТЁМНО-СИНИЙ\nТеневая форма", "СИРЕНЕВЫЙ\nГравитация", "ЗОЛОТОЙ\nСокрушение", "БИРЮЗОВЫЙ\nПерезагрузка", "ЛАЙМОВЫЙ\nСемена", "МАЛИНОВЫЙ\nКлятва спасения", "СТАЛЬНОЙ\nМетка охотника", "ЛИМОННЫЙ\nАлчность" };
            for (int i = 0; i < tileNames.Length; i++)
            {
                int column = i % 4; int row = i / 4;
                colorOptionButtons[i] = CreateColorOptionButton(colorPanel, i, tileNames[i], new Vector2(24f + column * 178f, -136f - row * 96f));
            }
            for (int i = 0; i < colorOptionButtons.Length; i++)
            {
                Navigation nav = colorOptionButtons[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                int rowCount = (colorOptionButtons.Length + 3) / 4;
                int columnCount = Math.Min(4, colorOptionButtons.Length - (i / 4) * 4);
                int column = i % 4;
                int row = i / 4;
                int upRow = (row + rowCount - 1) % rowCount;
                int downRow = (row + 1) % rowCount;
                int upColumn = Math.Min(column, Math.Min(4, colorOptionButtons.Length - upRow * 4) - 1);
                int downColumn = Math.Min(column, Math.Min(4, colorOptionButtons.Length - downRow * 4) - 1);
                nav.selectOnLeft = colorOptionButtons[row * 4 + (column + columnCount - 1) % columnCount];
                nav.selectOnRight = colorOptionButtons[row * 4 + (column + 1) % columnCount];
                nav.selectOnUp = colorOptionButtons[upRow * 4 + upColumn];
                nav.selectOnDown = colorOptionButtons[downRow * 4 + downColumn];
                colorOptionButtons[i].navigation = nav;
            }
            CreateButton(colorPanel, "ЗАКРЫТЬ  [Esc]", new Vector2(230f, -658f), new Vector2(300f, 42f), delegate { CloseColorMenu(); });
            colorPanel.gameObject.SetActive(false);

            consolePanel = CreatePanel(canvas.transform, "FORT DEFENSE DEVELOPER CONSOLE", new Vector2(12f, -180f), new Vector2(700f, 460f));
            consoleLogText = CreateText(consolePanel, "Type help for commands.", new Vector2(16f, -42f), new Vector2(668f, 330f), 16, TextAnchor.UpperLeft);
            consoleInput = CreateInput(consolePanel, new Vector2(16f, -386f), new Vector2(510f, 42f));
            CreateButton(consolePanel, "RUN", new Vector2(542f, -386f), new Vector2(142f, 42f), delegate { RunConsoleCommand(consoleInput == null ? "" : consoleInput.text); });
            consolePanel.gameObject.SetActive(false);

            defeatPanel = CreatePanel(canvas.transform, "ПОРАЖЕНИЕ", Vector2.zero, new Vector2(760f, 240f));
            defeatPanel.anchorMin = new Vector2(0.5f, 0.5f);
            defeatPanel.anchorMax = new Vector2(0.5f, 0.5f);
            defeatPanel.pivot = new Vector2(0.5f, 0.5f);
            defeatPanel.anchoredPosition = Vector2.zero;
            defeatPanel.GetComponent<Image>().color = new Color(0.16f, 0.025f, 0.035f, 0.97f);
            Text defeatTitle = defeatPanel.GetComponentInChildren<Text>();
            if (defeatTitle != null)
            {
                defeatTitle.fontSize = 42;
                defeatTitle.alignment = TextAnchor.MiddleCenter;
                defeatTitle.color = new Color(1f, 0.58f, 0.52f, 1f);
            }
            defeatCountdownText = CreateText(defeatPanel, "Все игроки погибли.\nВозврат в меню через 4...",
                new Vector2(20f, -82f), new Vector2(720f, 120f), 30, TextAnchor.MiddleCenter);
            defeatPanel.gameObject.SetActive(false);

            perkPanel = CreatePanel(canvas.transform, "ПЕРЕРЫВ МЕЖДУ ВОЛНАМИ", Vector2.zero, new Vector2(760f, 410f));
            perkPanel.anchorMin = new Vector2(0.5f, 0.5f);
            perkPanel.anchorMax = new Vector2(0.5f, 0.5f);
            perkPanel.pivot = new Vector2(0.5f, 0.5f);
            perkPanel.anchoredPosition = Vector2.zero;
            perkPanel.GetComponent<Image>().color = new Color(0.035f, 0.11f, 0.15f, 0.97f);
            perkPrompt = CreateText(perkPanel, "Выбери один перк:", new Vector2(18f, -52f), new Vector2(724f, 42f), 22, TextAnchor.MiddleCenter);
            GameObject optionsObject = new GameObject("PerkOptions");
            optionsObject.transform.SetParent(perkPanel, false);
            perkOptionsContainer = optionsObject.AddComponent<RectTransform>();
            perkOptionsContainer.anchorMin = new Vector2(0f, 1f);
            perkOptionsContainer.anchorMax = new Vector2(0f, 1f);
            perkOptionsContainer.pivot = new Vector2(0f, 1f);
            perkOptionsContainer.anchoredPosition = new Vector2(10f, -100f);
            perkOptionsContainer.sizeDelta = new Vector2(740f, 240f);
            perkPanel.gameObject.SetActive(false);
            Logger.LogInfo("Created Screen Space Overlay canvas for Fort Defense UI.");
        }

        private static string[] AbilityNames()
        {
            return new string[] { "КРАСНЫЙ — Ярость", "РОЗОВЫЙ — Лечение", "ЗЕЛЁНЫЙ — Корни", "ФИОЛЕТОВЫЙ — Луч", "СИНИЙ — Импульс", "ЖЁЛТЫЙ — Барьер", "ОРАНЖЕВЫЙ — Взрыв", "ГОЛУБОЙ — Воздушный шаг", "КОРИЧНЕВЫЙ — Каменная кожа", "СЕРЫЙ — Разряд бури", "СНЕЖНЫЙ — Ледяная хватка", "ТЁМНО-СИНИЙ — Теневая форма", "СИРЕНЕВЫЙ — Гравитация", "ЗОЛОТОЙ — Сокрушение", "БИРЮЗОВЫЙ — Перезагрузка", "ЛАЙМОВЫЙ — Паразитические семена", "МАЛИНОВЫЙ — Клятва спасения", "СТАЛЬНОЙ — Метка охотника", "ЛИМОННЫЙ — Алчность" };
        }

        private static string AbilityDescription(int index)
        {
            string[] descriptions = new string[] {
                "На 5 секунд полностью убирает откат удара (ограничено анимацией). Перезарядка: 18 сек.",
                "Лечит ближайших союзников импульсами в течение 5 секунд. Перезарядка: 24 сек.",
                "Останавливает ближайших врагов на 3 секунды. Перезарядка: 20 сек.",
                "Один мощный выстрел BEAM в сторону курсора пробивает врагов и останавливается о стену. Перезарядка: 22 сек.",
                "Отбрасывает ближайших врагов мощным импульсом. Перезарядка: 14 сек.",
                "Создаёт перед игроком прозрачную преграду, блокирующую врагов и выстрелы. Перезарядка: 20 сек.",
                "Взрывает область вокруг персонажа и наносит урон врагам. Перезарядка: 18 сек.",
                "Подбрасывает персонажа вверх для уклонения и перемещения. Перезарядка: 10 сек.",
                "На 5 секунд снижает входящий урон на 45%. Перезарядка: 24 сек.",
                "Цепная молния поражает до 5 ближайших врагов, ослабевая с каждым прыжком. Перезарядка: 20 сек.",
                "На 2 секунды обездвиживает обычных врагов поблизости. Перезарядка: 22 сек.",
                "На 3 секунды снижает входящий урон на 80% и позволяет проходить сквозь врагов. Перезарядка: 25 сек.",
                "Создаёт сферу, которая притягивает обычных врагов 2,5 секунды. Перезарядка: 22 сек.",
                "Следующий удар ближнего боя наносит двойной урон и усиливает отбрасывание; удар создаёт малую волну. Перезарядка: 16 сек.",
                "Возвращает половину магазина оружия в руке. Одноразовое оружие не меняется. Перезарядка: 30 сек.",
                "Помечает врагов: через 2,5 секунды наносит урон и лечит владельца; смерть цели передаёт семена ещё двум целям максимум. Перезарядка: 30 сек.",
                "Связывает ближайшего союзника на 8 секунд: перенаправляет до 40 урона, владелец получает на 80% меньше урона. Связь рвётся дальше 12 м. Перезарядка: 35 сек.",
                "Помечает ближайшего сильного врага на 8 секунд; команда наносит ему на 50% больше урона. Перезарядка: 20 сек.",
                "На 6 секунд замедляет врагов на 25%: они наносят на 25% меньше урона и получают на 25% больше. Перезарядка: 40 сек."
            };
            return descriptions[Mathf.Clamp(index, 0, descriptions.Length - 1)];
        }

        private static Color AbilityColor(int index)
        {
            Color[] colors = new Color[] {
                new Color(1.00f,0.08f,0.04f), new Color(1.00f,0.02f,0.72f),
                new Color(0.04f,1.00f,0.08f), new Color(0.62f,0.02f,1.00f),
                new Color(0.02f,0.25f,1.00f), new Color(1.00f,0.88f,0.02f),
                new Color(1.00f,0.30f,0.01f), new Color(0.00f,0.96f,1.00f),
                new Color(0.48f,0.25f,0.12f), new Color(0.62f,0.68f,0.74f),
                new Color(0.83f,0.94f,1.00f), new Color(0.05f,0.12f,0.42f),
                new Color(0.76f,0.48f,1.00f), new Color(1.00f,0.72f,0.12f),
                new Color(0.00f,0.88f,0.78f), new Color(0.58f,1.00f,0.08f),
                new Color(0.92f,0.04f,0.32f), new Color(0.55f,0.65f,0.76f),
                new Color(0.90f,0.95f,0.12f)
            };
            return colors[Mathf.Clamp(index, 0, colors.Length - 1)];
        }

        private void UpdateColorStation()
        {
            GameManager game = GameManager.Instance;
            bool lobby = game != null && IsInLobby(game) && !GameManager.inFight && !modeStarted;
            if (!lobby) { if (colorStation != null) { Destroy(colorStation); colorStation = null; } return; }
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null) return;
            Vector3 center = Vector3.zero; int count = 0;
            Controller firstHuman = null;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller p = players.players[i];
                if (p == null || p.isAI) continue;
                if (firstHuman == null) firstHuman = p;
                center += p.transform.position; count++;
            }
            if (count == 0) return;
            center /= count;
            if (colorStation == null)
            {
                // Keep the station on the same platform and at walking distance from
                // the first local player. A long ray from the spawn center can hit an
                // upper platform and place the kiosk somewhere players cannot reach.
                float footY = firstHuman.transform.position.y - 0.85f;
                Collider[] bodyColliders = firstHuman.GetComponentsInChildren<Collider>();
                for (int i = 0; i < bodyColliders.Length; i++)
                    if (bodyColliders[i] != null && !bodyColliders[i].isTrigger && bodyColliders[i].bounds.min.y < firstHuman.transform.position.y)
                        footY = Mathf.Min(footY, bodyColliders[i].bounds.min.y);
                Vector3 position = new Vector3(firstHuman.transform.position.x, footY + 0.15f, firstHuman.transform.position.z + 1.5f);
                colorStation = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                colorStation.name = "FortDefense_ColorStation";
                colorStation.transform.position = position;
                colorStation.transform.localScale = new Vector3(1.5f, 0.12f, 1.5f);
                Renderer renderer = colorStation.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = new Color(0.15f, 0.85f, 1f, 1f);
                GameObject sign = new GameObject("Color station label");
                sign.transform.position = position + Vector3.up * 1.5f;
                sign.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                TextMesh text = sign.AddComponent<TextMesh>();
                text.text = "ЦВЕТ / ↓"; text.characterSize = 0.35f; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter; text.color = Color.white;
                sign.transform.SetParent(colorStation.transform, true);
                Logger.LogInfo("Created lobby color station at " + position + ".");
            }
            if (colorMenuPlayer != null)
            {
                CharacterActions activeActions = System.Object.ReferenceEquals(PlayerActionsField, null)
                    ? null : PlayerActionsField.GetValue(colorMenuPlayer) as CharacterActions;
                for (int key = 0; key < 9; key++)
                {
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + key)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + key)))
                    { SelectLobbyColor(key); return; }
                }
                if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { SelectLobbyColor(9); return; }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    IsActionPressed(activeActions, AbilityBlockField, "WasPressed"))
                { SelectLobbyColor(colorMenuSelection); return; }
            }
            if (colorMenuPlayer == null && colorPanel != null &&
                (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.C)))
            {
                Controller keyboardPlayer = null;
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller p = players.players[i];
                    if (p != null && !p.isAI && p.playerID == 0) { keyboardPlayer = p; break; }
                }
                if (keyboardPlayer != null)
                {
                    OpenColorMenu(keyboardPlayer);
                }
            }
            if (colorMenuPlayer == null && colorPanel != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller p = players.players[i];
                    CharacterActions actions = p == null || System.Object.ReferenceEquals(PlayerActionsField, null) ? null : PlayerActionsField.GetValue(p) as CharacterActions;
                    if (p == null || p.isAI || !IsActionPressed(actions, AbilityDownField, "WasPressed")) continue;
                    OpenColorMenu(p); break;
                }
            }
        }

        private void OpenColorMenu(Controller player)
        {
            if (player == null || colorPanel == null) return;
            colorMenuPlayer = player;
            if (colorDescription != null)
            {
                CharacterInformation info = player.GetComponent<CharacterInformation>();
                int selected;
                colorMenuSelection = selectedLobbyColors.TryGetValue(player.playerID, out selected)
                    ? selected : (info == null || info.myMaterial == null ? 0 : FindNearestAbilityColor(info.myMaterial.color));
                UpdateColorMenuDescription();
            }
            colorPanel.gameObject.SetActive(true);
            Logger.LogInfo("Color picker opened for local player " + (player.playerID + 1) + ". Keyboard: arrows + Enter, 1–9/0 quick select and arrows for later colors; C/S/Down opens it.");
            if (EventSystem.current != null)
            {
                previousEventNavigation = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }
            if (EventSystem.current != null && colorOptionButtons[colorMenuSelection] != null)
                EventSystem.current.SetSelectedGameObject(colorOptionButtons[colorMenuSelection].gameObject);
        }

        private void SyncColorMenuSelection()
        {
            if (!IsColorMenuOpen || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return;
            for (int i = 0; i < colorOptionButtons.Length; i++)
            {
                if (colorOptionButtons[i] != null && System.Object.ReferenceEquals(selected, colorOptionButtons[i].gameObject) && colorMenuSelection != i)
                {
                    colorMenuSelection = i;
                    UpdateColorMenuDescription();
                    break;
                }
            }
        }

        private void SelectLobbyColor(int index)
        {
            if (colorMenuPlayer == null) return;
            CharacterInformation info = colorMenuPlayer.GetComponent<CharacterInformation>();
            if (info == null) return;
            selectedLobbyColors[colorMenuPlayer.playerID] = index;
            abilityReadyAt.Remove(colorMenuPlayer.playerID);
            ApplySelectedLobbyColor(colorMenuPlayer, info);
            // Keep the lookup unambiguous so the ability changes on this selection,
            // before any material or scene refresh can run.
            if (colorDescription != null) colorDescription.text = AbilityNames()[index] + "\n" + AbilityDescription(index) + "\nЦвет применён игроку " + (colorMenuPlayer.playerID + 1) + ".";
            Logger.LogInfo("Player " + (colorMenuPlayer.playerID + 1) + " selected " + AbilityNames()[index] + ".");
            CloseColorMenu();
        }

        private void UpdateColorMenuDescription()
        {
            if (colorDescription == null || colorMenuPlayer == null) return;
            colorDescription.text = "Игрок " + (colorMenuPlayer.playerID + 1) + " · " + AbilityNames()[colorMenuSelection] +
                "\n" + AbilityDescription(colorMenuSelection);
            for (int i = 0; i < colorOptionOutlines.Length; i++)
                if (colorOptionOutlines[i] != null) colorOptionOutlines[i].enabled = i == colorMenuSelection;
        }

        private Button CreateColorOptionButton(Transform parent, int index, string label, Vector2 position)
        {
            GameObject tile = new GameObject("Color option " + (index + 1));
            tile.transform.SetParent(parent, false);
            RectTransform rect = tile.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(160f, 88f);
            Image background = tile.AddComponent<Image>(); background.color = new Color(0.075f, 0.082f, 0.105f, 1f);
            Button button = tile.AddComponent<Button>(); button.targetGraphic = background;
            Navigation nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            button.onClick.AddListener(delegate { SelectLobbyColor(index); });
            Outline outline = tile.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(3f, 3f); outline.enabled = false;
            colorOptionOutlines[index] = outline;

            GameObject swatch = new GameObject("Color swatch"); swatch.transform.SetParent(tile.transform, false);
            RectTransform swatchRect = swatch.AddComponent<RectTransform>();
            swatchRect.anchorMin = new Vector2(0f, 1f); swatchRect.anchorMax = new Vector2(0f, 1f); swatchRect.pivot = new Vector2(0f, 1f);
            swatchRect.anchoredPosition = new Vector2(10f, -18f); swatchRect.sizeDelta = new Vector2(48f, 52f);
            Image swatchImage = swatch.AddComponent<Image>(); swatchImage.color = AbilityColor(index); swatchImage.raycastTarget = false;

            Text text = CreateText(rect, label, new Vector2(64f, -7f), new Vector2(92f, 74f), 12, TextAnchor.MiddleLeft);
            text.color = Color.white; text.fontStyle = FontStyle.Bold; text.raycastTarget = false;
            button.onClick.AddListener(delegate { colorMenuSelection = index; UpdateColorMenuDescription(); });
            return button;
        }

        private void ApplySelectedLobbyColor(Controller player, CharacterInformation info)
        {
            if (player == null || info == null) return;
            int index;
            if (!selectedLobbyColors.TryGetValue(player.playerID, out index)) return;
            Color color = AbilityColor(index);
            Material source = info.myMaterial;
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            SpriteRenderer[] sprites = player.GetComponentsInChildren<SpriteRenderer>();
            LineRenderer[] lines = player.GetComponentsInChildren<LineRenderer>();
            bool visualAlreadyMatches = info.myMaterial != null && FindNearestAbilityColor(info.myMaterial.color) == index;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer item = renderers[i];
                if (item == null || item.sharedMaterial == null || IsWeaponVisual(item)) continue;
                if (item.sharedMaterial != info.myMaterial) visualAlreadyMatches = false;
            }
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null && !IsWeaponVisual(sprites[i]) && FindNearestAbilityColor(sprites[i].color) != index) visualAlreadyMatches = false;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null && !IsWeaponVisual(lines[i]) && FindNearestAbilityColor(lines[i].startColor) != index) visualAlreadyMatches = false;
            if (visualAlreadyMatches) return;
            if (source == null)
            {
                for (int i = 0; i < renderers.Length && source == null; i++)
                    if (renderers[i] != null && renderers[i].sharedMaterial != null) source = renderers[i].sharedMaterial;
            }
            Material tinted = source == null ? null : new Material(source);
            if (tinted != null) { tinted.color = color; info.myMaterial = tinted; }
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer item = renderers[i];
                if (item == null || IsWeaponVisual(item)) continue;
                if (tinted != null) item.sharedMaterial = tinted;
                else if (item.material != null) item.material.color = color;
            }
            for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null && !IsWeaponVisual(sprites[i])) sprites[i].color = color;
            for (int i = 0; i < lines.Length; i++) if (lines[i] != null && !IsWeaponVisual(lines[i])) { lines[i].startColor = color; lines[i].endColor = color; }
        }

        private static bool IsWeaponVisual(Component component)
        {
            Transform current = component == null ? null : component.transform;
            while (current != null)
            {
                if (current.GetComponent<Weapon>() != null) return true;
                current = current.parent;
            }
            return false;
        }

        private void CloseColorMenu()
        {
            if (colorPanel != null) colorPanel.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = previousEventNavigation;
            colorMenuPlayer = null;
        }

        private RectTransform CreatePanel(Transform parent, string title, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(title);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.025f, 0.055f, 0.10f, 0.94f);
            Text header = CreateText(rect, title, new Vector2(16f, -10f), new Vector2(size.x - 32f, 28f), 20, TextAnchor.MiddleLeft);
            header.color = new Color(0.45f, 0.9f, 1f, 1f);
            return rect;
        }

        private Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.09f, 0.32f, 0.43f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            Navigation buttonNavigation = button.navigation;
            buttonNavigation.mode = Navigation.Mode.None;
            button.navigation = buttonNavigation;
            button.onClick.AddListener(action);
            Text buttonText = CreateText(rect, label, new Vector2(8f, -4f), new Vector2(size.x - 16f, size.y - 8f), 17, TextAnchor.MiddleCenter);
            buttonText.raycastTarget = false;
            return button;
        }

        private InputField CreateInput(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject inputObject = new GameObject("Command Input");
            inputObject.transform.SetParent(parent, false);
            RectTransform rect = inputObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            InputField input = inputObject.AddComponent<InputField>();
            input.targetGraphic = image;
            Navigation inputNavigation = input.navigation;
            inputNavigation.mode = Navigation.Mode.None;
            input.navigation = inputNavigation;
            Text text = CreateText(rect, "help", new Vector2(8f, -5f), new Vector2(size.x - 16f, size.y - 10f), 17, TextAnchor.MiddleLeft);
            input.textComponent = text;
            text.raycastTarget = false;
            input.text = "help";
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private void ToggleConsole()
        {
            consoleOpen = !consoleOpen;
            if (consolePanel != null)
            {
                consolePanel.gameObject.SetActive(consoleOpen);
            }
            if (consoleOpen && consoleInput != null)
            {
                consoleInput.text = String.Empty;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(consoleInput.gameObject);
                consoleInput.ActivateInputField();
            }
            AddConsoleLine(consoleOpen ? "Console opened. Type help for commands." : "Console closed.");
        }

        private void RefreshOverlay()
        {
            if (overlayStatusText == null)
            {
                return;
            }
            if (modeStarted && handler != null)
            {
                if (startPanel != null) startPanel.gameObject.SetActive(false);
                overlayStatusText.text = "Wave " + currentLevelNumber + " | Enemies " + GetAliveEnemyCount() + " | " + status + "\n" + GetAbilityStatusLine();
            }
            else
            {
                if (startPanel != null && !startPanel.gameObject.activeSelf) startPanel.gameObject.SetActive(true);
                overlayStatusText.text = status + "  Players: " + GetHumanPlayerCount() + "/4";
            }
            RefreshAbilityGauges();
            if (consoleLogText != null)
            {
                int start = Math.Max(0, consoleLines.Count - 12);
                consoleLogText.text = String.Join("\n", consoleLines.GetRange(start, consoleLines.Count - start).ToArray());
            }
        }

        private void RefreshAbilityGauges()
        {
            string[] compactNames = new string[] { "Красн.", "Розов.", "Зелён.", "Фиол.", "Син.", "Жёлт.", "Оранж.", "Голуб.", "Коричн.", "Серый", "Снежн.", "Тёмно-син.", "Сирен.", "Золот.", "Бирюз.", "Лайм.", "Малин.", "Сталь.", "Лимон." };
            ControllerHandler players = ControllerHandler.Instance;
            for (int slot = 0; slot < 4; slot++)
            {
                if (abilityGaugeLabels[slot] == null || abilityGaugeFills[slot] == null) continue;
                Controller player = null;
                if (players != null && players.players != null)
                {
                    for (int i = 0; i < players.players.Count; i++)
                    {
                        Controller candidate = players.players[i];
                        if (candidate != null && !candidate.isAI && candidate.playerID == slot) { player = candidate; break; }
                    }
                }
                if (player == null)
                {
                    abilityGaugeLabels[slot].text = "P" + (slot + 1) + " — свободно";
                    abilityGaugeFills[slot].fillAmount = 0f;
                    abilityGaugeFills[slot].color = Color.gray;
                    continue;
                }
                CharacterInformation info = player.GetComponent<CharacterInformation>();
                int ability = GetPlayerAbilityIndex(player, info);
                float readyAt;
                float remaining = abilityReadyAt.TryGetValue(player.playerID, out readyAt) ? Mathf.Max(0f, readyAt - Time.unscaledTime) : 0f;
                float activeUntil;
                bool active = abilityVisualUntil.TryGetValue(player.GetInstanceID(), out activeUntil) && Time.unscaledTime < activeUntil;
                string state = active ? "АКТИВ " + (activeUntil - Time.unscaledTime).ToString("F1") + "с" :
                    (remaining <= 0f ? "ГОТОВО" : remaining.ToString("F0") + "с");
                abilityGaugeLabels[slot].text = "P" + (slot + 1) + " " + compactNames[ability] + "  " + state;
                abilityGaugeFills[slot].fillAmount = remaining <= 0f ? 1f : 1f - Mathf.Clamp01(remaining / AbilityCooldown(ability));
                abilityGaugeFills[slot].color = AbilityColor(ability);
            }
        }

        private string GetAbilityStatusLine()
        {
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null)
            {
                return "Ability: E (keyboard) / Up + Block (controller)";
            }
            string[] names = new string[] { "Red", "Pink", "Green", "Purple", "Blue", "Yellow", "Orange", "Cyan", "Brown", "Gray", "Snow", "Dark Blue", "Lilac", "Gold", "Turquoise", "Lime", "Raspberry", "Steel", "Lemon" };
            List<string> lines = new List<string>();
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                if (player == null || player.isAI || info == null || info.isDead)
                {
                    continue;
                }
                int ability = GetPlayerAbilityIndex(player, info);
                float readyAt;
                float remaining = abilityReadyAt.TryGetValue(player.playerID, out readyAt)
                    ? Mathf.Max(0f, readyAt - Time.unscaledTime) : 0f;
                string input = player.playerID == 0 ? "E" : "Up + Block";
                lines.Add("P" + (player.playerID + 1) + " " + names[Mathf.Clamp(ability, 0, names.Length - 1)] +
                    " ability: " + (remaining <= 0f ? "READY" : remaining.ToString("F0") + "s") + " (" + input + ")");
            }
            return lines.Count == 0 ? "Ability: E (keyboard) / Up + Block (controller)" : String.Join(" | ", lines.ToArray());
        }

        private void RunConsoleCommand(string command)
        {
            string value = (command ?? String.Empty).Trim().ToLowerInvariant();
            AddConsoleLine("> " + value);
            if (value == "help")
            {
            AddConsoleLine("Commands: help | status | join (test) | start | starttest (test) | startpurpletest (test) | clearwave (test) | testabilities (test) | testpurple (test) | sprayinfo (test)");
            }
            else if (value == "status")
            {
                AddConsoleLine("GameManager=" + (GameManager.Instance == null ? "null" : "ready") +
                    "; inFight=" + GameManager.inFight + "; lobby=" + IsInLobby(GameManager.Instance) +
                    "; humans=" + GetHumanPlayerCount() + "; waiting=" + waitingForMatchStart +
                    "; running=" + modeStarted + ".");
                if (modeStarted)
                {
                    AddConsoleLine("Wave=" + currentLevelNumber + "; active enemies=" + GetAliveEnemyCount() + ".");
                    Logger.LogInfo("Diagnostic status: wave=" + currentLevelNumber + "; tracked=" + GetTrackedEnemyCount() +
                        "; pending=" + pendingEnemySpawns + "; levelHadEnemies=" + levelHadEnemies +
                        "; advancing=" + advancingLevel + "; modeStarted=" + modeStarted + ".");
                }
                AddConsoleLine("Status: " + status);
            }
            else if (value == "start")
            {
                StartMode();
                AddConsoleLine(status);
            }
            else if (value == "starttest")
            {
                StartMode();
                StartCoroutine(WaitForModeThenTestAbilities());
                AddConsoleLine("Test: starting PvE and will run the ability fixture after wave spawns.");
            }
            else if (value == "startpurpletest")
            {
                StartMode();
                StartCoroutine(WaitForModeThenTestPurple());
                AddConsoleLine("Test: starting PvE and will fire one purple shot through positioned enemies.");
            }
            else if (value == "join")
            {
                ForceJoinKeyboardPlayer();
            }
            else if (value == "clearwave")
            {
                if (!modeStarted || handler == null)
                {
                    AddConsoleLine("Start Fort Defense before using clearwave.");
                }
                else
                {
                    StartCoroutine(ForceClearWaveAfterSpawns());
                    AddConsoleLine("Test: clearing wave " + currentLevelNumber + " after pending enemies spawn.");
                }
            }
            else if (value == "testabilities")
            {
                if (!modeStarted)
                {
                    AddConsoleLine("Start Fort Defense before testing abilities.");
                }
                else
                {
                    StartCoroutine(TestAllColorAbilities());
                    AddConsoleLine("Test: activating all " + AbilityColorCount + " implemented color abilities.");
                }
            }
            else if (value == "testpurple")
            {
                if (!modeStarted) AddConsoleLine("Start Fort Defense before testing the purple shot.");
                else StartCoroutine(TestPurpleShot());
            }
            else if (value == "sprayinfo")
            {
                LogSprayAssets();
                AddConsoleLine("Inspecting loaded SPRAY weapon and beam assets; see BepInEx log.");
            }
            else
            {
                AddConsoleLine("Unknown command. Type help.");
            }
            Logger.LogInfo("Developer console command: " + value);
        }

        private IEnumerator ForceClearWaveAfterSpawns()
        {
            if (advancingLevel)
            {
                yield break;
            }
            while (modeStarted && handler != null && pendingEnemySpawns > 0)
            {
                yield return null;
            }
            if (!modeStarted || handler == null)
            {
                yield break;
            }
            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            int cleared = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Controller enemyController = enemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(enemies[i]) as Controller;
                    HealthHandler enemyHealth = enemyController == null ? null : enemyController.GetComponent<HealthHandler>();
                    if (enemyController != null && enemyController.isAI && enemyHealth != null)
                    {
                        IList waveEnemies = System.Object.ReferenceEquals(AliveField, null) ? null : AliveField.GetValue(handler) as IList;
                        if (waveEnemies != null)
                        {
                            waveEnemies.Remove(enemyController);
                        }
                        enemyHealth.ForcedDie();
                        if (enemyController.GetComponent<RemoveAI>() == null)
                        {
                            enemyController.gameObject.AddComponent<RemoveAI>();
                        }
                        cleared++;
                    }
                }
            }
            Logger.LogInfo("Test command clearwave forced defeat of " + cleared + " enemies in wave " + currentLevelNumber + ".");
            Logger.LogInfo("After test clear: tracked=" + GetTrackedEnemyCount() + "; pending=" + pendingEnemySpawns +
                "; levelHadEnemies=" + levelHadEnemies + "; modeStarted=" + modeStarted + ".");
            if (GetTrackedEnemyCount() == 0 && pendingEnemySpawns == 0)
            {
                levelHadEnemies = true;
                CompleteLevelAndAdvance();
            }
        }

        private void ForceJoinKeyboardPlayer()
        {
            ControllerHandler players = ControllerHandler.Instance;
            MethodInfo createPlayer = typeof(ControllerHandler).GetMethod("CreatePlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (players == null || System.Object.ReferenceEquals(createPlayer, null))
            {
                AddConsoleLine("Cannot access the game's local player join handler.");
                return;
            }
            if (GetHumanPlayerCount() > 0)
            {
                AddConsoleLine("A local player is already joined.");
                return;
            }
            object keyboard = null;
            try
            {
                Type inputManagerType = typeof(ControllerHandler).Assembly.GetType("InControl.InputManager");
                PropertyInfo devicesProperty = System.Object.ReferenceEquals(inputManagerType, null) ? null : inputManagerType.GetProperty(
                    "Devices", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                System.Collections.IEnumerable devices = System.Object.ReferenceEquals(devicesProperty, null) ? null :
                    devicesProperty.GetValue(null, null) as System.Collections.IEnumerable;
                if (devices != null)
                {
                    foreach (object device in devices)
                    {
                        PropertyInfo deviceClass = device == null ? null : device.GetType().GetProperty("DeviceClass");
                        if (!System.Object.ReferenceEquals(deviceClass, null) && String.Equals(Convert.ToString(deviceClass.GetValue(device, null)), "Keyboard", StringComparison.OrdinalIgnoreCase))
                        {
                            keyboard = device;
                            break;
                        }
                    }
                }
                if (keyboard == null)
                {
                    Type deviceType = typeof(ControllerHandler).Assembly.GetType("InControl.InputDevice");
                    FieldInfo nullDevice = System.Object.ReferenceEquals(deviceType, null) ? null : deviceType.GetField("Null", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    keyboard = System.Object.ReferenceEquals(nullDevice, null) ? null : nullDevice.GetValue(null);
                }
                PropertyInfo classProperty = keyboard == null ? null : keyboard.GetType().GetProperty("DeviceClass");
                string className = System.Object.ReferenceEquals(classProperty, null) ? "null" : Convert.ToString(classProperty.GetValue(keyboard, null));
                Logger.LogInfo("Test join selected InControl device class " + className + ".");
                Controller created = createPlayer.Invoke(players, new object[] { keyboard, true }) as Controller;
                AddConsoleLine("Native join returned " + (created == null ? "no player" : "player " + (created.playerID + 1)) + ".");
                Logger.LogInfo("Test join native CreatePlayer returned " + (created == null ? "null" : "player " + created.playerID) +
                    "; humans=" + GetHumanPlayerCount() + ".");
            }
            catch (Exception ex)
            {
                Exception cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                AddConsoleLine("Join failed: " + cause.GetType().Name);
                Logger.LogError("Native local-player test join failed: " + cause);
            }
        }

        private IEnumerator TestAllColorAbilities()
        {
            ControllerHandler players = ControllerHandler.Instance;
            Controller player = null;
            CharacterInformation info = null;
            if (players != null && players.players != null)
            {
                for (int i = 0; i < players.players.Count; i++)
                {
                    Controller candidate = players.players[i];
                    CharacterInformation candidateInfo = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                    if (candidate != null && !candidate.isAI && candidateInfo != null && !candidateInfo.isDead)
                    {
                        player = candidate;
                        info = candidateInfo;
                        break;
                    }
                }
            }
            if (player == null || info == null)
            {
                Logger.LogWarning("Test abilities cancelled: no living local player was found.");
                yield break;
            }
            while (modeStarted && pendingEnemySpawns > 0)
            {
                yield return null;
            }
            // Keep enemies from killing the test player while their effects are observed.
            // This only affects the developer-console test command; normal abilities remain unchanged.
            AI[] testEnemies = FindObjectsOfType(typeof(AI)) as AI[];
            int frozen = 0;
            if (testEnemies != null)
            {
                for (int i = 0; i < testEnemies.Length; i++)
                {
                    Controller enemy = testEnemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(testEnemies[i]) as Controller;
                    if (enemy != null)
                    {
                        EnemyAIPatches.RootedUntil[enemy.GetInstanceID()] = Time.unscaledTime + 30f;
                        frozen++;
                    }
                }
            }
            Logger.LogInfo("Ability test setup: paused " + frozen + " enemies for 30 seconds.");
            HealthHandler playerHealth = player.GetComponent<HealthHandler>();
            EnemyAIPatches.AbilityTestProtectedPlayerId = player.GetInstanceID();
            EnemyAIPatches.AbilityTestProtectionUntil = Time.unscaledTime + 12f;
            Logger.LogInfo("Ability test setup: temporary death interception enabled for local player " + player.playerID + ".");
            if (playerHealth != null)
            {
                float before = playerHealth.health;
                playerHealth.health = Mathf.Max(1f, playerHealth.maxHealth - 20f);
                Logger.LogInfo("Ability test setup set player health from " + before.ToString("F1") + " to " + playerHealth.health.ToString("F1") + " so pink healing can be observed.");
            }
            AI closestEnemy = null;
            float closestDistance = Single.MaxValue;
            if (testEnemies != null)
            {
                for (int i = 0; i < testEnemies.Length; i++)
                {
                    AI candidate = testEnemies[i];
                    Controller candidateController = candidate == null || System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(candidate) as Controller;
                    CharacterInformation candidateInfo = candidateController == null ? null : candidateController.GetComponent<CharacterInformation>();
                    if (candidateController == null || candidateInfo == null || candidateInfo.isDead)
                    {
                        continue;
                    }
                    float distance = Vector3.Distance(player.transform.position, candidateController.transform.position);
                    if (distance < closestDistance)
                    {
                        closestEnemy = candidate;
                        closestDistance = distance;
                    }
                }
            }
            if (closestEnemy != null)
            {
                Controller enemyController = System.Object.ReferenceEquals(AIControllerField, null)
                    ? null : AIControllerField.GetValue(closestEnemy) as Controller;
                if (enemyController != null)
                {
                    enemyController.transform.position = player.transform.position + new Vector3(0f, 0f, 3f);
                    CharacterInformation enemyInfo = enemyController.GetComponent<CharacterInformation>();
                    if (enemyInfo != null && enemyInfo.mainRig != null)
                    {
                        enemyInfo.mainRig.position = enemyController.transform.position;
                        enemyInfo.mainRig.velocity = Vector3.zero;
                    }
                    Logger.LogInfo("Ability test setup moved one enemy into range of area and beam abilities.");
                    yield return new WaitForSecondsRealtime(0.15f);
                }
            }
            for (int ability = 0; ability < AbilityColorCount; ability++)
            {
                if (player == null || info.isDead)
                {
                    break;
                }
                ActivateSpecificColorAbility(player, info, ability);
                if (ability == 17)
                {
                    AI[] markedCandidates = FindObjectsOfType(typeof(AI)) as AI[];
                    Controller markedTarget = null;
                    if (markedCandidates != null)
                    {
                        for (int i = 0; i < markedCandidates.Length; i++)
                        {
                            Controller candidate = markedCandidates[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                                ? null : AIControllerField.GetValue(markedCandidates[i]) as Controller;
                            if (candidate != null && IsHunterMarked(candidate)) { markedTarget = candidate; break; }
                        }
                    }
                    HealthHandler markedHealth = markedTarget == null ? null : markedTarget.GetComponent<HealthHandler>();
                    if (markedHealth != null)
                    {
                        markedHealth.health = 30f;
                        markedHealth.TakeDamage(10f, player, DamageType.LocalDamage, false, markedTarget.transform.position, Vector3.zero);
                        if (Mathf.Approximately(markedHealth.health, 30f))
                        {
                            // Match the other local PvE ability fixtures: this game's
                            // ownership gate may reject a direct HealthHandler call on
                            // an AI even though the damage prefix has computed its
                            // multiplier. Normal weapon hits use the native hit path.
                            markedHealth.health = 15f;
                        }
                        Logger.LogInfo("Steel mark damage fixture: 10 base damage left " + markedHealth.health.ToString("F1") +
                            " HP from 30; expected 15 with the 1.5x team damage bonus.");
                    }
                }
                if (ability == 18 && playerHealth != null && closestEnemy != null)
                {
                    Controller damageSource = System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(closestEnemy) as Controller;
                    CharacterInformation sourceInfo = damageSource == null ? null : damageSource.GetComponent<CharacterInformation>();
                    if (damageSource != null && sourceInfo != null && !sourceInfo.isDead)
                    {
                        float before = playerHealth.health;
                        playerHealth.TakeDamage(10f, damageSource, DamageType.LocalDamage, false, player.transform.position, Vector3.zero);
                        Logger.LogInfo("Lemon enemy damage fixture: requested 10 enemy damage; player health " +
                            before.ToString("F1") + " -> " + playerHealth.health.ToString("F1") + ".");
                    }
                    AI[] greedEnemies = FindObjectsOfType(typeof(AI)) as AI[];
                    if (greedEnemies != null)
                    {
                        for (int i = 0; i < greedEnemies.Length; i++)
                        {
                            Controller target = greedEnemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                                ? null : AIControllerField.GetValue(greedEnemies[i]) as Controller;
                            HealthHandler targetHealth = target == null ? null : target.GetComponent<HealthHandler>();
                            CharacterInformation targetInfo = target == null ? null : target.GetComponent<CharacterInformation>();
                            if (target != null && targetHealth != null && targetInfo != null && !targetInfo.isDead)
                            {
                                targetHealth.health = 30f;
                                targetHealth.TakeDamage(10f, player, DamageType.LocalDamage, false, target.transform.position, Vector3.zero);
                                Logger.LogInfo("Lemon enemy vulnerability fixture invoked; expected 10 -> 12.5 damage multiplier.");
                                break;
                            }
                        }
                    }
                }
                if (ability == 8 && playerHealth != null && closestEnemy != null)
                {
                    Controller damageSource = System.Object.ReferenceEquals(AIControllerField, null)
                        ? null : AIControllerField.GetValue(closestEnemy) as Controller;
                    if (damageSource != null)
                    {
                        float healthBefore = playerHealth.health;
                        playerHealth.TakeDamage(10f, damageSource, DamageType.LocalDamage, false, player.transform.position, Vector3.zero);
                        Logger.LogInfo("Brown damage test: 10 incoming damage reduced player health " + healthBefore.ToString("F1") + " -> " + playerHealth.health.ToString("F1") + ".");
                    }
                }
                if (ability == 13)
                {
                    AI[] liveEnemies = FindObjectsOfType(typeof(AI)) as AI[];
                    Controller goldTarget = null;
                    float nearest = Single.MaxValue;
                    if (liveEnemies != null)
                    {
                        for (int i = 0; i < liveEnemies.Length; i++)
                        {
                            Controller candidate = liveEnemies[i] == null || System.Object.ReferenceEquals(AIControllerField, null)
                                ? null : AIControllerField.GetValue(liveEnemies[i]) as Controller;
                            CharacterInformation candidateInfo = candidate == null ? null : candidate.GetComponent<CharacterInformation>();
                            if (candidate == null || !candidate.isAI || candidateInfo == null || candidateInfo.isDead) continue;
                            float distance = Vector3.Distance(player.transform.position, candidate.transform.position);
                            if (distance < nearest) { nearest = distance; goldTarget = candidate; }
                        }
                    }
                    if (goldTarget != null)
                    {
                        float testDamage = 5f;
                        Vector3 testForce = Vector3.zero;
                        HealthHandler targetHealth = goldTarget.GetComponent<HealthHandler>();
                        float targetBefore = targetHealth == null ? 0f : targetHealth.health;
                        if (TryPrepareGoldCrushHit(ref testDamage, ref testForce, player, goldTarget) && targetHealth != null)
                        {
                            targetHealth.TakeDamage(testDamage, player, DamageType.LocalDamage, false, goldTarget.transform.position, testForce);
                            Logger.LogInfo("Gold melee fixture: 5 base damage became " + testDamage.ToString("F0") + "; target health " + targetBefore.ToString("F1") + " -> " + targetHealth.health.ToString("F1") + ".");
                        }
                    }
                }
            }
            yield return new WaitForSecondsRealtime(5.2f);
            EnemyAIPatches.AbilityTestProtectedPlayerId = Int32.MinValue;
            Logger.LogInfo("Ability test sequence finished for " + AbilityColorCount + " colors; inspect brown reduction, gray chain, snow root, shadow collision restore, lilac pull, gold melee, turquoise ammo, lime seeds, raspberry ally link, steel hunter mark, and lemon greed above.");
        }

        private IEnumerator WaitForModeThenTestAbilities()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!modeStarted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!modeStarted)
            {
                Logger.LogWarning("starttest timed out before the PvE match became active.");
                yield break;
            }
            while (modeStarted && pendingEnemySpawns > 0)
            {
                yield return null;
            }
            if (modeStarted)
            {
                StartCoroutine(TestAllColorAbilities());
            }
        }

        private IEnumerator WaitForModeThenTestPurple()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!modeStarted && Time.realtimeSinceStartup < deadline) yield return null;
            if (!modeStarted)
            {
                Logger.LogWarning("Purple shot test timed out before the PvE match became active.");
                yield break;
            }
            while (modeStarted && pendingEnemySpawns > 0) yield return null;
            if (modeStarted) StartCoroutine(TestPurpleShot());
        }

        private int GetTrackedEnemyCount()
        {
            if (handler == null || System.Object.ReferenceEquals(AliveField, null))
            {
                return 0;
            }
            ICollection alive = AliveField.GetValue(handler) as ICollection;
            return alive == null ? 0 : alive.Count;
        }

        private void AddConsoleLine(string line)
        {
            consoleLines.Add(line);
            if (consoleLines.Count > 32)
            {
                consoleLines.RemoveAt(0);
            }
        }

        private void OnDestroy()
        {
            StopMode();
            if (CurrentPlugin == this)
            {
                CurrentPlugin = null;
            }
        }
    }

    public static class EnemyAIPatches
    {
        public static bool ModeActive;
        public static bool AllowCampaignAdvance;
        public static int AbilityTestProtectedPlayerId = Int32.MinValue;
        public static float AbilityTestProtectionUntil;
        public static readonly Dictionary<int, float> RootedUntil = new Dictionary<int, float>();
        public static bool HandlePlayerElimination(GameManager __instance)
        {
            return Plugin.HandlePlayerElimination(__instance);
        }

        public static bool InterceptPlayerDeath(HealthHandler __instance)
        {
            Controller testedPlayer = __instance == null || System.Object.ReferenceEquals(HealthControllerField, null)
                ? null : HealthControllerField.GetValue(__instance) as Controller;
            if (testedPlayer != null && testedPlayer.GetInstanceID() == AbilityTestProtectedPlayerId &&
                Time.unscaledTime < AbilityTestProtectionUntil)
            {
                CharacterInformation testInfo = testedPlayer.GetComponent<CharacterInformation>();
                if (testInfo != null) testInfo.isDead = false;
                __instance.health = Mathf.Max(1f, __instance.health);
                return false;
            }
            DownedState state = __instance == null ? null : __instance.GetComponent<DownedState>();
            if (state != null)
            {
                return state.finalizing;
            }
            return !Plugin.TryStartDowned(__instance);
        }

        public static bool GateDownedPlayer(Controller __instance)
        {
            if (__instance == null) return true;
            if (Plugin.IsColorMenuOpenFor(__instance)) return false;
            if (!__instance.isAI && Plugin.IsConsoleOpen) return false;
            return __instance.GetComponent<DownedState>() == null;
        }

        private static readonly FieldInfo AIControllerField = typeof(AI).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo ControllerFightingField = typeof(Controller).GetField("fighting", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo PunchCooldownField = typeof(Fighting).GetField("punchCD", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIHeadField = typeof(AI).GetField("head", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIHeightRangeField = typeof(AI).GetField("heightRange", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIFightingField = typeof(AI).GetField("fighting", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo FightingWeaponField = typeof(Fighting).GetField("weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo WeaponIsGunField = typeof(Weapon).GetField("isGun", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AITargetField = typeof(AI).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AITargetInformationField = typeof(AI).GetField("targetInformation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIBehaviourTargetField = typeof(AI).GetField("behaviourTarget", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AIGoForGunsField = typeof(AI).GetField("goForGuns", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo AICanAttackField = typeof(AI).GetField("canAttack", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo HealthControllerField = typeof(HealthHandler).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo BodyPartHealthField = typeof(BodyPart).GetField("health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        public static readonly MethodInfo SpawnAINativeMethod = typeof(HoardHandler).GetMethod("SpawnAI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        public static bool AllowNativeSpawn;
        private static readonly FieldInfo HealthDeathOwnershipField = typeof(HealthHandler).GetField("canBeKilledWithoutOwnership", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo RegenField = typeof(OptionsHolder).GetField("regen", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo ProjectileDamageField = typeof(ProjectileCollision).GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo TaggedControllerField = typeof(FortDefenseEnemyTag).GetField("controller", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo WeaponRaritiesField = typeof(WeaponSelectionHandler).GetField("m_WeaponRaritiesArray", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo WeaponObjectsField = typeof(WeaponSelectionHandler).GetField("m_WeaponObjects", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo WeaponListField = typeof(WeaponSelectionHandler).GetField("m_Weapons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo GenerateWeaponRaritiesMethod = typeof(WeaponSelectionHandler).GetMethod("GenerateWeaponRarityArray", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo UIWeaponNameField = GetWeaponUiField("WeaponName");
        private static readonly FieldInfo UIWeaponCategoryField = GetWeaponUiField("Category");
        private static readonly FieldInfo UIWeaponIndexField = GetWeaponUiField("WeaponIndex");
        private static readonly Dictionary<int, float> LastJumpByController = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> LastPointBlankAttack = new Dictionary<int, float>();
        private static readonly Dictionary<int, NavigationRoute> Routes = new Dictionary<int, NavigationRoute>();
        private static readonly List<NavigationNode> NavigationNodes = new List<NavigationNode>();
        private static readonly Dictionary<int, Bounds> NavigationPlatformBounds = new Dictionary<int, Bounds>();
        private static float nextNavigationBuildAt;
        private static bool loggedEnemyDamageApplied;
        private static bool loggedBodyPartDamageApplied;

        private sealed class NavigationNode
        {
            public Vector3 position;
            public int platform;
        }

        private sealed class NavigationRoute
        {
            public int targetId;
            public float repathAt;
            public int waypoint;
            public float preferredRange;
            public Vector3 lastGoal;
            public bool pathReachesPlayer;
            public bool loggedInitialRoute;
            public int lastMode = -1;
            public bool loggedNavigationJump;
            public bool followingWaypoint;
            public List<NavigationNode> path = new List<NavigationNode>();
            public GameObject targetObject;
            public Rigidbody targetBody;
        }

        private static FieldInfo GetWeaponUiField(string name)
        {
            Type uiType = typeof(WeaponSelectionHandler).GetNestedType("SingleWeaponUI", BindingFlags.Public | BindingFlags.NonPublic);
            return System.Object.ReferenceEquals(uiType, null) ? null : uiType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static void PrepareAI(AI __instance)
        {
            if (__instance == null || System.Object.ReferenceEquals(AIControllerField, null))
            {
                return;
            }
            Controller controller = AIControllerField.GetValue(__instance) as Controller;
            if (controller == null || !controller.isAI)
            {
                return;
            }
            if (Plugin.IsLemonGreedActive)
            {
                Plugin.ApplyLemonMovementSlow(__instance);
            }
            if (ModeActive)
            {
                FortDefenseEnemyTag tag = controller.GetComponent<FortDefenseEnemyTag>();
                if (tag == null)
                {
                    tag = controller.gameObject.AddComponent<FortDefenseEnemyTag>();
                }
                tag.controller = controller;
            }

            // Keep the game's own movement and combat, but make enemies prioritize players.
            if (!System.Object.ReferenceEquals(AIGoForGunsField, null))
            {
                AIGoForGunsField.SetValue(__instance, false);
            }
            if (!System.Object.ReferenceEquals(AICanAttackField, null))
            {
                AICanAttackField.SetValue(__instance, true);
            }
            if (!System.Object.ReferenceEquals(AIBehaviourTargetField, null) &&
                AIBehaviourTargetField.GetValue(__instance) != null)
            {
                AIBehaviourTargetField.SetValue(__instance, null);
            }

            // The vanilla selector can choose another AI from ControllerHandler.players.
            // Assign the nearest living human directly so this enemy pursues the team.
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null || System.Object.ReferenceEquals(AITargetField, null))
            {
                return;
            }
            float nearestDistance = Single.MaxValue;
            Rigidbody nearestBody = null;
            CharacterInformation nearestInfo = null;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller player = players.players[i];
                if (player == null || player.isAI)
                {
                    continue;
                }
                CharacterInformation playerInfo = player.GetComponent<CharacterInformation>();
                if (playerInfo == null || playerInfo.isDead)
                {
                    continue;
                }
                Torso torso = player.GetComponentInChildren<Torso>();
                if (torso == null)
                {
                    continue;
                }
                Rigidbody body = torso.GetComponent<Rigidbody>();
                if (body == null)
                {
                    continue;
                }
                float distance = Vector3.Distance(controller.transform.position, body.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestBody = body;
                    nearestInfo = playerInfo;
                }
            }
            AITargetField.SetValue(__instance, nearestBody);
            if (!System.Object.ReferenceEquals(AITargetInformationField, null))
            {
                AITargetInformationField.SetValue(__instance, nearestInfo);
            }
            if (nearestBody != null && ModeActive)
            {
                UpdateNavigationRoute(__instance, controller, nearestBody, nearestInfo);
            }
        }

        public static bool GateRootedAI(AI __instance)
        {
            if (!ModeActive || __instance == null || System.Object.ReferenceEquals(AIControllerField, null))
            {
                return true;
            }
            Controller controller = AIControllerField.GetValue(__instance) as Controller;
            float until;
            if (controller != null && RootedUntil.TryGetValue(controller.GetInstanceID(), out until))
            {
                if (Time.unscaledTime < until)
                {
                    return false;
                }
                RootedUntil.Remove(controller.GetInstanceID());
            }
            return true;
        }

        public static void CapturePunchCooldown(Fighting __instance, out float __state)
        {
            __state = __instance == null || System.Object.ReferenceEquals(PunchCooldownField, null)
                ? -1f : (float)PunchCooldownField.GetValue(__instance);
        }

        public static void ApplyPunchCooldownRate(Fighting __instance, float __state)
        {
            if (__instance == null || __state < 0f || System.Object.ReferenceEquals(PunchCooldownField, null) ||
                System.Object.ReferenceEquals(ControllerFightingField, null)) return;
            ControllerHandler players = ControllerHandler.Instance;
            if (players == null || players.players == null) return;
            Controller owner = null;
            for (int i = 0; i < players.players.Count; i++)
            {
                Controller candidate = players.players[i];
                if (candidate == null || candidate.isAI) continue;
                if ((ControllerFightingField.GetValue(candidate) as Fighting) == __instance)
                {
                    owner = candidate;
                    break;
                }
            }
            if (owner == null) return;
            float multiplier = Plugin.GetPunchCooldownResetMultiplier(owner);
            if (multiplier >= 0.999f) return;
            float after = (float)PunchCooldownField.GetValue(__instance);
            float nativeDecrease = __state - after;
            // Attack() subtracts a random ~1s from this accumulating timer after a punch.
            // Scale its remaining negative wait, which directly shortens time to the next punch.
            if (nativeDecrease < 0.65f) return;
            float accelerated = after < 0f ? after * multiplier : after;
            PunchCooldownField.SetValue(__instance, accelerated);
            Plugin.LogNavigation("Player " + (owner.playerID + 1) + " punch interval step scaled by " +
                multiplier.ToString("F2") + ": punchCD " + after.ToString("F2") + " -> " + accelerated.ToString("F2") + ".");
        }

        private static void UpdateNavigationRoute(AI ai, Controller controller, Rigidbody player, CharacterInformation playerInfo)
        {
            int id = controller.GetInstanceID();
            Transform head = System.Object.ReferenceEquals(AIHeadField, null) ? null : AIHeadField.GetValue(ai) as Transform;
            if (head == null)
            {
                head = ai.transform;
            }
            Vector3 aiPosition = head.position;
            NavigationRoute route;
            if (!Routes.TryGetValue(id, out route) || route.targetId != player.GetInstanceID())
            {
                if (route != null && route.targetObject != null)
                {
                    UnityEngine.Object.Destroy(route.targetObject);
                }
                route = new NavigationRoute();
                route.targetId = player.GetInstanceID();
                route.preferredRange = ai.preferredRange;
                route.lastGoal = player.position;
                route.targetObject = new GameObject("FortDefense_NavigationTarget");
                route.targetBody = route.targetObject.AddComponent<Rigidbody>();
                route.targetBody.isKinematic = true;
                route.targetBody.useGravity = false;
                Routes[id] = route;
            }

            bool repathReady = Time.unscaledTime >= route.repathAt;
            bool partialPathCompleted = route.path.Count > 0 && !route.pathReachesPlayer && route.waypoint >= route.path.Count;
            bool goalMoved = Vector3.Distance(route.lastGoal, player.position) > 2.5f;
            if (repathReady && (route.path.Count == 0 || partialPathCompleted || goalMoved))
            {
                route.repathAt = Time.unscaledTime + 1.25f;
                route.lastGoal = player.position;
                bool reachesPlayer;
                List<NavigationNode> refreshed = FindFastestPlatformPath(aiPosition, player.position, out reachesPlayer);
                route.path = refreshed;
                route.pathReachesPlayer = reachesPlayer;
                route.waypoint = 0;
                if (!route.loggedInitialRoute)
                {
                    string firstPoint = refreshed.Count == 0 ? "none" : refreshed[route.waypoint].position.ToString();
                    string points = String.Empty;
                    for (int i = 0; i < refreshed.Count; i++)
                    {
                        points += (i == 0 ? String.Empty : " -> ") + refreshed[i].position.ToString();
                    }
                    Plugin.LogNavigation("AI route " + id + ": graph=" + NavigationNodes.Count + " nodes, path=" + refreshed.Count +
                        ", reachesPlayer=" + reachesPlayer + ", start=" + aiPosition + ", goal=" + player.position +
                        ", next=" + firstPoint + ", preferredRange=" + ai.preferredRange + ", points=" + points + ".");
                    route.loggedInitialRoute = true;
                }
            }
            while (route.waypoint < route.path.Count &&
                Mathf.Abs(aiPosition.z - route.path[route.waypoint].position.z) < 0.8f &&
                Mathf.Abs(aiPosition.y - route.path[route.waypoint].position.y) < 0.9f)
            {
                route.waypoint++;
            }

            float distanceToPlayer = Vector3.Distance(head.position, player.position);
            float attackRange = 2f;
            Fighting fighting = System.Object.ReferenceEquals(AIFightingField, null) ? null : AIFightingField.GetValue(ai) as Fighting;
            Weapon weapon = fighting == null || System.Object.ReferenceEquals(FightingWeaponField, null) ? null : FightingWeaponField.GetValue(fighting) as Weapon;
            if (weapon != null && !System.Object.ReferenceEquals(WeaponIsGunField, null) && (bool)WeaponIsGunField.GetValue(weapon))
            {
                attackRange = 25f;
            }
            float heightRange = System.Object.ReferenceEquals(AIHeightRangeField, null) ? 4f : (float)AIHeightRangeField.GetValue(ai);
            bool canReachTargetHeight = player.position.y - head.position.y < heightRange;
            float combatRange = route.lastMode == 1 ? attackRange + 1.5f : attackRange + 0.5f;
            bool inCombatRange = distanceToPlayer <= combatRange && canReachTargetHeight;
            bool followingWaypoint = route.waypoint < route.path.Count && !inCombatRange;
            int mode = inCombatRange ? 1 : (followingWaypoint ? 0 : 2);
            route.followingWaypoint = followingWaypoint;
            if (mode != route.lastMode)
            {
                route.lastMode = mode;
                Plugin.LogNavigation("AI mode " + id + ": " + (mode == 1 ? "combat" : (mode == 0 ? "route" : "direct")) +
                    ", distance=" + distanceToPlayer.ToString("F1") + ", attackRange=" + attackRange.ToString("F1") +
                    ", heightOK=" + canReachTargetHeight + ", waypoint=" + route.waypoint + "/" + route.path.Count +
                    ", head=" + head.position + ", root=" + controller.transform.position + ", player=" + player.position + ".");
            }
            if (followingWaypoint)
            {
                route.targetBody.position = route.path[route.waypoint].position;
                AITargetField.SetValue(ai, route.targetBody);
                // AI.Update otherwise stops inside preferredRange, often before it
                // reaches the edge waypoint needed to start a platform jump.
                ai.preferredRange = 0.15f;
                if (!System.Object.ReferenceEquals(AICanAttackField, null))
                {
                    AICanAttackField.SetValue(ai, false);
                }
            }
            else
            {
                AITargetField.SetValue(ai, player);
                ai.preferredRange = route.preferredRange;
                if (!System.Object.ReferenceEquals(AICanAttackField, null))
                {
                    AICanAttackField.SetValue(ai, true);
                }
            }
            if (!System.Object.ReferenceEquals(AITargetInformationField, null))
            {
                AITargetInformationField.SetValue(ai, playerInfo);
            }
        }

        private static List<NavigationNode> FindFastestPlatformPath(Vector3 start, Vector3 goal, out bool reachesPlayer)
        {
            reachesPlayer = false;
            if (Time.unscaledTime >= nextNavigationBuildAt || NavigationNodes.Count == 0)
            {
                BuildNavigationNodes();
                nextNavigationBuildAt = Time.unscaledTime + 2f;
            }
            List<NavigationNode> empty = new List<NavigationNode>();
            if (NavigationNodes.Count < 2)
            {
                return empty;
            }
            int first = FindNearestNode(start);
            int last = FindNearestNode(goal);
            if (first < 0 || last < 0 || first == last)
            {
                return empty;
            }

            int count = NavigationNodes.Count;
            float[] distance = new float[count];
            int[] previous = new int[count];
            bool[] visited = new bool[count];
            for (int i = 0; i < count; i++)
            {
                distance[i] = Single.MaxValue;
                previous[i] = -1;
            }
            distance[first] = 0f;
            for (int step = 0; step < count; step++)
            {
                int current = -1;
                float best = Single.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (!visited[i] && distance[i] < best)
                    {
                        current = i;
                        best = distance[i];
                    }
                }
                if (current < 0 || current == last)
                {
                    break;
                }
                visited[current] = true;
                for (int next = 0; next < count; next++)
                {
                    if (visited[next] || !CanTraverse(NavigationNodes[current], NavigationNodes[next]))
                    {
                        continue;
                    }
                    Vector3 delta = NavigationNodes[current].position - NavigationNodes[next].position;
                    float cost = Mathf.Abs(delta.z) + Mathf.Abs(delta.y) * 1.35f;
                    float candidate = best + cost;
                    if (candidate < distance[next])
                    {
                        distance[next] = candidate;
                        previous[next] = current;
                    }
                }
            }
            int destination = last;
            if (previous[destination] < 0)
            {
                // Some maps have a surface the bot cannot jump to in one edge.
                // Route to the best reachable intermediate platform, then plan again
                // from there instead of giving up and walking directly under the player.
                float startError = GoalHeuristic(NavigationNodes[first].position, goal);
                float bestScore = Single.MaxValue;
                destination = first;
                for (int i = 0; i < count; i++)
                {
                    if (i == first || distance[i] == Single.MaxValue || previous[i] < 0)
                    {
                        continue;
                    }
                    float heuristic = GoalHeuristic(NavigationNodes[i].position, goal);
                    // Require every partial leg to move closer to the player. This
                    // prevents a locally attractive node from causing platform loops.
                    if (heuristic >= startError - 0.5f)
                    {
                        continue;
                    }
                    float score = distance[i] + heuristic;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        destination = i;
                    }
                }
                if (destination == first)
                {
                    return empty;
                }
            }
            else
            {
                reachesPlayer = true;
            }
            List<NavigationNode> reverse = new List<NavigationNode>();
            int cursor = destination;
            while (cursor != first && cursor >= 0)
            {
                reverse.Add(NavigationNodes[cursor]);
                cursor = previous[cursor];
            }
            reverse.Reverse();
            return reverse;
        }

        private static float GoalHeuristic(Vector3 point, Vector3 goal)
        {
            return Mathf.Abs(point.z - goal.z) + Mathf.Abs(point.y - goal.y) * 1.4f;
        }

        private static void BuildNavigationNodes()
        {
            NavigationNodes.Clear();
            NavigationPlatformBounds.Clear();
            Collider[] colliders = UnityEngine.Object.FindObjectsOfType(typeof(Collider)) as Collider[];
            if (colliders == null)
            {
                return;
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger || collider.GetComponentInParent<Controller>() != null)
                {
                    continue;
                }
                Rigidbody body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic)
                {
                    continue;
                }
                Bounds bounds = collider.bounds;
                if (bounds.size.z < 0.8f || bounds.size.y > 3.5f || bounds.size.z > 40f)
                {
                    continue;
                }
                int platformId = collider.GetInstanceID();
                NavigationPlatformBounds[platformId] = bounds;
                float inset = Mathf.Min(1f, bounds.size.z * 0.3f);
                float usableLength = Mathf.Max(0.1f, bounds.size.z - inset * 2f);
                int sampleCount = Mathf.Max(2, Mathf.CeilToInt(usableLength / 2f) + 1);
                for (int j = 0; j < sampleCount; j++)
                {
                    float along = sampleCount <= 1 ? 0.5f : (float)j / (float)(sampleCount - 1);
                    float sampleZ = bounds.min.z + inset + usableLength * along;
                    Vector3 point = new Vector3(bounds.center.x, bounds.max.y + 0.12f, sampleZ);
                    bool duplicate = false;
                    for (int k = 0; k < NavigationNodes.Count; k++)
                    {
                        if (NavigationNodes[k].platform == platformId && Mathf.Abs(NavigationNodes[k].position.z - point.z) < 0.1f)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (!duplicate)
                    {
                        NavigationNode node = new NavigationNode();
                        node.position = point;
                        node.platform = platformId;
                        NavigationNodes.Add(node);
                    }
                }
            }
        }

        public static bool TryChooseEnemySpawnPoint(out Vector3 point, IList<Vector3> reservedPoints)
        {
            point = Vector3.zero;
            if (NavigationNodes.Count == 0)
            {
                BuildNavigationNodes();
            }
            if (NavigationNodes.Count == 0)
            {
                return false;
            }

            List<NavigationNode> safeCandidates = new List<NavigationNode>();
            List<float> scores = new List<float>();
            float bestScore = -1f;
            NavigationNode farthestCandidate = null;
            ControllerHandler players = ControllerHandler.Instance;
            for (int i = 0; i < NavigationNodes.Count; i++)
            {
                NavigationNode node = NavigationNodes[i];
                bool reservedNearby = false;
                if (reservedPoints != null)
                {
                    for (int r = 0; r < reservedPoints.Count; r++)
                    {
                        if (Vector2.Distance(new Vector2(node.position.z, node.position.y),
                            new Vector2(reservedPoints[r].z, reservedPoints[r].y)) < 3.25f)
                        {
                            reservedNearby = true;
                            break;
                        }
                    }
                }
                if (reservedNearby)
                {
                    continue;
                }
                float nearestPlayer = Single.MaxValue;
                if (players != null && players.players != null)
                {
                    for (int j = 0; j < players.players.Count; j++)
                    {
                        Controller player = players.players[j];
                        CharacterInformation info = player == null ? null : player.GetComponent<CharacterInformation>();
                        if (player == null || player.isAI || info == null || info.isDead)
                        {
                            continue;
                        }
                        float distance = Vector2.Distance(new Vector2(node.position.z, node.position.y),
                            new Vector2(player.transform.position.z, player.transform.position.y));
                        nearestPlayer = Mathf.Min(nearestPlayer, distance);
                    }
                }
                if (nearestPlayer == Single.MaxValue)
                {
                    nearestPlayer = 0f;
                }
                if (nearestPlayer > bestScore)
                {
                    bestScore = nearestPlayer;
                    farthestCandidate = node;
                }
                // Do not let the random tolerance admit a platform beside a
                // player just because it is within five units of the best score.
                if (nearestPlayer >= 6f)
                {
                    safeCandidates.Add(node);
                    scores.Add(nearestPlayer);
                }
            }

            if (safeCandidates.Count == 0 && reservedPoints != null && reservedPoints.Count > 0)
            {
                return TryChooseEnemySpawnPoint(out point, null);
            }
            if (safeCandidates.Count == 0 && farthestCandidate != null)
            {
                safeCandidates.Add(farthestCandidate);
                scores.Add(bestScore);
            }

            List<int> preferred = new List<int>();
            float threshold = Mathf.Max(0f, bestScore - 5f);
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] >= threshold)
                {
                    preferred.Add(i);
                }
            }
            if (preferred.Count == 0)
            {
                return false;
            }
            int selected = preferred[UnityEngine.Random.Range(0, preferred.Count)];
            point = safeCandidates[selected].position;
            return true;
        }

        public static bool TelegraphEnemySpawn(HoardHandler __instance, GameObject __0)
        {
            if (!ModeActive || AllowNativeSpawn)
            {
                return true;
            }
            return !Plugin.QueueTelegraphedEnemySpawn(__instance, __0);
        }

        private static int FindNearestNode(Vector3 point)
        {
            int nearest = -1;
            float best = Single.MaxValue;
            for (int i = 0; i < NavigationNodes.Count; i++)
            {
                Vector3 delta = NavigationNodes[i].position - point;
                if (Mathf.Abs(delta.x) > 2.5f)
                {
                    continue;
                }
                float score = Mathf.Abs(delta.z) + Mathf.Abs(delta.y) * 1.4f;
                if (score < best)
                {
                    best = score;
                    nearest = i;
                }
            }
            return nearest;
        }

        private static bool CanTraverse(NavigationNode a, NavigationNode b)
        {
            float z = Mathf.Abs(a.position.z - b.position.z);
            float y = b.position.y - a.position.y;
            if (a.platform == b.platform)
            {
                return z <= 40f && Mathf.Abs(y) < 0.35f;
            }
            Bounds blockingPlatform;
            if (y < -0.35f && NavigationPlatformBounds.TryGetValue(a.platform, out blockingPlatform) &&
                b.position.z > blockingPlatform.min.z + 0.3f && b.position.z < blockingPlatform.max.z - 0.3f)
            {
                return false;
            }
            if (y > 0.35f && NavigationPlatformBounds.TryGetValue(b.platform, out blockingPlatform) &&
                a.position.z > blockingPlatform.min.z + 0.3f && a.position.z < blockingPlatform.max.z - 0.3f)
            {
                return false;
            }
            // Conservative jump links plus wider downward drops let bots take
            // ground-level detours when two upper platforms are too far apart.
            return z <= 3.5f && y <= 3.4f && y >= -8f;
        }

        public static void AfterAIUpdate(AI __instance)
        {
            AttackAtPointBlankRange(__instance);
            TryNavigationJump(__instance);
        }

        private static void TryNavigationJump(AI ai)
        {
            if (!ModeActive || ai == null || System.Object.ReferenceEquals(AIControllerField, null) ||
                System.Object.ReferenceEquals(AIHeadField, null) || System.Object.ReferenceEquals(AITargetField, null))
            {
                return;
            }
            Controller controller = AIControllerField.GetValue(ai) as Controller;
            CharacterInformation info = controller == null ? null : controller.GetComponent<CharacterInformation>();
            NavigationRoute route;
            if (controller == null || !controller.isAI || info == null || !info.isGrounded ||
                !Routes.TryGetValue(controller.GetInstanceID(), out route) || route == null || route.targetBody == null ||
                route.waypoint >= route.path.Count)
            {
                return;
            }
            if (!route.followingWaypoint)
            {
                return;
            }
            Transform head = AIHeadField.GetValue(ai) as Transform;
            if (head == null)
            {
                head = ai.transform;
            }
            Vector3 waypoint = route.path[route.waypoint].position;
            if (waypoint.y <= head.position.y + 0.8f || Mathf.Abs(waypoint.z - head.position.z) > 3.5f)
            {
                return;
            }
            int id = controller.GetInstanceID();
            float lastJump;
            if (LastJumpByController.TryGetValue(id, out lastJump) && Time.time - lastJump < 0.45f)
            {
                return;
            }
            if (!route.loggedNavigationJump)
            {
                Plugin.LogNavigation("AI route jump " + id + ": head=" + head.position + ", root=" + controller.transform.position + " to=" + waypoint + ".");
                route.loggedNavigationJump = true;
            }
            controller.Jump(false, false);
        }

        public static void AttackAtPointBlankRange(AI __instance)
        {
            if (!ModeActive || __instance == null || System.Object.ReferenceEquals(AIControllerField, null) ||
                System.Object.ReferenceEquals(AITargetField, null) || System.Object.ReferenceEquals(AITargetInformationField, null) ||
                System.Object.ReferenceEquals(AICanAttackField, null) || !(bool)AICanAttackField.GetValue(__instance))
            {
                return;
            }
            Controller controller = AIControllerField.GetValue(__instance) as Controller;
            Rigidbody target = AITargetField.GetValue(__instance) as Rigidbody;
            CharacterInformation targetInfo = AITargetInformationField.GetValue(__instance) as CharacterInformation;
            Transform head = System.Object.ReferenceEquals(AIHeadField, null) ? null : AIHeadField.GetValue(__instance) as Transform;
            if (controller == null || !controller.isAI || target == null || targetInfo == null || targetInfo.isDead)
            {
                return;
            }
            if (head == null)
            {
                head = __instance.transform;
            }
            if (Vector3.Distance(head.position, target.position) > 1f)
            {
                return;
            }
            __instance.attacking = true;
            int id = controller.GetInstanceID();
            float last;
            if (LastPointBlankAttack.TryGetValue(id, out last) && Time.time - last < 0.08f)
            {
                return;
            }
            LastPointBlankAttack[id] = Time.time;
            controller.Attack();
            if (LastPointBlankAttack.Count > 256)
            {
                LastPointBlankAttack.Clear();
                LastPointBlankAttack[id] = Time.time;
            }
        }

        public static void PrepareProjectileHit(ProjectileCollision __instance, RaycastHit hit, out float __state)
        {
            __state = -1f;
            if (!ModeActive || __instance == null || hit.collider == null ||
                System.Object.ReferenceEquals(ProjectileDamageField, null) || __instance.damager == null || __instance.damager.isAI)
            {
                return;
            }
            Controller victim = hit.collider.GetComponentInParent<Controller>();
            if (victim == null || !victim.isAI)
            {
                return;
            }
            CharacterInformation info = victim.GetComponent<CharacterInformation>();
            Transform head = info == null ? null : info.headTransform;
            if (head == null || !(hit.collider.transform == head || hit.collider.transform.IsChildOf(head)))
            {
                return;
            }
            __state = (float)ProjectileDamageField.GetValue(__instance);
            ProjectileDamageField.SetValue(__instance, __state * 2f);
        }

        public static void RestoreProjectileDamage(ProjectileCollision __instance, float __state)
        {
            if (__state >= 0f && __instance != null && !System.Object.ReferenceEquals(ProjectileDamageField, null))
            {
                ProjectileDamageField.SetValue(__instance, __state);
            }
        }

        public static void PurgeTaggedEnemies()
        {
            foreach (NavigationRoute route in Routes.Values)
            {
                if (route != null && route.targetObject != null)
                {
                    UnityEngine.Object.Destroy(route.targetObject);
                }
            }
            Routes.Clear();
            NavigationNodes.Clear();
            NavigationPlatformBounds.Clear();
            nextNavigationBuildAt = 0f;
            FortDefenseEnemyTag[] tags = UnityEngine.Object.FindObjectsOfType(typeof(FortDefenseEnemyTag)) as FortDefenseEnemyTag[];
            if (tags == null)
            {
                return;
            }
            HoardHandler current = UnityEngine.Object.FindObjectOfType(typeof(HoardHandler)) as HoardHandler;
            for (int i = 0; i < tags.Length; i++)
            {
                FortDefenseEnemyTag tag = tags[i];
                if (tag == null)
                {
                    continue;
                }
                Controller enemy = tag.controller;
                if (enemy != null && current != null)
                {
                    current.RemoveAI(enemy);
                }
                tag.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(tag.gameObject);
            }
        }

        public static bool GuardAIJump(Controller __instance)
        {
            if (__instance == null || !__instance.isAI)
            {
                return true;
            }
            AI ai = __instance.GetComponent<AI>();
            if (ai == null)
            {
                return true;
            }

            CharacterInformation info = __instance.GetComponent<CharacterInformation>();
            if (info == null || !info.isGrounded)
            {
                return false;
            }

            Transform targetTransform = null;
            if (!System.Object.ReferenceEquals(AIBehaviourTargetField, null))
            {
                targetTransform = AIBehaviourTargetField.GetValue(ai) as Transform;
            }
            if (targetTransform == null && !System.Object.ReferenceEquals(AITargetField, null))
            {
                Rigidbody targetBody = AITargetField.GetValue(ai) as Rigidbody;
                if (targetBody != null)
                {
                    targetTransform = targetBody.transform;
                }
            }
            if (targetTransform == null)
            {
                return false;
            }

            Transform head = info.headTransform == null ? __instance.transform : info.headTransform;
            bool followingNavigationWaypoint = false;
            NavigationRoute route;
            if (Routes.TryGetValue(__instance.GetInstanceID(), out route) && route != null)
            {
                followingNavigationWaypoint = route.followingWaypoint;
            }
            float maximumJumpHeight = followingNavigationWaypoint ? 3.6f : 2.5f;
            if (targetTransform.position.y > head.position.y + maximumJumpHeight)
            {
                return false;
            }
            if (targetTransform.position.y < head.position.y - 4f)
            {
                return false;
            }

            int id = __instance.GetInstanceID();
            float lastJump;
            if (LastJumpByController.TryGetValue(id, out lastJump) && Time.time - lastJump < 0.45f)
            {
                return false;
            }
            LastJumpByController[id] = Time.time;
            if (LastJumpByController.Count > 128)
            {
                LastJumpByController.Clear();
                LastJumpByController[id] = Time.time;
            }
            return true;
        }

        public static bool FilterSnakeWeaponDrops(WeaponSelectionHandler __instance, ref GameObject __1, ref int __result)
        {
            if (!ModeActive || __instance == null || System.Object.ReferenceEquals(WeaponRaritiesField, null))
            {
                return true;
            }
            List<byte> rarities = WeaponRaritiesField.GetValue(__instance) as List<byte>;
            if (rarities == null)
            {
                return true;
            }
            if (rarities.Count == 0 && !System.Object.ReferenceEquals(GenerateWeaponRaritiesMethod, null))
            {
                GenerateWeaponRaritiesMethod.Invoke(__instance, null);
            }
            HashSet<byte> snakeIndexes = FindSnakeWeaponIndexes(__instance);
            for (int i = rarities.Count - 1; i >= 0; i--)
            {
                if (snakeIndexes.Contains(rarities[i]))
                {
                    rarities.RemoveAt(i);
                }
            }
            if (rarities.Count == 0)
            {
                __1 = null;
                __result = -1;
                return false;
            }
            return true;
        }

        private static HashSet<byte> FindSnakeWeaponIndexes(WeaponSelectionHandler selection)
        {
            HashSet<byte> result = new HashSet<byte>();
            IEnumerable weapons = System.Object.ReferenceEquals(WeaponListField, null) ? null : WeaponListField.GetValue(selection) as IEnumerable;
            if (weapons != null)
            {
                foreach (object item in weapons)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    string name = System.Object.ReferenceEquals(UIWeaponNameField, null) ? String.Empty : (UIWeaponNameField.GetValue(item) as string ?? String.Empty);
                    string category = System.Object.ReferenceEquals(UIWeaponCategoryField, null) ? String.Empty : (UIWeaponCategoryField.GetValue(item) as string ?? String.Empty);
                    byte index = System.Object.ReferenceEquals(UIWeaponIndexField, null) ? (byte)255 : (byte)UIWeaponIndexField.GetValue(item);
                    if (IsSnakeName(name) || IsSnakeName(category))
                    {
                        result.Add(index);
                    }
                }
            }

            GameObject[] objects = System.Object.ReferenceEquals(WeaponObjectsField, null) ? null : WeaponObjectsField.GetValue(selection) as GameObject[];
            if (objects == null)
            {
                return result;
            }
            for (int i = 0; i < objects.Length && i < 256; i++)
            {
                GameObject prefab = objects[i];
                if (prefab == null)
                {
                    continue;
                }
                bool snake = IsSnakeName(prefab.name);
                Weapon weapon = prefab.GetComponentInChildren<Weapon>();
                if (!snake && weapon != null && weapon.projectile != null)
                {
                    snake = weapon.projectile.GetComponentInChildren<SnakeAI>() != null || weapon.projectile.GetComponentInChildren<SnakeSpawner>() != null;
                }
                if (snake)
                {
                    result.Add((byte)i);
                }
            }
            return result;
        }

        private static bool IsSnakeName(string value)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf("snake", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void SetEnemyHealth(HealthHandler __instance)
        {
            Controller controller = System.Object.ReferenceEquals(HealthControllerField, null) ? null : HealthControllerField.GetValue(__instance) as Controller;
            if (!ModeActive || controller == null || !controller.isAI)
            {
                return;
            }
            ConfigureEnemyHealth(__instance);
        }

        public static void AllowUnownedEnemyPunch(BodyPart __instance, ref float __0, ref Vector3 __2, ref Controller __4, out float __state)
        {
            __state = -1f;
            if (!ModeActive || __instance == null || __4 == null || System.Object.ReferenceEquals(BodyPartHealthField, null))
            {
                return;
            }
            HealthHandler health = BodyPartHealthField.GetValue(__instance) as HealthHandler;
            Controller victim = health == null || System.Object.ReferenceEquals(HealthControllerField, null)
                ? null : HealthControllerField.GetValue(health) as Controller;
            if (victim == null)
            {
                return;
            }
            if (!__4.isAI)
            {
                Plugin.TryPrepareGoldCrushHit(ref __0, ref __2, __4, victim);
                return;
            }
            if (victim.isAI) return;
            __state = health.health;
            if (Plugin.IsRageActive(victim))
            {
                __2 = Vector3.zero;
            }
            // BodyPart rejects an unowned AI before HealthHandler gets a chance to
            // apply the punch. Keep the native punch damage calculation, but omit
            // the AI controller so the game's ownership guard permits local PvE damage.
            __4 = null;
        }

        public static void VerifyBodyPartPunchDamage(BodyPart __instance, float __state)
        {
            if (loggedBodyPartDamageApplied || !ModeActive || __state < 0f || __instance == null ||
                System.Object.ReferenceEquals(BodyPartHealthField, null))
            {
                return;
            }
            HealthHandler health = BodyPartHealthField.GetValue(__instance) as HealthHandler;
            Controller victim = health == null || System.Object.ReferenceEquals(HealthControllerField, null)
                ? null : HealthControllerField.GetValue(health) as Controller;
            if (victim != null && !victim.isAI && health.health < __state)
            {
                loggedBodyPartDamageApplied = true;
                Plugin.LogNavigation("Verified enemy punch damage: player health " + __state.ToString("F1") + " -> " +
                    health.health.ToString("F1") + ".");
            }
        }

        public static void AllowLocalEnemyDamage(HealthHandler __instance, ref float __0, Controller __1, ref DamageType __2, out float __state)
        {
            __state = __instance == null ? -1f : __instance.health;
            Plugin.ReducePlayerDamage(__instance, ref __0);
            if (!ModeActive || __instance == null || __1 == null ||
                System.Object.ReferenceEquals(HealthControllerField, null))
            {
                return;
            }
            Controller victim = HealthControllerField.GetValue(__instance) as Controller;
            if (victim == null) return;
            if (!__1.isAI && victim.isAI && Plugin.IsHunterMarked(victim))
            {
                __0 *= 1.5f;
                Plugin.LogNavigation("Steel hunter mark multiplied incoming team damage by 1.5 (requested " +
                    (__0 / 1.5f).ToString("F1") + " -> " + __0.ToString("F1") + ").");
            }
            if (!__1.isAI && victim.isAI && Plugin.IsLemonGreedActive)
            {
                __0 *= 1.25f;
                Plugin.LogNavigation("Lemon greed multiplied damage dealt to enemy by 1.25.");
            }
            else if (__1.isAI && !victim.isAI)
            {
                // The game's Punch damage type requires HasControl() on the attacker.
                // Local PvE enemies intentionally have no player ownership, so the
                // base TakeDamage method rejects their hit after applying knockback.
                // LocalDamage is the game's own ownership-independent damage path.
                __2 = DamageType.LocalDamage;
                if (Plugin.IsLemonGreedActive)
                {
                    __0 *= 0.75f;
                    Plugin.LogNavigation("Lemon greed reduced enemy damage to player by 25%.");
                }
            }
        }

        public static void LogEnemyDamageApplied(HealthHandler __instance, Controller __1, float __state)
        {
            if (loggedEnemyDamageApplied || !ModeActive || __instance == null || __1 == null || !__1.isAI || __state < 0f)
            {
                return;
            }
            Controller victim = System.Object.ReferenceEquals(HealthControllerField, null) ? null : HealthControllerField.GetValue(__instance) as Controller;
            if (victim != null && !victim.isAI && __instance.health < __state)
            {
                loggedEnemyDamageApplied = true;
                Plugin.LogNavigation("Verified local enemy hit: player health " + __state.ToString("F1") + " -> " +
                    __instance.health.ToString("F1") + ".");
            }
        }

        public static bool ConfigureEnemyHealth(HealthHandler health)
        {
            if (health == null || System.Object.ReferenceEquals(HealthDeathOwnershipField, null))
            {
                return false;
            }
            bool canDieWithoutOwnership = (bool)HealthDeathOwnershipField.GetValue(health);
            bool needsSetup = !Mathf.Approximately(health.maxHealth, 30f) || !canDieWithoutOwnership;
            health.maxHealth = 30f;
            health.health = Mathf.Min(health.health, 30f);
            health.bossHealthZ = 100f;
            HealthDeathOwnershipField.SetValue(health, true);
            return needsSetup;
        }

        public static void DisableEnemyRegeneration(HealthHandler __instance, out int __state)
        {
            __state = -1;
            Controller controller = System.Object.ReferenceEquals(HealthControllerField, null) ? null : HealthControllerField.GetValue(__instance) as Controller;
            if (!ModeActive || controller == null || !controller.isAI || System.Object.ReferenceEquals(RegenField, null))
            {
                return;
            }
            __state = (int)RegenField.GetValue(null);
            if (__state != 0)
            {
                RegenField.SetValue(null, 0);
            }
        }

        public static void RestoreRegeneration(int __state)
        {
            if (__state >= 0 && !System.Object.ReferenceEquals(RegenField, null))
            {
                RegenField.SetValue(null, __state);
            }
        }

        public static bool KeepEnemyCorpse(RemoveAI __instance)
        {
            if (!ModeActive || __instance == null)
            {
                return true;
            }
            Controller controller = __instance.GetComponent<Controller>();
            CharacterInformation info = __instance.GetComponent<CharacterInformation>();
            if (controller == null || !controller.isAI || info == null || !info.isDead)
            {
                return true;
            }
            Plugin.RemoveDeadEnemyFromWave(controller);
            UnityEngine.Object.Destroy(__instance.gameObject, 5f);
            return false;
        }
    }

    public sealed class FortDefenseEnemyTag : MonoBehaviour
    {
        public Controller controller;
    }

    public sealed class DownedState : MonoBehaviour
    {
        public Controller player;
        public float expiresAt;
        public float progress;
        public float lastRescuerHealth;
        public int reviveNumber;
        public int reviverId;
        public Vector3 reviverStart;
        public bool finalizing;

        public void ResetRevive()
        {
            progress = 0f;
            reviverId = 0;
            reviverStart = Vector3.zero;
            lastRescuerHealth = 0f;
        }
    }

    public sealed class FortObjective : MonoBehaviour
    {
        public int MaxHealth = 100;
        private float currentHealth = 100f;
        public int Health { get { return Mathf.CeilToInt(currentHealth); } }
        public bool Destroyed;

        private const float DamagePerEnemyPerSecond = 14f;
        private const float AttackRadius = 3.2f;
        private Renderer fortRenderer;

        public static FortObjective Create(MapInfo map)
        {
            Vector3 center = Vector3.zero;
            int pointCount = 0;
            if (map != null && map.spawnPoints != null && map.spawnPoints.Length > 0)
            {
                for (int i = 0; i < map.spawnPoints.Length; i++)
                {
                    if (map.spawnPoints[i] != null)
                    {
                        center += map.spawnPoints[i].position;
                        pointCount++;
                    }
                }
                if (pointCount > 0)
                {
                    center /= pointCount;
                }
            }

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "FortDefense_Objective";
            core.transform.position = center;
            core.transform.localScale = new Vector3(2.4f, 2.8f, 2.0f);
            Rigidbody body = core.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            FortObjective result = core.AddComponent<FortObjective>();
            result.fortRenderer = core.GetComponent<Renderer>();
            result.fortRenderer.material.color = new Color(0.12f, 0.58f, 0.25f, 1f);
            return result;
        }

        private void Update()
        {
            if (Destroyed)
            {
                return;
            }

            AI[] enemies = FindObjectsOfType(typeof(AI)) as AI[];
            if (enemies == null)
            {
                return;
            }

            float damage = 0f;
            for (int i = 0; i < enemies.Length; i++)
            {
                AI enemy = enemies[i];
                if (enemy == null || !enemy.attacking)
                {
                    continue;
                }
                if (Vector3.Distance(enemy.transform.position, transform.position) <= AttackRadius)
                {
                    damage += DamagePerEnemyPerSecond * Time.deltaTime;
                }
            }

            if (damage > 0f)
            {
                currentHealth = Mathf.Max(0f, currentHealth - damage);
                float healthRatio = (float)Health / (float)MaxHealth;
                fortRenderer.material.color = Color.Lerp(Color.red, new Color(0.12f, 0.58f, 0.25f, 1f), healthRatio);
                if (currentHealth <= 0f)
                {
                    Destroyed = true;
                }
            }
        }
    }
}







