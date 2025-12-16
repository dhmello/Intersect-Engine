using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Config;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using GwenLabel = Intersect.Client.Framework.Gwen.Control.Label;

namespace Intersect.Client.Interface.Game.Minimap
{
    public partial class MinimapWindow
    {
        private WindowControl mMinimapWindow;
        private ImagePanel[,] mMinimapTile;

        private const int MINIMAP_WINDOW_SIZE = 128; // Reduzido para performance
        private const int TILE_SIZE = 1;

        private readonly Color mUnexploredColor = new Color(0, 0, 0);
        private readonly Color mOutboundColor = new Color(40, 40, 40);
        private readonly Color mPlayerColor = new Color(255, 255, 0);

        [Serializable]
        private class ExploredTileData
        {
            public string MapId { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int R { get; set; }
            public int G { get; set; }
            public int B { get; set; }
            public long ExploredTimestamp { get; set; }
        }

        [Serializable]
        private class ExplorationDataContainer
        {
            public List<ExploredTileData> ExploredTiles { get; set; } = new List<ExploredTileData>();
            public Dictionary<string, MapPositionData> MapPositions { get; set; } = new Dictionary<string, MapPositionData>();
        }

        [Serializable]
        private class MapPositionData
        {
            public int GridX { get; set; }
            public int GridY { get; set; }
        }

        private class InternalTileData
        {
            public Color TileColor;
            public long ExploredTimestamp;
        }

        private readonly Dictionary<Guid, Dictionary<string, InternalTileData>> mExploredData =
            new Dictionary<Guid, Dictionary<string, InternalTileData>>();

        private struct VisualTileCache
        {
            public int WorldX;
            public int WorldY;
            public Guid MapId;
            public Color Color;
        }

        private VisualTileCache[,] mVisualCache;
        private long mLastUpdateTime;
        private const long UPDATE_INTERVAL_MS = 200; // Aumentado para performance
        private Point mLastPlayerPosition;
        private Guid mLastPlayerMapId;
        private float mLastZoomLevel;

        private const int EXPLORATION_RADIUS = 5; // Reduzido para performance

        private float mZoomLevel = 0.1f;
        private const float MAX_ZOOM_LEVEL = 10f;
        private const float MIN_ZOOM_LEVEL = 0.1f;

        private Button mZoomInButton;
        private Button mZoomOutButton;
        private Button mClearExplorationButton;
        private GwenLabel mZoomLabel;
        private GwenLabel mMapNameLabel;

        private IGameTexture MinimapIconsTexture;
        private bool mForceFullUpdate = true;

        private string ExplorationDataPath => Path.Combine(
            "resources",
            "minimap",
            $"explored_{Globals.Me?.Name ?? "default"}.json"
        );

        private Dictionary<Guid, (int gridX, int gridY)> mMapPositions = new Dictionary<Guid, (int, int)>();
        private Dictionary<string, Color> mTileColorCache = new Dictionary<string, Color>();

        public MinimapWindow(Canvas gameCanvas)
        {
            mMinimapWindow = new WindowControl(gameCanvas, "Map", false, "MinimapWindow");
            mMinimapWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());

            MinimapIconsTexture = Graphics.Renderer.WhitePixel;

            LoadExplorationData();
            InitializeZoomControls();
            InitializeTiles();

            mMinimapWindow.IsClosable = true; // Permitir fechar

            // Posição topo direito (seguindo layout JSON)
            mMinimapWindow.Width = MINIMAP_WINDOW_SIZE + 16;
            mMinimapWindow.Height = MINIMAP_WINDOW_SIZE + 70; // Ajustar altura para melhor acomodação

            mMinimapWindow.X = gameCanvas.Width - mMinimapWindow.Width - 8;
            mMinimapWindow.Y = 8;

            mLastPlayerPosition = new Point(-1, -1);
            mLastPlayerMapId = Guid.Empty;
            mLastZoomLevel = mZoomLevel;
        }

