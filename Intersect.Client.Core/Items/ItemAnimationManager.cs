using System.Collections.Concurrent;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Items;

/// <summary>
/// Global manager for item animations
/// </summary>
public static class ItemAnimationManager
{
    private static readonly ConcurrentDictionary<Guid, ItemAnimation> _itemAnimations = new();

    /// <summary>
    /// Gets or creates an item animation for the given descriptor and texture
    /// </summary>
    public static ItemAnimation GetOrCreateAnimation(ItemDescriptor descriptor, IGameTexture texture)
    {
        if (descriptor == null)
        {
            return null;
        }

        return _itemAnimations.GetOrAdd(descriptor.Id, _ => new ItemAnimation(descriptor, texture));
    }

    /// <summary>
    /// Gets the source rectangle for the current frame of an item
    /// </summary>
    public static Framework.GenericClasses.FloatRect? GetItemSourceRect(ItemDescriptor descriptor, IGameTexture texture)
    {
        if (descriptor == null || texture == null)
        {
            return null;
        }

        var animation = GetOrCreateAnimation(descriptor, texture);
        return animation?.GetSourceRect();
    }

    /// <summary>
    /// Clears all cached animations (e.g., when changing maps or reloading content)
    /// </summary>
    public static void ClearAnimations()
    {
        _itemAnimations.Clear();
    }

    /// <summary>
    /// Removes animation for a specific item
    /// </summary>
    public static void RemoveAnimation(Guid itemId)
    {
        _itemAnimations.TryRemove(itemId, out _);
    }
}
