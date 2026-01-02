using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Items;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// An ImagePanel that supports animated items with grid-based frame layout
/// </summary>
public partial class AnimatedItemPanel : ImagePanel
{
    private ItemDescriptor? _itemDescriptor;
    private FloatRect? _customSourceRect;

    public AnimatedItemPanel(Base parent, string? name = null) : base(parent, name)
    {
    }

    /// <summary>
    /// Sets the item to be displayed with animation support.
    /// Supports both horizontal and grid-based frame layouts.
    /// </summary>
    public void SetItem(ItemDescriptor? descriptor)
    {
        _itemDescriptor = descriptor;
        
        if (descriptor != null && !string.IsNullOrEmpty(descriptor.Icon))
        {
            // Load the texture
            var texture = GameContentManager.Current?.GetTexture(Framework.Content.TextureType.Item, descriptor.Icon);
            if (texture != null)
            {
                Texture = texture;
                RenderColor = descriptor.Color;
                
                // Calculate frame count based on texture dimensions (grid layout)
                // Frames per row = width / 32
                // Frames per column = height / 32
                // Total frames = framesPerRow * framesPerColumn
                var framesPerRow = Math.Max(1, texture.Width / 32);
                var framesPerColumn = Math.Max(1, texture.Height / 32);
                var frameCount = framesPerRow * framesPerColumn;
                
                descriptor.AnimationFrameCount = frameCount;
            }
        }
        else
        {
            Texture = null;
        }
    }

    /// <summary>
    /// Gets the item descriptor for this panel
    /// </summary>
    public ItemDescriptor? ItemDescriptor => _itemDescriptor;

    /// <summary>
    /// Overrides the source rectangle for custom rendering
    /// </summary>
    public void SetCustomSourceRect(FloatRect? sourceRect)
    {
        _customSourceRect = sourceRect;
    }

    protected override void Render(Framework.Gwen.Skin.Base skin)
    {
        // If we have an item descriptor and texture, use the animated rendering
        if (_itemDescriptor != null && Texture != null)
        {
            var sourceRect = ItemAnimationManager.GetItemSourceRect(_itemDescriptor, Texture);
            if (sourceRect.HasValue)
            {
                // Temporarily override the texture rect for animation
                SetTextureRect(
                    (int)sourceRect.Value.X,
                    (int)sourceRect.Value.Y,
                    (int)sourceRect.Value.Width,
                    (int)sourceRect.Value.Height
                );

                base.Render(skin);
                return;
            }
        }

        // Fall back to default rendering
        base.Render(skin);
    }
}
