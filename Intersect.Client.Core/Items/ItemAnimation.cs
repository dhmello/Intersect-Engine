using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core;

namespace Intersect.Client.Items;

/// <summary>
/// Manages animation state for item icons
/// </summary>
public partial class ItemAnimation
{
    private readonly ItemDescriptor _descriptor;
    private int _currentFrame;
    private long _lastFrameTime;
    private readonly int _frameCount;
    private readonly int _frameDuration;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly int _framesPerRow;

    public ItemAnimation(ItemDescriptor descriptor, IGameTexture texture)
    {
        _descriptor = descriptor;
        _lastFrameTime = Timing.Global.MillisecondsUtc;
        _frameDuration = descriptor.AnimationFrameSpeed;

        if (texture != null)
        {
            // Each frame is always 32x32 pixels
            _frameWidth = 32;
            _frameHeight = 32;
            
            // Calculate how many frames fit horizontally and vertically
            _framesPerRow = Math.Max(1, texture.Width / 32);
            var framesPerColumn = Math.Max(1, texture.Height / 32);
            
            // Total frame count is frames per row * frames per column
            _frameCount = _framesPerRow * framesPerColumn;
            descriptor.AnimationFrameCount = _frameCount;
        }
        else
        {
            _frameWidth = 32;
            _frameHeight = 32;
            _frameCount = 1;
            _framesPerRow = 1;
        }

        _currentFrame = 0;
    }

    public int CurrentFrame
    {
        get
        {
            // Update animation frame if enough time has passed
            if (_frameCount > 1)
            {
                var currentTime = Timing.Global.MillisecondsUtc;
                if (currentTime - _lastFrameTime >= _frameDuration)
                {
                    _currentFrame = (_currentFrame + 1) % _frameCount;
                    _lastFrameTime = currentTime;
                }
            }

            return _currentFrame;
        }
    }

    public int FrameCount => _frameCount;

    public int FrameWidth => _frameWidth;

    public int FrameHeight => _frameHeight;

    /// <summary>
    /// Gets the source rectangle for the current animation frame.
    /// Frames are read left to right, top to bottom (like reading text).
    /// </summary>
    public Framework.GenericClasses.FloatRect GetSourceRect()
    {
        // Calculate which row and column the current frame is in
        var row = CurrentFrame / _framesPerRow;
        var column = CurrentFrame % _framesPerRow;

        return new Framework.GenericClasses.FloatRect(
            column * _frameWidth,  // X position
            row * _frameHeight,     // Y position
            _frameWidth,
            _frameHeight
        );
    }

    /// <summary>
    /// Resets the animation to the first frame
    /// </summary>
    public void Reset()
    {
        _currentFrame = 0;
        _lastFrameTime = Timing.Global.MillisecondsUtc;
    }
}