        private void InitializeZoomControls()
        {
            var controlY = 8;
            var controlHeight = 20;
            var spacing = 2;
            var currentX = 8;

            // Botão de fechar (canto superior direito) - ajustar posição
            var closeButton = new Button(mMinimapWindow, "CloseButton")
            {
                Text = "X",
                Width = 20,
                Height = controlHeight,
                X = MINIMAP_WINDOW_SIZE - 16, // Ajustar para não cortar
                Y = controlY
            };
            closeButton.Clicked += (sender, args) => Hide();

            // Zoom In
            mZoomInButton = new Button(mMinimapWindow, "ZoomInButton")
            {
                Text = "+",
                Width = 20,
                Height = controlHeight,
                X = currentX,
                Y = controlY
            };
            mZoomInButton.Clicked += ZoomInButton_Clicked;
            currentX += 20 + spacing;

            // Zoom Out
            mZoomOutButton = new Button(mMinimapWindow, "ZoomOutButton")
            {
                Text = "-",
                Width = 20,
                Height = controlHeight,
                X = currentX,
                Y = controlY
            };
            mZoomOutButton.Clicked += ZoomOutButton_Clicked;
            currentX += 20 + spacing;

            // Zoom Label
            mZoomLabel = new GwenLabel(mMinimapWindow, "ZoomLabel")
            {
                Text = "0.1x",
                Width = 35,
                Height = controlHeight,
                X = currentX,
                Y = controlY + 2,
                TextAlign = Framework.Gwen.Pos.Center
            };
            currentX += 35 + spacing;

            // Clear Button
            mClearExplorationButton = new Button(mMinimapWindow, "ClearExplorationButton")
            {
                Text = "C",
                Width = 20,
                Height = controlHeight,
                X = currentX,
                Y = controlY
            };
            mClearExplorationButton.Clicked += ClearExplorationButton_Clicked;
            mClearExplorationButton.SetToolTipText("Clear exploration data");

            // Label do nome do mapa (embaixo dos controles)
            mMapNameLabel = new GwenLabel(mMinimapWindow, "MapNameLabel")
            {
                Text = "",
                Width = MINIMAP_WINDOW_SIZE,
                Height = 18,
                X = 8,
                Y = MINIMAP_WINDOW_SIZE + 38,
                TextAlign = Framework.Gwen.Pos.Center
            };
        }

