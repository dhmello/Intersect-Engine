using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Config;
using System;
using System.Linq;

namespace Intersect.Client.Interface.Game.Minimap
{
    public enum MiniMapTileType
    {
        Outbound = 0,
        Tile,
        Player,
        Entity,
        Block,
        Resource
    }

    public partial class MinimapWindow
    {
        private WindowControl mMinimapWindow;
        private ImagePanel[,] mMinimapTile;

        // Configuration

        // This will render a color based on the "ground" tile layer, set to false to use the icon on minimaptile.png
        private const bool RENDER_GROUND_BASED_MINIMAP = false; // Simplified to use icons only
        private const int MINIMAP_SIZE_X = 25; // MUST BE AN ODD NUMBER
        private const int MINIMAP_SIZE_Y = 25; // MUST BE AN ODD NUMBER

        private Color mOutboundColor = Color.White;
        private Color mTileColor = Color.White;
        private Color mPlayerColor = Color.White;
        private Color mEntityColor = Color.White;
        private Color mBlockColor = Color.Red;
        private Color mResourceColor = Color.White;

        // End Configuration

        private IGameTexture MinimapIconsTexture;
        private int mMinimapTextureSize;

        public MinimapWindow(Canvas gameCanvas)
        {
            mMinimapWindow = new WindowControl(gameCanvas, "Minimap", false, "MinimapWindow");
            mMinimapWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());

            MinimapIconsTexture = Globals.ContentManager.GetTexture(Framework.Content.TextureType.Misc, "minimaptile.png");
            mMinimapTextureSize = MinimapIconsTexture?.Height ?? 4;
            mMinimapTile = new ImagePanel[MINIMAP_SIZE_X, MINIMAP_SIZE_Y];

            for (var x = 0; x < MINIMAP_SIZE_X; x++)
            {
                for (var y = 0; y < MINIMAP_SIZE_Y; y++)
                {
                    mMinimapTile[x, y] = new ImagePanel(mMinimapWindow, string.Format("MinimapTilex{0}y{1}", x, y));
                    mMinimapTile[x, y].Texture = MinimapIconsTexture;
                    mMinimapTile[x, y].Width = mMinimapTextureSize;
                    mMinimapTile[x, y].Height = mMinimapTextureSize;
                    mMinimapTile[x, y].X = x * mMinimapTextureSize;
                    mMinimapTile[x, y].Y = y * mMinimapTextureSize;
                }
            }

            mMinimapWindow.IsClosable = false;
            mMinimapWindow.Width = MINIMAP_SIZE_X * mMinimapTextureSize + 4;
            mMinimapWindow.Height = MINIMAP_SIZE_Y * mMinimapTextureSize + 30;
        }

        public void Update()
        {
            if (mMinimapWindow.IsHidden)
            {
                return;
            }

            if (Globals.Me == null)
            {
                return;
            }

            var startX = Globals.Me.X - MINIMAP_SIZE_X / 2;
            var startY = Globals.Me.Y - MINIMAP_SIZE_Y / 2;
            var endX = Globals.Me.X + MINIMAP_SIZE_X / 2;
            var endY = Globals.Me.Y + MINIMAP_SIZE_Y / 2;

            var currentMap = Globals.Me.MapInstance;

            for (var x = startX; x <= endX; x++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    MiniMapTileType miniMapTileType = MiniMapTileType.Outbound;
                    int minimapTileX = x + startX * -1;
                    int minimapTileY = y + startY * -1;
                    var gridX = currentMap.GridX;
                    var gridY = currentMap.GridY;
                    int lx = x;
                    int ly = y;

                    if (lx < 0)
                    {
                        lx = Options.Instance.Map.MapWidth + lx;
                        gridX = currentMap.GridX - 1;
                    }
                    else if (lx >= Options.Instance.Map.MapWidth)
                    {
                        lx -= Options.Instance.Map.MapWidth;
                        gridX = currentMap.GridX + 1;
                    }

                    if (ly < 0)
                    {
                        ly = Options.Instance.Map.MapHeight + ly;
                        gridY = currentMap.GridY - 1;
                    }
                    else if (ly >= Options.Instance.Map.MapHeight)
                    {
                        ly -= Options.Instance.Map.MapHeight;
                        gridY = currentMap.GridY + 1;
                    }

                    if (gridX < 0 || gridY < 0 || gridX >= Globals.MapGridWidth || gridY >= Globals.MapGridHeight)
                    {
                        mMinimapTile[minimapTileX, minimapTileY].RenderColor = mOutboundColor;
                        miniMapTileType = MiniMapTileType.Outbound;
                    }
                    else
                    {
                        var map = Globals.MapGrid[gridX, gridY];

                        if (map == Guid.Empty)
                        {
                            mMinimapTile[minimapTileX, minimapTileY].RenderColor = mOutboundColor;
                            miniMapTileType = MiniMapTileType.Outbound;
                        }
                        else
                        {
                            mMinimapTile[minimapTileX, minimapTileY].RenderColor = mTileColor;
                            miniMapTileType = MiniMapTileType.Tile;

                            if (lx == Globals.Me.X && ly == Globals.Me.Y && map == currentMap.Id)
                            {
                                mMinimapTile[minimapTileX, minimapTileY].RenderColor = mPlayerColor;
                                miniMapTileType = MiniMapTileType.Player;
                            }
                            else
                            {
                                IEntity blockedBy = null;
                                var tileBlocked = Globals.Me.IsTileBlocked(
                                    new Point(lx, ly), 0, map, ref blockedBy);

                                if (tileBlocked == -2)
                                {
                                    mMinimapTile[minimapTileX, minimapTileY].RenderColor = mBlockColor;
                                    miniMapTileType = MiniMapTileType.Block;
                                }
                                else if (Globals.Entities.Count > 0 &&
                                         Globals.Entities.Any(e => e.Value.MapInstance.Id == map && e.Value.X == lx && e.Value.Y == ly))
                                {
                                    var entity = Globals.Entities.FirstOrDefault(e => e.Value.MapInstance.Id == map && e.Value.X == lx && e.Value.Y == ly);

                                    if (entity.Value.GetType() == typeof(Resource))
                                    {
                                        mMinimapTile[minimapTileX, minimapTileY].RenderColor = mResourceColor;
                                        miniMapTileType = MiniMapTileType.Resource;
                                    }
                                    else
                                    {
                                        mMinimapTile[minimapTileX, minimapTileY].RenderColor = mEntityColor;
                                        miniMapTileType = MiniMapTileType.Entity;
                                    }
                                }
                            }
                        }
                    }

                    mMinimapTile[minimapTileX, minimapTileY].Texture = MinimapIconsTexture;
                    mMinimapTile[minimapTileX, minimapTileY].SetTextureRect((int)miniMapTileType * mMinimapTextureSize, 0, mMinimapTextureSize, mMinimapTextureSize);
                    mMinimapTile[minimapTileX, minimapTileY].SetSize(mMinimapTextureSize, mMinimapTextureSize);
                }
            }
        }

        public void Show()
        {
            mMinimapWindow.IsHidden = false;
        }

        public bool IsVisible()
        {
            return !mMinimapWindow.IsHidden;
        }

        public void Hide()
        {
            mMinimapWindow.IsHidden = true;
        }
    }
}
