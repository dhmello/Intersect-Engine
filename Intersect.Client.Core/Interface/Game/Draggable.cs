using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.DragDrop;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Items;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Interface.Game;

public partial class Draggable(Base parent, string name) : ImagePanel(parent, name)
{
    // Armazena o descriptor do item para animação durante drag
    private ItemDescriptor? _itemDescriptor;

    public bool IsDragging => DragAndDrop.CurrentPackage?.DrawControl == this;

    /// <summary>
    /// Define o ItemDescriptor para suportar animação durante drag
    /// </summary>
    public ItemDescriptor? ItemDescriptor
    {
        get => _itemDescriptor;
        set => _itemDescriptor = value;
    }

    // TODO: Fix drag and drop names
    public override bool DragAndDrop_Draggable()
    {
        return true;
    }

    public override bool DragAndDrop_CanAcceptPackage(Package package)
    {
        return true;
    }

    public override Package? DragAndDrop_GetPackage(int x, int y)
    {
        return new Package()
        {
            IsDraggable = true,
            DrawControl = this,
            Name = Name,
            HoldOffset = ToLocal(InputHandler.MousePosition.X, InputHandler.MousePosition.Y),
        };
    }

    public override void DragAndDrop_StartDragging(Package package, int x, int y)
    {
        IsVisibleInParent = false;
    }

    public override void DragAndDrop_EndDragging(bool success, int x, int y)
    {
        IsVisibleInParent = true;
    }

    protected override void Render(Framework.Gwen.Skin.Base skin)
    {
        // Aplicar animação se temos um ItemDescriptor e textura
        if (_itemDescriptor != null && Texture != null)
        {
            var sourceRect = ItemAnimationManager.GetItemSourceRect(_itemDescriptor, Texture);
            if (sourceRect.HasValue)
            {
                SetTextureRect(
                    (int)sourceRect.Value.X,
                    (int)sourceRect.Value.Y,
                    (int)sourceRect.Value.Width,
                    (int)sourceRect.Value.Height
                );
            }
        }

        base.Render(skin);
    }
}