        private void InitializeTiles()
        {
            var size = MINIMAP_WINDOW_SIZE;
            mMinimapTile = new ImagePanel[size, size];
            mVisualCache = new VisualTileCache[size, size];

            // Ajustar Y para ficar abaixo dos controles (40px de margem para mais espaço)
            var tileStartY = 40;

            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < size; y++)
                {
                    mMinimapTile[x, y] = new ImagePanel(mMinimapWindow, $"MinimapTile{x}_{y}")
                    {
                        Texture = MinimapIconsTexture,
                        Width = TILE_SIZE,
                        Height = TILE_SIZE,
                        X = 8 + x * TILE_SIZE,
                        Y = tileStartY + y * TILE_SIZE
                    };

                    mVisualCache[x, y] = new VisualTileCache
                    {
                        WorldX = -1,
                        WorldY = -1,
                        MapId = Guid.Empty,
                        Color = mUnexploredColor
                    };
                }
            }
        }

        private void ZoomInButton_Clicked(Base sender, MouseButtonState arguments)
        {
            if (mZoomLevel > MIN_ZOOM_LEVEL)
            {
                mZoomLevel = Math.Max(MIN_ZOOM_LEVEL, mZoomLevel * 0.8f);
                OnZoomChanged();
            }
        }

        private void ZoomOutButton_Clicked(Base sender, MouseButtonState arguments)
        {
            if (mZoomLevel < MAX_ZOOM_LEVEL)
            {
                mZoomLevel = Math.Min(MAX_ZOOM_LEVEL, mZoomLevel * 1.25f);
                OnZoomChanged();
            }
        }

        private void ClearExplorationButton_Clicked(Base sender, MouseButtonState arguments)
        {
            ClearExplorationData();
            mForceFullUpdate = true;
        }

        private void OnZoomChanged()
        {
            mZoomLabel.Text = $"{mZoomLevel:F1}x";
            mForceFullUpdate = true;
            mLastZoomLevel = mZoomLevel;
        }

        private void UpdateMapName()
        {
            if (Globals.Me?.MapInstance != null)
            {
                var mapInstance = Globals.Me.MapInstance as MapInstance;
                if (mapInstance != null)
                {
                    var mapName = mapInstance.Name ?? "Unknown";
                    mMapNameLabel.Text = mapName;
                    mMinimapWindow.Title = mapName;
                }
            }
        }

        public void Update()
        {
            if (mMinimapWindow.IsHidden || Globals.Me == null)
            {
                return;
            }

            var currentTime = Timing.Global.Milliseconds;
            var playerMoved = mLastPlayerPosition.X != Globals.Me.X ||
                             mLastPlayerPosition.Y != Globals.Me.Y;

            var mapChanged = mLastPlayerMapId != Globals.Me.MapId;

            if (playerMoved || mapChanged)
            {
                ExploreAreaAroundPlayer();
                BuildMapPositionCache();
            }

            if (mapChanged)
            {
                if (mLastPlayerMapId != Guid.Empty)
                {
                    SaveExplorationData();
                }
                UpdateMapName();
                mForceFullUpdate = true;
            }

            if (!playerMoved && !mapChanged && !mForceFullUpdate &&
                (currentTime - mLastUpdateTime) < UPDATE_INTERVAL_MS)
            {
                return;
            }

            mLastUpdateTime = currentTime;
            mLastPlayerPosition = new Point(Globals.Me.X, Globals.Me.Y);
            mLastPlayerMapId = Globals.Me.MapId;

            UpdateMinimapVisuals();
            mForceFullUpdate = false;
        }

        private void BuildMapPositionCache()
        {
            if (Globals.Me?.MapInstance == null) return;

            var currentMapId = Globals.Me.MapId;
            if (!mMapPositions.ContainsKey(currentMapId))
            {
                mMapPositions[currentMapId] = (0, 0);
            }

            var currentMap = Globals.Me.MapInstance as MapInstance;
            if (currentMap != null)
            {
                var currentPos = mMapPositions[currentMapId];

                if (currentMap.Up != Guid.Empty && !mMapPositions.ContainsKey(currentMap.Up))
                {
                    mMapPositions[currentMap.Up] = (currentPos.Item1, currentPos.Item2 - 1);
                }
                if (currentMap.Down != Guid.Empty && !mMapPositions.ContainsKey(currentMap.Down))
                {
                    mMapPositions[currentMap.Down] = (currentPos.Item1, currentPos.Item2 + 1);
                }
                if (currentMap.Left != Guid.Empty && !mMapPositions.ContainsKey(currentMap.Left))
                {
                    mMapPositions[currentMap.Left] = (currentPos.Item1 - 1, currentPos.Item2);
                }
                if (currentMap.Right != Guid.Empty && !mMapPositions.ContainsKey(currentMap.Right))
                {
                    mMapPositions[currentMap.Right] = (currentPos.Item1 + 1, currentPos.Item2);
                }
            }
        }

        private void ExploreAreaAroundPlayer()
        {
            var currentMap = Globals.Me.MapInstance as MapInstance;
            if (currentMap == null) return;

            var playerX = Globals.Me.X;
            var playerY = Globals.Me.Y;
            var mapId = currentMap.Id;

            if (!mExploredData.ContainsKey(mapId))
            {
                mExploredData[mapId] = new Dictionary<string, InternalTileData>();
            }

            var exploredMap = mExploredData[mapId];
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (var dx = -EXPLORATION_RADIUS; dx <= EXPLORATION_RADIUS; dx++)
            {
                for (var dy = -EXPLORATION_RADIUS; dy <= EXPLORATION_RADIUS; dy++)
                {
                    var tileX = playerX + dx;
                    var tileY = playerY + dy;

                    if (tileX >= 0 && tileX < Options.Instance.Map.MapWidth &&
                        tileY >= 0 && tileY < Options.Instance.Map.MapHeight)
                    {
                        var key = $"{tileX}_{tileY}";

                        if (!exploredMap.ContainsKey(key))
                        {
                            exploredMap[key] = new InternalTileData
                            {
                                TileColor = GetDominantTileColor(currentMap, tileX, tileY),
                                ExploredTimestamp = timestamp
                            };
                        }
                    }
                }
            }
        }

        private Color GetDominantTileColor(MapInstance map, int x, int y)
        {
            try
            {
                var groundLayerName = Options.Instance.Map.Layers.All.FirstOrDefault();
                if (groundLayerName != null && map.Layers.TryGetValue(groundLayerName, out var groundLayer))
                {
                    if (x >= 0 && x < groundLayer.GetLength(0) && y >= 0 && y < groundLayer.GetLength(1))
                    {
                        var tile = groundLayer[x, y];
                        if (tile.TilesetId != Guid.Empty)
                        {
                            return AnalyzeTilePixels(tile.TilesetId, tile.X, tile.Y);
                        }
                    }
                }
            }
            catch { }

            return new Color(120, 120, 120);
        }

        private Color AnalyzeTilePixels(Guid tilesetId, int tileX, int tileY)
        {
            var cacheKey = $"{tilesetId}_{tileX}_{tileY}";
            if (mTileColorCache.TryGetValue(cacheKey, out var cachedColor))
            {
                return cachedColor;
            }

            // Sistema otimizado: cores baseadas em tileset + posição
            unchecked
            {
                var hash = tilesetId.GetHashCode() ^ (tileX << 16) ^ tileY;

                // Cores mais realistas baseadas no tipo de terreno
                var baseHue = Math.Abs(hash % 360);
                var saturation = 0.6f + (hash % 40) / 100f; // 0.6-1.0
                var brightness = 0.4f + (hash % 40) / 100f; // 0.4-0.8

                // Ajustes específicos para tipos comuns
                if (baseHue < 60) // Vermelho/laranja - grama
                {
                    saturation = 0.7f;
                    brightness = 0.5f;
                }
                else if (baseHue < 120) // Verde - floresta
                {
                    saturation = 0.8f;
                    brightness = 0.4f;
                }
                else if (baseHue < 180) // Azul - água
                {
                    saturation = 0.9f;
                    brightness = 0.3f;
                    baseHue = 210; // Azul específico para água
                }
                else if (baseHue < 240) // Azul escuro - água profunda
                {
                    saturation = 0.8f;
                    brightness = 0.2f;
                    baseHue = 240;
                }
                else // Roxo/cinza - montanhas/pedras
                {
                    saturation = 0.3f;
                    brightness = 0.6f;
                }

                // Converter HSV para RGB
                var c = brightness * saturation;
                var x = c * (1 - Math.Abs((baseHue / 60) % 2 - 1));
                var m = brightness - c;

                float r, g, b;
                if (baseHue < 60)
                {
                    r = c; g = x; b = 0;
                }
                else if (baseHue < 120)
                {
                    r = x; g = c; b = 0;
                }
                else if (baseHue < 180)
                {
                    r = 0; g = c; b = x;
                }
                else if (baseHue < 240)
                {
                    r = 0; g = x; b = c;
                }
                else if (baseHue < 300)
                {
                    r = x; g = 0; b = c;
                }
                else
                {
                    r = c; g = 0; b = x;
                }

                var resultColor = new Color(
                    (byte)((r + m) * 255),
                    (byte)((g + m) * 255),
                    (byte)((b + m) * 255)
                );

                mTileColorCache[cacheKey] = resultColor;
                return resultColor;
            }
        }

        private void UpdateMinimapVisuals()
        {
            if (Globals.Me?.MapInstance == null) return;

            var windowSize = MINIMAP_WINDOW_SIZE;
            var halfWindowSize = windowSize / 2;
            var currentMapId = Globals.Me.MapId;

            if (!mMapPositions.ContainsKey(currentMapId))
            {
                mMapPositions[currentMapId] = (0, 0);
            }

            var currentMapPos = mMapPositions[currentMapId];
            var mapWidth = Options.Instance.Map.MapWidth;
            var mapHeight = Options.Instance.Map.MapHeight;

            var playerAbsX = currentMapPos.Item1 * mapWidth + Globals.Me.X;
            var playerAbsY = currentMapPos.Item2 * mapHeight + Globals.Me.Y;

            for (var x = 0; x < windowSize; x++)
            {
                for (var y = 0; y < windowSize; y++)
                {
                    var worldOffsetX = (x - halfWindowSize) * mZoomLevel;
                    var worldOffsetY = (y - halfWindowSize) * mZoomLevel;

                    var absoluteWorldX = (int)(playerAbsX + worldOffsetX);
                    var absoluteWorldY = (int)(playerAbsY + worldOffsetY);

                    var cache = mVisualCache[x, y];

                    if (mForceFullUpdate ||
                        cache.WorldX != absoluteWorldX ||
                        cache.WorldY != absoluteWorldY ||
                        cache.MapId != currentMapId)
                    {
                        var (mapId, localX, localY) = GetMapAndLocalCoords(absoluteWorldX, absoluteWorldY, mapWidth, mapHeight);
                        var color = GetTileColorForDisplay(localX, localY, mapId);

                        if (mapId == currentMapId && localX == Globals.Me.X && localY == Globals.Me.Y)
                        {
                            color = mPlayerColor;
                        }

                        mVisualCache[x, y] = new VisualTileCache
                        {
                            WorldX = absoluteWorldX,
                            WorldY = absoluteWorldY,
                            MapId = mapId,
                            Color = color
                        };

                        UpdateTileVisual(x, y, color);
                    }
                }
            }
        }

        private (Guid mapId, int x, int y) GetMapAndLocalCoords(int absoluteX, int absoluteY, int mapWidth, int mapHeight)
        {
            var mapGridX = Math.DivRem(absoluteX, mapWidth, out var localX);
            var mapGridY = Math.DivRem(absoluteY, mapHeight, out var localY);

            if (localX < 0)
            {
                mapGridX--;
                localX += mapWidth;
            }

            if (localY < 0)
            {
                mapGridY--;
                localY += mapHeight;
            }

            foreach (var kvp in mMapPositions)
            {
                if (kvp.Value.Item1 == mapGridX && kvp.Value.Item2 == mapGridY)
                {
                    return (kvp.Key, localX, localY);
                }
            }

            return (Guid.Empty, localX, localY);
        }

        private Color GetTileColorForDisplay(int localX, int localY, Guid mapId)
        {
            if (mapId == Guid.Empty)
            {
                return mUnexploredColor;
            }

            if (localX < 0 || localY < 0 ||
                localX >= Options.Instance.Map.MapWidth ||
                localY >= Options.Instance.Map.MapHeight)
            {
                return mOutboundColor;
            }

            if (mExploredData.TryGetValue(mapId, out var exploredMap))
            {
                var key = $"{localX}_{localY}";
                if (exploredMap.TryGetValue(key, out var tileData))
                {
                    return tileData.TileColor;
                }
            }

            return mUnexploredColor;
        }

        private void UpdateTileVisual(int x, int y, Color color)
        {
            var tile = mMinimapTile[x, y];
            if (tile == null) return;

            tile.Texture = MinimapIconsTexture;
            tile.SetTextureRect(0, 0, 1, 1);
            tile.RenderColor = color;
        }

        #region Persistence

        private void SaveExplorationData()
        {
            try
            {
                var directory = Path.GetDirectoryName(ExplorationDataPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var container = new ExplorationDataContainer();

                foreach (var mapKvp in mExploredData)
                {
                    foreach (var tileKvp in mapKvp.Value)
                    {
                        var coords = tileKvp.Key.Split('_');
                        if (coords.Length == 2 &&
                            int.TryParse(coords[0], out var x) &&
                            int.TryParse(coords[1], out var y))
                        {
                            container.ExploredTiles.Add(new ExploredTileData
                            {
                                MapId = mapKvp.Key.ToString(),
                                X = x,
                                Y = y,
                                R = tileKvp.Value.TileColor.R,
                                G = tileKvp.Value.TileColor.G,
                                B = tileKvp.Value.TileColor.B,
                                ExploredTimestamp = tileKvp.Value.ExploredTimestamp
                            });
                        }
                    }
                }

                foreach (var mapPos in mMapPositions)
                {
                    container.MapPositions[mapPos.Key.ToString()] = new MapPositionData
                    {
                        GridX = mapPos.Value.Item1,
                        GridY = mapPos.Value.Item2
                    };
                }

                var json = JsonConvert.SerializeObject(container, Formatting.None);
                File.WriteAllText(ExplorationDataPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MINIMAP] Save failed: {ex.Message}");
            }
        }

        private void LoadExplorationData()
        {
            try
            {
                if (!File.Exists(ExplorationDataPath))
                {
                    return;
                }

                var json = File.ReadAllText(ExplorationDataPath);
                var container = JsonConvert.DeserializeObject<ExplorationDataContainer>(json);

                if (container != null)
                {
                    if (container.ExploredTiles != null)
                    {
                        foreach (var tile in container.ExploredTiles)
                        {
                            if (Guid.TryParse(tile.MapId, out var mapId))
                            {
                                if (!mExploredData.ContainsKey(mapId))
                                {
                                    mExploredData[mapId] = new Dictionary<string, InternalTileData>();
                                }

                                var key = $"{tile.X}_{tile.Y}";
                                mExploredData[mapId][key] = new InternalTileData
                                {
                                    TileColor = new Color(tile.R, tile.G, tile.B),
                                    ExploredTimestamp = tile.ExploredTimestamp
                                };
                            }
                        }
                    }

                    if (container.MapPositions != null)
                    {
                        foreach (var mapPos in container.MapPositions)
                        {
                            if (Guid.TryParse(mapPos.Key, out var mapId))
                            {
                                mMapPositions[mapId] = (mapPos.Value.GridX, mapPos.Value.GridY);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MINIMAP] Load failed: {ex.Message}");
            }
        }

        private void ClearExplorationData()
        {
            mExploredData.Clear();
            mMapPositions.Clear();
            mTileColorCache.Clear();

            try
            {
                if (File.Exists(ExplorationDataPath))
                {
                    File.Delete(ExplorationDataPath);
                }
            }
            catch { }
        }

        #endregion

        public void Show()
        {
            mMinimapWindow.IsHidden = false;
            mForceFullUpdate = true;

            if (Globals.Me != null)
            {
                ExploreAreaAroundPlayer();
                BuildMapPositionCache();
                UpdateMapName();
            }
        }

        public bool IsVisible()
        {
            return !mMinimapWindow.IsHidden;
        }

        public void Hide()
        {
            mMinimapWindow.IsHidden = true;
            SaveExplorationData();
        }

        public void ZoomIn()
        {
            if (mZoomLevel > MIN_ZOOM_LEVEL)
            {
                mZoomLevel = Math.Max(MIN_ZOOM_LEVEL, mZoomLevel * 0.8f);
                OnZoomChanged();
            }
        }

        public void ZoomOut()
        {
            if (mZoomLevel < MAX_ZOOM_LEVEL)
            {
                mZoomLevel = Math.Min(MAX_ZOOM_LEVEL, mZoomLevel * 1.25f);
                OnZoomChanged();
            }
        }

        public void Dispose()
        {
            SaveExplorationData();

            mExploredData?.Clear();
            mMapPositions?.Clear();
            mTileColorCache?.Clear();

            if (mMinimapTile != null)
            {
                for (var x = 0; x < mMinimapTile.GetLength(0); x++)
                {
                    for (var y = 0; y < mMinimapTile.GetLength(1); y++)
                    {
                        mMinimapTile[x, y]?.Dispose();
                    }
                }
            }

            mZoomInButton?.Dispose();
            mZoomOutButton?.Dispose();
            mClearExplorationButton?.Dispose();
            mZoomLabel?.Dispose();
            mMapNameLabel?.Dispose();
            mMinimapWindow?.Dispose();
        }
    }
}