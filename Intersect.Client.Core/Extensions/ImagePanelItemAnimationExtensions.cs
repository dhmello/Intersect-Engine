using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Items;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Extensions;

/// <summary>
/// Extension methods for ImagePanel to support animated item rendering
/// </summary>
public static class ImagePanelItemAnimationExtensions
{
    /// <summary>
    /// Sets up an ImagePanel to render an animated item
    /// </summary>
    public static void SetItemTexture(this ImagePanel imagePanel, ItemDescriptor descriptor, IGameTexture texture)
    {
        if (imagePanel == null || descriptor == null)
        {
            return;
        }

        // Store the descriptor for later animation updates
        imagePanel.Texture = texture;
        imagePanel.RenderColor = descriptor.Color;
        
        // If the item has multiple frames, set up the UVs for animation
        if (texture != null)
        {
            var frameCount = Math.Min(4, Math.Max(1, texture.Width / 32));
            descriptor.AnimationFrameCount = frameCount;
        }
    }

    /// <summary>
    /// Gets the source rectangle for rendering the current animation frame of an item
    /// </summary>
    public static FloatRect? GetItemAnimationSourceRect(this ImagePanel imagePanel, ItemDescriptor descriptor)
    {
        if (imagePanel?.Texture == null || descriptor == null)
        {
            return null;
        }

        return ItemAnimationManager.GetItemSourceRect(descriptor, imagePanel.Texture);
    }
}
