﻿using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Managers;
using PlanBuild.Plans;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanBuild.Blueprints
{
    internal class BlueprintManager
    {
        internal static string BlueprintPath = Path.Combine(BepInEx.Paths.BepInExRootPath, "config", nameof(PlanBuild), "blueprints");

        internal float selectionRadius = 10.0f;

        internal float cameraOffsetMake = 0.0f;
        internal float cameraOffsetPlace = 5.0f;
        internal bool updateCamera = true;

        internal readonly Dictionary<string, Blueprint> m_blueprints = new Dictionary<string, Blueprint>();
        internal readonly Dictionary<int, List<PlanPiece>> m_worldBlueprints = new Dictionary<int, List<PlanPiece>>();

        internal static ConfigEntry<float> rayDistanceConfig;
        internal static ConfigEntry<bool> allowDirectBuildConfig;
        internal static ConfigEntry<KeyCode> planSwitchConfig;
        internal static ButtonConfig planSwitchButton;

        private static BlueprintManager _instance;

        public static BlueprintManager Instance
        {
            get
            {
                if (_instance == null) _instance = new BlueprintManager();
                return _instance;
            }
        }

        internal void Init()
        {
            //TODO: Client only - how to do? or just ignore - there are no bps and maybe someday there will be a server-wide directory of blueprints for sharing :)

            // Load Blueprints
            LoadKnownBlueprints();

            // KeyHints
            CreateCustomKeyHints();
            
            // Hooks 
            On.PieceTable.UpdateAvailable += OnUpdateAvailable;
            On.Player.PlacePiece += BeforePlaceBlueprintPiece;
            On.GameCamera.UpdateCamera += AdjustCameraHeight;
            On.Player.UpdatePlacement += ShowBlueprintCapture;

            Jotunn.Logger.LogInfo("BlueprintManager Initialized");
        }

        private void OnUpdateAvailable(On.PieceTable.orig_UpdateAvailable orig, PieceTable self, HashSet<string> knownRecipies, Player player, bool hideUnavailable, bool noPlacementCost)
        {
            RegisterKnownBlueprints();
            player.UpdateKnownRecipesList();
            orig(self, knownRecipies, player, hideUnavailable, noPlacementCost);
        }

        

        public void RegisterPlanPiece(int blueprintID, PlanPiece planPiece)
        {

        }

        private void UndoLastBlueprint()
        {
            
        }

        private void LoadKnownBlueprints()
        {
            Jotunn.Logger.LogMessage("Loading known blueprints");

            if (!Directory.Exists(BlueprintPath))
            {
                Directory.CreateDirectory(BlueprintPath);
            }

            List<string> blueprintFiles = new List<string>();
            blueprintFiles.AddRange(Directory.EnumerateFiles(".", "*.blueprint", SearchOption.AllDirectories));
            blueprintFiles.AddRange(Directory.EnumerateFiles(".", "*.vbuild", SearchOption.AllDirectories));

            // Try to load all saved blueprints
            foreach (var absoluteFilePath in blueprintFiles)
            {
                string name = Path.GetFileNameWithoutExtension(absoluteFilePath);
                if (!m_blueprints.ContainsKey(name))
                {
                    var bp = new Blueprint(name);
                    if (bp.Load(absoluteFilePath))
                    {
                        m_blueprints.Add(name, bp);
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning($"Could not load blueprint {absoluteFilePath}");
                    }
                }
            }
        }

        private void CreateCustomKeyHints()
        {
            allowDirectBuildConfig = PlanBuildPlugin.Instance.Config.Bind("Blueprint Rune", "Allow direct build", false,
                new ConfigDescription("Allow placement of blueprints without materials", null, new object[] { new ConfigurationManagerAttributes() { IsAdminOnly = true } }));

            rayDistanceConfig = PlanBuildPlugin.Instance.Config.Bind("Blueprint Rune", "Place distance", 20f,
                new ConfigDescription("Place distance while using the Blueprint Rune", new AcceptableValueRange<float>(0f, 1f)));

            planSwitchConfig = PlanBuildPlugin.Instance.Config.Bind("Blueprint Rune", "Rune mode toggle key", KeyCode.P,
                new ConfigDescription("Hotkey to switch between rune modes"));

            planSwitchButton = new ButtonConfig
            {
                Name = "Rune mode toggle key",
                Key = planSwitchConfig.Value,
                HintToken = "$hud_bp_switch_to_plan_mode"
            };
            InputManager.Instance.AddButton(PlanBuildPlugin.PluginGUID, planSwitchButton);

            KeyHintConfig KHC_default = new KeyHintConfig
            {
                Item = "BlueprintRune",
                ButtonConfigs = new[]
                {
                    planSwitchButton,
                    new ButtonConfig { Name = "BuildMenu", HintToken = "$hud_buildmenu" }
                }
            };
            GUIManager.Instance.AddKeyHint(KHC_default);

            KeyHintConfig KHC_make = new KeyHintConfig
            {
                Item = "BlueprintRune",
                Piece = "make_blueprint",
                ButtonConfigs = new[]
                {
                    planSwitchButton,
                    new ButtonConfig { Name = "Attack", HintToken = "$hud_bpcapture" },
                    new ButtonConfig { Name = "Scroll", Axis = "Mouse ScrollWheel", HintToken = "$hud_bpradius" },
                }
            };
            GUIManager.Instance.AddKeyHint(KHC_make);

            foreach (var entry in m_blueprints)
            {
                entry.Value.CreateKeyHint();
            }
        }

        private void RegisterKnownBlueprints()
        { 
            // Client only
            if (!ZNet.instance.IsDedicated())
            { 
                // Create prefabs for all known blueprints
                foreach (var bp in Instance.m_blueprints.Values)
                { 
                    bp.CreatePrefab(); 
                }
            }
        }

        /// <summary>
        ///     Incept placing of the meta pieces.
        ///     Cancels the real placement of the placeholder pieces.
        /// </summary>
        private bool BeforePlaceBlueprintPiece(On.Player.orig_PlacePiece orig, Player self, Piece piece)
        {
            // Client only
            if (!ZNet.instance.IsDedicated())
            {
                // Capture a new blueprint
                if (piece.name == "make_blueprint")
                {
                    var circleProjector = self.m_placementGhost.GetComponent<CircleProjector>();
                    if (circleProjector != null)
                    {
                        Object.Destroy(circleProjector);
                    }

                    var bpname = $"blueprint{Instance.m_blueprints.Count() + 1:000}";
                    Jotunn.Logger.LogInfo($"Capturing blueprint {bpname}");

                    var bp = new Blueprint(bpname);
                    Vector3 capturePosition = self.m_placementMarkerInstance.transform.position;
                    if (bp.Capture(capturePosition, Instance.selectionRadius, 1.0f))
                    {
                        TextInput.instance.m_queuedSign = new Blueprint.BlueprintSaveGUI(bp);
                        TextInput.instance.Show($"Save Blueprint ({bp.GetPieceCount()} pieces captured)", bpname, 50);
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning($"Could not capture blueprint {bpname}");
                    }

                    // Reset Camera offset
                    Instance.cameraOffsetMake = 0f;

                    // Don't place the piece and clutter the world with it
                    return false;
                } 
                // Place a known blueprint
                if (Player.m_localPlayer.m_placementStatus == Player.PlacementStatus.Valid 
                    && piece.name != BlueprintRunePrefab.BlueprintSnapPointName 
                    && piece.name != BlueprintRunePrefab.BlueprintCenterPointName
                    && piece.name.StartsWith("piece_blueprint"))
                {
                    Blueprint bp = Instance.m_blueprints[piece.m_name];
                    var transform = self.m_placementGhost.transform;
                    var position = self.m_placementGhost.transform.position;
                    var rotation = self.m_placementGhost.transform.rotation;

                    bool crouching = ZInput.GetButton("Crouch");
                    if (crouching && !allowDirectBuildConfig.Value)
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$msg_direct_build_disabled");
                        return false;
                    }

                    if (ZInput.GetButton("AltPlace"))
                    {
                        Vector2 extent = bp.GetExtent();
                        FlattenTerrain.FlattenForBlueprint(transform, extent.x, extent.y, bp.m_pieceEntries);
                    }

                    uint cntEffects = 0u;
                    uint maxEffects = 10u;

                    foreach (var entry in bp.m_pieceEntries)
                    {
                        // Final position
                        Vector3 entryPosition = position + transform.forward * entry.posZ + transform.right * entry.posX + new Vector3(0, entry.posY, 0);

                        // Final rotation
                        Quaternion entryQuat = new Quaternion(entry.rotX, entry.rotY, entry.rotZ, entry.rotW);
                        entryQuat.eulerAngles += rotation.eulerAngles;

                        // Get the prefab of the piece or the plan piece
                        string prefabName = entry.name;
                        if (!allowDirectBuildConfig.Value || !crouching)
                        {
                            prefabName += PlanPiecePrefab.PlannedSuffix;
                        }

                        GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
                        if (!prefab)
                        {
                            Jotunn.Logger.LogWarning(entry.name + " not found, you are probably missing a dependency for blueprint " + bp.m_name + ", not placing @ " + entryPosition);
                            continue;
                        }

                        // Instantiate a new object with the new prefab
                        GameObject gameObject = Object.Instantiate(prefab, entryPosition, entryQuat);

                        // Register special effects
                        CraftingStation craftingStation = gameObject.GetComponentInChildren<CraftingStation>();
                        if (craftingStation)
                        {
                            self.AddKnownStation(craftingStation);
                        }
                        Piece newpiece = gameObject.GetComponent<Piece>();
                        if (newpiece)
                        {
                            newpiece.SetCreator(self.GetPlayerID());
                        }
                        PrivateArea privateArea = gameObject.GetComponent<PrivateArea>();
                        if (privateArea)
                        {
                            privateArea.Setup(Game.instance.GetPlayerProfile().GetName());
                        }
                        WearNTear wearntear = gameObject.GetComponent<WearNTear>();
                        if (wearntear)
                        {
                            wearntear.OnPlaced();
                        }
                        TextReceiver textReceiver = gameObject.GetComponent<TextReceiver>();
                        if (textReceiver != null)
                        {
                            textReceiver.SetText(entry.additionalInfo);
                        }

                        // Limited build effects
                        if (cntEffects < maxEffects)
                        {
                            newpiece.m_placeEffect.Create(gameObject.transform.position, rotation, gameObject.transform, 1f);
                            self.AddNoise(50f);
                            cntEffects++;
                        }

                        // Count up player builds
                        Game.instance.GetPlayerProfile().m_playerStats.m_builds++;
                    }

                    // Reset Camera offset
                    Instance.cameraOffsetPlace = 5f;

                    // Dont set the blueprint piece and clutter the world with it
                    return false;
                }
            }

            return orig(self, piece);
        }

        /// <summary>
        ///     Add some camera height while planting a blueprint
        /// </summary>
        private void AdjustCameraHeight(On.GameCamera.orig_UpdateCamera orig, GameCamera self, float dt)
        {
            orig(self, dt);

            if (Player.m_localPlayer)
            {
                if (Player.m_localPlayer.InPlaceMode())
                {
                    if (Player.m_localPlayer.m_placementGhost)
                    {
                        var pieceName = Player.m_localPlayer.m_placementGhost.name;
                        if (pieceName.StartsWith("make_blueprint"))
                        {
                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                float minOffset = 0f;
                                float maxOffset = 20f;
                                if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                                {
                                    Instance.cameraOffsetMake = Mathf.Clamp(Instance.cameraOffsetMake += 1f, minOffset, maxOffset);
                                }

                                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                                {
                                    Instance.cameraOffsetMake = Mathf.Clamp(Instance.cameraOffsetMake -= 1f, minOffset, maxOffset);
                                }
                            }

                            if (updateCamera)
                            {
                                self.transform.position += new Vector3(0, Instance.cameraOffsetMake, 0);
                            }
                        }
                        if (pieceName.StartsWith("piece_blueprint"))
                        {
                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                // TODO: base min/max off of selected piece dimensions
                                float minOffset = 2f;
                                float maxOffset = 20f;
                                if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                                {
                                    Instance.cameraOffsetPlace = Mathf.Clamp(Instance.cameraOffsetPlace += 1f, minOffset, maxOffset);
                                }

                                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                                {
                                    Instance.cameraOffsetPlace = Mathf.Clamp(Instance.cameraOffsetPlace -= 1f, minOffset, maxOffset);
                                }
                            }

                            self.transform.position += new Vector3(0, Instance.cameraOffsetPlace, 0);
                        }
                    }
                }
            }
        }

        public int HighlightCapture(Vector3 startPosition, float startRadius, float radiusDelta)
        {
            int capturedPieces = 0;
            foreach (var piece in Piece.m_allPieces)
            {
                if (Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(piece.transform.position.x, piece.transform.position.z)) < startRadius)
                {
                    WearNTear wearNTear = piece.GetComponent<WearNTear>();
                    if (wearNTear)
                    {
                        wearNTear.Highlight();
                    }
                    capturedPieces++;
                }
            }
            return capturedPieces;
        }

        const float HighlightTimeout = 0.5f;
        float m_lastHightlight = 0;

        /// <summary>
        ///     Show and change blueprint selection radius
        /// </summary>
        private void ShowBlueprintCapture(On.Player.orig_UpdatePlacement orig, Player self, bool takeInput, float dt)
        {
            orig(self, takeInput, dt);

            if (self.m_placementGhost)
            {
                var piece = self.m_placementGhost.GetComponent<Piece>();
                if (piece != null)
                {
                    if (piece.name == "make_blueprint" && !piece.IsCreator())
                    {
                        if (!self.m_placementMarkerInstance)
                        {
                            return;
                        }

                        self.m_maxPlaceDistance = 50f;

                        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl))
                        {
                            if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                            {
                                Instance.selectionRadius -= 1f;
                                if (Instance.selectionRadius < 2f)
                                {
                                    Instance.selectionRadius = 2f;
                                }
                            }

                            if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                            {
                                Instance.selectionRadius += 1f;
                            }
                        }


                        var circleProjector = self.m_placementMarkerInstance.GetComponent<CircleProjector>();
                        if (circleProjector == null)
                        {
                            circleProjector = self.m_placementMarkerInstance.AddComponent<CircleProjector>();
                            circleProjector.m_prefab = PrefabManager.Instance.GetPrefab("piece_workbench").GetComponentInChildren<CircleProjector>().m_prefab;

                            // Force calculation of segment count
                            circleProjector.m_radius = -1;
                            circleProjector.Start();
                        }

                        if (circleProjector.m_radius != Instance.selectionRadius)
                        {
                            circleProjector.m_radius = Instance.selectionRadius;
                            circleProjector.m_nrOfSegments = (int)circleProjector.m_radius * 4;
                            circleProjector.Update();
                            Jotunn.Logger.LogDebug($"Setting radius to {Instance.selectionRadius}");
                        }

                        if (Time.time > m_lastHightlight + HighlightTimeout)
                        {
                            int capturePieces = HighlightCapture(self.m_placementMarkerInstance.transform.position, Instance.selectionRadius, 1.0f);
                            m_lastHightlight = Time.time;
                        }
                    }
                   
                    else if (piece.name.StartsWith("piece_blueprint"))
                    {
                        self.m_maxPlaceDistance = rayDistanceConfig.Value;

                        // Destroy placement marker instance to get rid of the circleprojector
                        if (self.m_placementMarkerInstance)
                        {
                            Object.DestroyImmediate(self.m_placementMarkerInstance);
                        }

                        // Reset rotation when changing camera
                        if (Input.GetAxis("Mouse ScrollWheel") != 0f && Input.GetKey(KeyCode.LeftShift))
                        {

                            if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                            {
                                self.m_placeRotation++;
                            }

                            if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                            {
                                self.m_placeRotation--;
                            }

                        }
                    }
                    else
                    {
                        // Destroy placement marker instance to get rid of the circleprojector
                        if (self.m_placementMarkerInstance)
                        {
                            Object.DestroyImmediate(self.m_placementMarkerInstance);
                        }

                        // Restore placementDistance
                        // default value, if we introduce config stuff for this, then change it here!
                        self.m_maxPlaceDistance = 8;
                    }
                }
            }
        }
    }
}
