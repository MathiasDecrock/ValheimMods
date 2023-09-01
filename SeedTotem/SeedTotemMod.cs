﻿// SeedTotem
// a Valheim mod skeleton using Jötunn
//
// File:    SeedTotem.cs
// Project: SeedTotem

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SeedTotem
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class SeedTotemMod : BaseUnityPlugin
    {
        public const string PluginGUID = "marcopogo.SeedTotem";
        public const string PluginName = "Seed Totem";
        public const string PluginVersion = "4.3.2";
        public ConfigEntry<int> nexusID;
        private SeedTotemPrefabConfig seedTotemPrefabConfig;
        private Harmony harmony; 

        public enum PieceLocation
        {
            Hammer, Cultivator
        }

        public void Awake()
        {
            harmony = new Harmony(PluginGUID);
            harmony.PatchAll();
            On.WearNTear.Damage += SeedTotem.OnDamage;

            CreateConfiguration();

            PrefabManager.OnVanillaPrefabsAvailable += AddCustomPrefabs;

            SeedTotem.configGlowColor.SettingChanged += SettingsChanged;
            SeedTotem.configLightColor.SettingChanged += SettingsChanged;
            SeedTotem.configLightIntensity.SettingChanged += SettingsChanged;
            SeedTotem.configFlareColor.SettingChanged += SettingsChanged;
            SeedTotem.configFlareSize.SettingChanged += SettingsChanged;

            SeedTotemPrefabConfig.configLocation.SettingChanged += UpdatePieceLocation;

            Jotunn.Managers.PieceManager.OnPiecesRegistered += OnPiecesRegistered;
        }

        private void CreateConfiguration()
        {
     
            //server configs
            SeedTotem.configMaxRadius = Config.Bind("Server", "Max Dispersion Radius", defaultValue: 20f, new ConfigDescription("Max dispersion radius of the Seed totem.", new AcceptableValueRange<float>(2f, 64f), new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configDefaultRadius = Config.Bind("Server", "Default Dispersion Radius", defaultValue: 5f, new ConfigDescription("Default dispersion radius of the Seed totem.", new AcceptableValueRange<float>(2f, SeedTotem.configMaxRadius.Value), new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configDispersionTime = Config.Bind("Server", "Dispersion time", defaultValue: 10f, new ConfigDescription("Time (in seconds) between each dispersion (low values can cause lag)", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configMargin = Config.Bind("Server", "Space requirement margin", defaultValue: 0.1f, new ConfigDescription("Extra distance to make sure plants have enough space", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configDispersionCount = Config.Bind("Server", "Dispersion count", defaultValue: 5, new ConfigDescription("Maximum number of plants to place when dispersing (high values can cause lag)", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configMaxRetries = Config.Bind("Server", "Max retries", defaultValue: 8, new ConfigDescription("Maximum number of placement tests on each dispersion (high values can cause lag)", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configHarvestOnHit = Config.Bind("Server", "Harvest on hit", defaultValue: true, new ConfigDescription("Should the Seed totem send out a wave to pick all pickables in radius when hit?", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configAdminOnlyRadius = Config.Bind("Server", "Only admin can change radius", defaultValue: true, new ConfigDescription("Should only admins be able to change the radius of individual Seed totems?", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configCheckCultivated = Config.Bind("Server", "Check for cultivated ground", defaultValue: true, new ConfigDescription("Should the Seed totem also check for cultivated land?", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configCheckBiome = Config.Bind("Server", "Check for correct biome", defaultValue: true, new ConfigDescription("Should the Seed totem also check for the correct biome?", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotemPrefabConfig.configRecipe = Config.Bind("Server", "Seed totem requirements", "FineWood:5,GreydwarfEye:5,SurtlingCore:1,AncientSeed:1", new ConfigDescription("Requirements to build the Seed totem", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            AutoFieldPrefabConfig.configRecipe = Config.Bind("Server", "Advanced seed totem requirements", "FineWood:10,GreydwarfEye:10,SurtlingCore:2,AncientSeed:1", new ConfigDescription("Requirements to build the Advanced seed totem", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            SeedTotem.configMaxSeeds = Config.Bind("Server", "Max seeds in totem (0 is no limit)", defaultValue: 0, new ConfigDescription("Maximum number of seeds in each totem, 0 is no limit", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            //client configs 
            SeedTotem.configShowQueue = Config.Bind("UI", "Show queue", defaultValue: true, new ConfigDescription("Show the current queue on hover"));
            SeedTotem.configGlowColor = Config.Bind("Graphical", "Glow lines color", new Color(0f, 0.8f, 0f, 1f), new ConfigDescription("Color of the glowing lines on the Seed totem"));
            SeedTotem.configLightColor = Config.Bind("Graphical", "Glow light color", new Color(0f, 0.8f, 0f, 0.05f), new ConfigDescription("Color of the light from the Seed totem"));
            SeedTotem.configLightIntensity = Config.Bind("Graphical", "Glow light intensity", 3f, new ConfigDescription("Intensity of the light flare from the Seed totem"));
            SeedTotem.configFlareColor = Config.Bind("Graphical", "Glow flare color", new Color(0f, 0.8f, 0f, 0.1f), new ConfigDescription("Color of the light flare from the Seed totem"));
            SeedTotem.configFlareSize = Config.Bind("Graphical", "Glow flare size", 3f, new ConfigDescription("Size of the light flare from the Seed totem"));

            SeedTotem.configRadiusChange = Config.Bind("Input", "Radius size change for each keypress", 1f, new ConfigDescription("How much the radius will change for each keypress"));
            SeedTotem.configRadiusIncrementButton = Config.Bind("Input", "Increment seed totem radius", new KeyboardShortcut(KeyCode.KeypadPlus));
            SeedTotem.configRadiusDecrementButton = Config.Bind("Input", "Decrement seed totem radius", new KeyboardShortcut(KeyCode.KeypadMinus));
            SeedTotem.configWidthIncrementButton = Config.Bind("Input", "Increment seed totem width", new KeyboardShortcut(KeyCode.RightArrow));
            SeedTotem.configWidthDecrementButton = Config.Bind("Input", "Decrement seed totem width", new KeyboardShortcut(KeyCode.LeftArrow));
            SeedTotem.configLengthIncrementButton = Config.Bind("Input", "Increment seed totem length", new KeyboardShortcut(KeyCode.UpArrow));
            SeedTotem.configLengthDecrementButton = Config.Bind("Input", "Decrement seed totem length", new KeyboardShortcut(KeyCode.DownArrow));
            nexusID = Config.Bind("General", "NexusID", 876, new ConfigDescription("Nexus mod ID for updates", new AcceptableValueList<int>(new int[] { 876 })));

            SeedTotemPrefabConfig.configLocation = Config.Bind("UI", "Build menu", PieceLocation.Hammer, "In which build menu is the Seed totem located");
        }

        private void OnPiecesRegistered()
        {
            seedTotemPrefabConfig.UpdatePieceLocation();
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
        
        private void AddCustomPrefabs()
        {
            AssetBundle assetBundle = AssetUtils.LoadAssetBundleFromResources("seedtotem", typeof(SeedTotemMod).Assembly);
            try
            {
                seedTotemPrefabConfig = new SeedTotemPrefabConfig();

                var seedTotemPrefab = PrefabManager.Instance.CreateClonedPrefab(SeedTotemPrefabConfig.prefabName, "guard_stone");
                seedTotemPrefabConfig.UpdateCopiedPrefab(assetBundle, seedTotemPrefab);

                AutoFieldPrefabConfig autoFieldPrefabConfig = new AutoFieldPrefabConfig();
                autoFieldPrefabConfig.UpdateCopiedPrefab(assetBundle);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding cloned item: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomPrefabs;
                assetBundle?.Unload(false);
            }
        }

        private void UpdatePieceLocation(object sender, EventArgs e)
        {
            seedTotemPrefabConfig.UpdatePieceLocation();
        }

        private void SettingsChanged(object sender, EventArgs e)
        {
            SeedTotem.SettingsUpdated();
        }

        public static string GetAssetPath(string assetName, bool isDirectory = false)
        {
            string text = Path.Combine(BepInEx.Paths.PluginPath, "SeedTotem", assetName);
            if (isDirectory)
            {
                if (!Directory.Exists(text))
                {
                    Assembly assembly = typeof(SeedTotemMod).Assembly;
                    text = Path.Combine(Path.GetDirectoryName(assembly.Location), assetName);
                    if (!Directory.Exists(text))
                    {
                        Jotunn.Logger.LogWarning($"Could not find directory ({assetName}).");
                        return null;
                    }
                }
                return text;
            }
            if (!File.Exists(text))
            {
                Assembly assembly = typeof(SeedTotemMod).Assembly;
                text = Path.Combine(Path.GetDirectoryName(assembly.Location), assetName);
                if (!File.Exists(text))
                {
                    Jotunn.Logger.LogWarning($"Could not find asset ({assetName}).");
                    return null;
                }
            }
            return text;
        }

#if DEBUG
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            { // Set a breakpoint here to break on F9 key press
                Jotunn.Logger.LogInfo("Right here");
            }
        }

#endif
    }
}