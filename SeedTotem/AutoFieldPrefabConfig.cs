using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using SeedTotem.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static SeedTotem.SeedTotemMod;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace SeedTotem
{
    internal class AutoFieldPrefabConfig
    {


        
        private const string localizationName = "seed_totem";
        public const string ravenTopic = "$tutorial_" + localizationName + "_topic";
        public const string ravenText = "$tutorial_" + localizationName + "_text";
        public const string ravenLabel = "$tutorial_" + localizationName + "_label";
        private const string iconPath = "icons/seed_totem.png";
        public const string requirementsFile = "seed-totem-custom-requirements.json";
        public const string prefabName = "piece_seed_totem_auto_field";
        internal static ConfigEntry<PieceLocation> configLocation;
        private PieceTable pieceTable;
        private Piece piece;

        private GameObject currentPiece;

        public AutoFieldPrefabConfig()
        {
        }

        private static RequirementConfig[] LoadJsonFile(string filename)
        {
            RequirementConfig[] defaultRecipe = new RequirementConfig[] {
                new RequirementConfig()
                {
                    Item = "FineWood",
                    Amount = 5,
                    Recover = true
                },
                 new RequirementConfig()
                {
                    Item = "GreydwarfEye",
                    Amount = 5,
                    Recover = true
                },
                  new RequirementConfig()
                {
                    Item = "SurtlingCore",
                    Amount = 1,
                    Recover = true
                },
                   new RequirementConfig()
                {
                    Item = "AncientSeed",
                    Amount = 1,
                    Recover = true
                }
            };
            if (SeedTotem.configCustomRecipe.Value)
            {
                string assetPath = SeedTotemMod.GetAssetPath(filename);
                bool fileFound = string.IsNullOrEmpty(assetPath);
                if (fileFound)
                {
                    Logger.LogWarning("File not found: " + filename + " using default recipe");
                    return defaultRecipe;
                }

                Dictionary<string, int> reqDict = ReadDict(assetPath);
                RequirementConfig[] result = new RequirementConfig[reqDict.Count];
                int i = 0;
                foreach (KeyValuePair<string, int> pair in reqDict)
                {
                    result[i++] = new RequirementConfig()
                    {
                        Item = pair.Key,
                        Amount = pair.Value,
                        Recover = true
                    };
                }
                return result;
            }
            else
            {
                return defaultRecipe;
            }
        }

        private static Dictionary<string, int> ReadDict(string assetPath)
        {
            string json = File.ReadAllText(assetPath);
            Dictionary<string, int> dictionary = (Dictionary<string, int>)SimpleJson.SimpleJson.DeserializeObject(json, typeof(Dictionary<string, int>));
            return dictionary;
        }

        private SeedTotem prefabSeedTotem;

        public void UpdateCopiedPrefab()
        {
            AssetBundle assetBundle = AssetUtils.LoadAssetBundleFromResources("seedtotem", typeof(SeedTotemMod).Assembly);
            GameObject autoFieldSkeleton = assetBundle.LoadAsset<GameObject>(prefabName);
            Sprite autoFieldIcon = assetBundle.LoadAsset<Sprite>("auto_field_icon");

            KitbashObject autoFieldKitbash = KitbashManager.Instance.AddKitbash(autoFieldSkeleton, new KitbashConfig
            {
                Layer = "piece",
                FixReferences = true,
                KitbashSources = new List<KitbashSourceConfig>
                    {
                        new KitbashSourceConfig
                        {
                            Name = "default",
                            TargetParentPath = "new",
                            SourcePrefab = "guard_stone",
                            SourcePath = "new/default",
                            Scale = Vector3.one * 0.6f
                        },
                        new KitbashSourceConfig
                        {
                            Name = "hopper",
                            TargetParentPath = "new",
                            Position = new Vector3(0.29f, 1.12f, 1.26f),
                            Rotation = Quaternion.Euler(177.7f, -258.918f, -89.55298f),
                            Scale = Vector3.one,
                            SourcePrefab = "piece_spinningwheel",
                            SourcePath = "SpinningWheel_Destruction/SpinningWheel_Destruction_SpinningWheel_Broken.016",
                            Materials = new string[]
                            {
                                "SpinningWheel_mat"
                            }
                        },
                        new KitbashSourceConfig
                        {
                            Name = "gear_left",
                            TargetParentPath = "new/pivot_left",
                            Position = new Vector3(-0.383f, 0.8181f, -0.8028001f),
                            Rotation = Quaternion.Euler(0,-90.00001f,-90.91601f ),
                            Scale = Vector3.one * 0.68285f,
                            SourcePrefab = "piece_artisanstation",
                            SourcePath = "ArtisanTable_Destruction/ArtisanTable_Destruction.007_ArtisanTable.019",
                            Materials = new string[]{
                                "ArtisanTable_Mat",
                                "TearChanal_mat"
                            }
                        },
                        new KitbashSourceConfig
                        {
                            Name = "gear_right",
                            TargetParentPath = "new/pivot_right",
                            Position = new Vector3(-0.47695f, 0.5057697f,-0.7557001f),
                            Rotation = Quaternion.Euler(0, -90.00001f, -90.91601f),
                            Scale = Vector3.one * 0.68285f,
                            SourcePrefab = "piece_artisanstation",
                            SourcePath = "ArtisanTable_Destruction/ArtisanTable_Destruction.006_ArtisanTable.018",
                            Materials = new string[]{
                                "ArtisanTable_Mat",
                                "TearChanal_mat"
                            }
                        }
                    }
            });

            autoFieldKitbash.OnKitbashApplied += () =>
            {
                SeedTotem seedTotem = autoFieldKitbash.Prefab.AddComponent<SeedTotem>();
                seedTotem.m_shape = SeedTotem.FieldShape.Rectangle;
                GameObject guardStone = PrefabManager.Instance.GetPrefab("guard_stone");
                GameObject wayEffect = Object.Instantiate(guardStone.transform.Find("WayEffect").gameObject, autoFieldKitbash.Prefab.transform);
                wayEffect.name = "WayEffect";
                seedTotem.CopyPrivateArea(guardStone.GetComponent<PrivateArea>());
                seedTotem.m_enabledEffect = wayEffect;
                RectangleProjector rectangleProjector = autoFieldKitbash.Prefab.transform.Find("AreaMarker").gameObject.AddComponent<RectangleProjector>();
                seedTotem.m_rectangleProjector = rectangleProjector;
                RequirementConfig[] defaultRecipe = new RequirementConfig[] {
                        new RequirementConfig()
                        {
                            Item = "FineWood",
                            Amount = 10,
                            Recover = true
                        },
                         new RequirementConfig()
                        {
                            Item = "GreydwarfEye",
                            Amount = 10,
                            Recover = true
                        },
                          new RequirementConfig()
                        {
                            Item = "SurtlingCore",
                            Amount = 2,
                            Recover = true
                        },
                           new RequirementConfig()
                        {
                            Item = "AncientSeed",
                            Amount = 1,
                            Recover = true
                        }
                    };
                PieceManager.Instance.AddPiece(new CustomPiece(autoFieldKitbash.Prefab, true, new PieceConfig
                {
                    PieceTable = "Hammer",
                    CraftingStation = "piece_artisanstation",
                    Requirements = defaultRecipe,
                    Icon = autoFieldIcon
                })); 
            };
        }


        internal void UpdatePieceLocation()
        {
            Logger.LogInfo("Moving Seed Totem to " + configLocation.Value);
            foreach (PieceLocation location in Enum.GetValues(typeof(PieceLocation)))
            {
                currentPiece = RemovePieceFromPieceTable(location, prefabName);
                if (currentPiece != null)
                {
                    break;
                }
            }
            if (configLocation.Value == PieceLocation.Cultivator)
            {
                GetPieceTable(configLocation.Value).m_pieces.Insert(2, currentPiece);
            }
            else
            {
                GetPieceTable(configLocation.Value).m_pieces.Add(currentPiece);
            }
            if (Player.m_localPlayer)
            {
                Player.m_localPlayer.AddKnownPiece(currentPiece.GetComponent<Piece>());
            }
        }

        private PieceTable GetPieceTable(PieceLocation location)
        {
            string pieceTableName = $"_{location}PieceTable";
            Object[] array = Resources.FindObjectsOfTypeAll(typeof(PieceTable));
            for (int i = 0; i < array.Length; i++)
            {
                PieceTable pieceTable = (PieceTable)array[i];
                string name = pieceTable.gameObject.name;
                if (pieceTableName == name)
                {
                    return pieceTable;
                }
            }
            return null;
        }

        private GameObject GetPieceFromPieceTable(PieceLocation location, string pieceName)
        {
            PieceTable pieceTable = GetPieceTable(location);
            int currentPosition = pieceTable.m_pieces.FindIndex(piece => piece.name == pieceName);
            if (currentPosition >= 0)
            {
                Logger.LogInfo("Found Piece " + pieceName + " at position " + currentPosition);
                GameObject @object = pieceTable.m_pieces[currentPosition];
                pieceTable.m_pieces.RemoveAt(currentPosition);
                return @object;
            }
            return null;
        }

        private GameObject RemovePieceFromPieceTable(PieceLocation location, string pieceName)
        {
            Logger.LogDebug("Removing " + pieceName + " from " + location);
            PieceTable pieceTable = GetPieceTable(location);
            int currentPosition = pieceTable.m_pieces.FindIndex(piece => piece.name == pieceName);
            if (currentPosition >= 0)
            {
                Logger.LogDebug("Found Piece " + pieceName + " at position " + currentPosition);
                GameObject @object = pieceTable.m_pieces[currentPosition];
                pieceTable.m_pieces.RemoveAt(currentPosition);
                return @object;
            }

            return null;
        }

        private Piece SetPieceTablePosition(string pieceTableName, string pieceName, int position)
        {
            Logger.LogInfo("Moving " + pieceName + " to position " + position + " in " + pieceTableName);
            Object[] array = Resources.FindObjectsOfTypeAll(typeof(PieceTable));
            for (int i = 0; i < array.Length; i++)
            {
                pieceTable = (PieceTable)array[i];
                string name = pieceTable.gameObject.name;
                if (pieceTableName == name)
                {
                    Logger.LogInfo("Found PieceTable " + pieceTableName);
                    int currentPosition = pieceTable.m_pieces.FindIndex(piece => piece.name == pieceName);
                    if (currentPosition >= 0)
                    {
                        Logger.LogInfo("Found Piece " + pieceName + " at position " + currentPosition);
                        GameObject @object = pieceTable.m_pieces[currentPosition];
                        pieceTable.m_pieces.RemoveAt(currentPosition);
                        Logger.LogInfo("Moving to position " + position);
                        pieceTable.m_pieces.Insert(position, @object);
                        return @object.GetComponent<Piece>();
                    }
                }
            }
            return null;
        }
    }
}