using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Items;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Interface.Game.Inventory;

public partial class MapItemIcon
{
    public ImagePanel Container;

    public MapItemInstance? MyItem;

    public Guid MapId;

    public int TileIndex;

    public ImagePanel Pnl;

    private MapItemWindow mMapItemWindow;

    // Descriptor atual para controle de animação
    private ItemDescriptor? _currentDescriptor;

    public MapItemIcon(MapItemWindow window)
    {
        mMapItemWindow = window;
    }

    public void Setup()
    {
        Pnl = new ImagePanel(Container, "MapItemIcon");
        Pnl.HoverEnter += pnl_HoverEnter;
        Pnl.HoverLeave += pnl_HoverLeave;
        Pnl.Clicked += pnl_Clicked;
    }

    void pnl_Clicked(Base sender, MouseButtonState arguments)
    {
        if (MyItem == null || TileIndex < 0 || TileIndex >= Options.Instance.Map.MapWidth * Options.Instance.Map.MapHeight)
        {
            return;
        }

        _ = Player.TryPickupItem(MapId, TileIndex, MyItem.Id);
    }

    void pnl_HoverLeave(Base sender, EventArgs arguments)
    {
        Interface.GameUi.ItemDescriptionWindow?.Hide();
    }

    void pnl_HoverEnter(Base? sender, EventArgs? arguments)
    {
        if (MyItem == null)
        {
            return;
        }

        if (InputHandler.MouseFocus != null)
        {
            return;
        }

        if (Globals.InputManager.IsMouseButtonDown(MouseButton.Left))
        {
            return;
        }

        Interface.GameUi.ItemDescriptionWindow?.Show(ItemDescriptor.Get(MyItem.ItemId), MyItem.Quantity, MyItem.ItemProperties);
    }

    public FloatRect RenderBounds()
    {
        var rect = new FloatRect()
        {
            X = Pnl.ToCanvas(new Point(0, 0)).X,
            Y = Pnl.ToCanvas(new Point(0, 0)).Y,
            Width = Pnl.Width,
            Height = Pnl.Height
        };

        return rect;
    }

    /// <summary>
    /// Renderiza o ícone do item com animação
    /// </summary>
    public void Render()
    {
        // Atualizar animação do item se necessário
        if (_currentDescriptor != null && Pnl.Texture != null)
        {
            var sourceRect = ItemAnimationManager.GetItemSourceRect(_currentDescriptor, Pnl.Texture);
            if (sourceRect.HasValue)
            {
                Pnl.SetTextureRect(
                    (int)sourceRect.Value.X,
                    (int)sourceRect.Value.Y,
                    (int)sourceRect.Value.Width,
                    (int)sourceRect.Value.Height
                );
            }
        }
    }

    public void Update()
    {
        if (MyItem == null)
        {
            _currentDescriptor = null;
            return;
        }

        var item = ItemDescriptor.Get(MyItem.ItemId);
        if (item != null)
        {
            // Armazenar o descriptor atual para usar na animação
            _currentDescriptor = item;

            var itemTex = Globals.ContentManager.GetTexture(Framework.Content.TextureType.Item, item.Icon);
            if (itemTex != null)
            {
                Pnl.RenderColor = item.Color;
                Pnl.Texture = itemTex;
            }
            else
            {
                if (Pnl.Texture != null)
                {
                    Pnl.Texture = null;
                }
            }
        }
        else
        {
            _currentDescriptor = null;
            if (Pnl.Texture != null)
            {
                Pnl.Texture = null;
            }
        }

        // Atualizar animação
        Render();
    }
}
