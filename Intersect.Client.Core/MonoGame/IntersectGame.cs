using Intersect.Client.Core;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Gwen.Renderer;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.MonoGame.File_Management;
using Intersect.Client.MonoGame.Graphics;
using Intersect.Client.MonoGame.Input;
using Intersect.Client.MonoGame.Network;
using Intersect.Configuration;
using Intersect.Updater;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Reflection;
using Intersect.Client.Framework.Database;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.ThirdParty;
using Intersect.Utilities;
using MainMenu = Intersect.Client.Interface.Menu.MainMenu;
using Intersect.Client.Interface.Shared;
using Intersect.Client.MonoGame.NativeInterop;
using Intersect.Client.MonoGame.NativeInterop.OpenGL;
using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Framework.SystemInformation;
using Microsoft.Extensions.Logging;
using Exception = System.Exception;

namespace Intersect.Client.MonoGame;

/// <summary>
///     This is the main type for your game.
/// </summary>
internal partial class IntersectGame : Game
{
    private bool mInitialized;

    private double mLastUpdateTime = 0;

    private readonly GameRenderer _gameRenderer;

    private GraphicsDeviceManager mGraphics;

    #region "Autoupdate Variables"

    private Updater.Updater mUpdater;

    private Texture2D updaterBackground;

    private SpriteFont updaterFont;

    private SpriteFont updaterFontSmall;

    private Texture2D updaterProgressBar;

    private SpriteBatch updateBatch;

    private bool updaterGraphicsReset;

    #endregion

    private IClientContext Context { get; }

    private Action PostStartupAction { get; }

    private IntersectGame(IClientContext context, Action postStartupAction)
    {
        Context = context;
        PostStartupAction = postStartupAction;

        try
        {
            Strings.Load();
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(exception, "Error occurred loading strings for client");
            throw;
        }

        mGraphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 800,
            PreferredBackBufferHeight = 480,
            PreferHalfPixelOffset = true,
            PreferMultiSampling = true,
        };

        mGraphics.PreparingDeviceSettings += (_, args) =>
        {
            args.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage =
                RenderTargetUsage.PreserveContents;
            args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 8;
        };

        Content.RootDirectory = string.Empty;
        IsMouseVisible = true;
        Globals.ContentManager = new MonoContentManager();
        Globals.Database = new JsonDatabase();

        // Load configuration.
        Globals.Database.LoadPreferences();

        Window.IsBorderless = Context.StartupOptions.BorderlessWindow;

        _gameRenderer = new MonoRenderer(mGraphics, Content, this)
        {
            OverrideResolution = Context.StartupOptions.ScreenResolution,
        };

        Globals.InputManager = new MonoInput(this);
        GameClipboard.Instance = new MonoClipboard();

        Core.Graphics.Renderer = _gameRenderer;

        Interface.Interface.GwenRenderer = new IntersectRenderer(null, Core.Graphics.Renderer);
        Interface.Interface.GwenInput = new IntersectInput();

        // Windows
        Window.Position = new Microsoft.Xna.Framework.Point(
            _gameRenderer.ScreenWidth - _gameRenderer.ActiveResolution.X,
            _gameRenderer.ScreenHeight - _gameRenderer.ActiveResolution.Y
        ) / new Microsoft.Xna.Framework.Point(2);
        Window.AllowAltF4 = false;

        // Store frequently used property values in local variables.
        string mouseCursor = ClientConfiguration.Instance.MouseCursor;
        string updateUrl = ClientConfiguration.Instance.UpdateUrl;

        // If we're going to be rendering a custom mouse cursor, hide the default one!
        if (!string.IsNullOrWhiteSpace(mouseCursor))
        {
            IsMouseVisible = false;
        }

        // Reuse Updater object instead of creating a new one each time.
        if (!string.IsNullOrWhiteSpace(updateUrl))
        {
            mUpdater = new Updater.Updater(
                updateUrl,
                Path.Combine("version.json"),
                true,
                5
            );
        }
    }

    /// <summary>
    ///     Allows the game to perform any initialization it needs to before starting to run.
    ///     This is where it can query for any required services and load any non-graphic
    ///     related content.  Calling base.Initialize will enumerate through any components
    ///     and initialize them as well.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        PlatformStatistics.GPUStatisticsProvider = GL.CreateGPUStatisticsProvider();

        if (mUpdater != null)
        {
            //Set the size of the updater screen before applying graphic changes.
            //We need to do this here instead of in the constructor for the size change to apply to Linux
            mGraphics.PreferredBackBufferWidth = 800;
            mGraphics.PreferredBackBufferHeight = 480;
        }

        if (Steam.SteamDeck)
        {
            Window.IsBorderless = true;

            var displayResolution = new Resolution(
                GraphicsDevice.DisplayMode.Width,
                GraphicsDevice.DisplayMode.Height
            );

            _gameRenderer.PreferredResolution = displayResolution;
            _gameRenderer.OverrideResolution = displayResolution;

            mGraphics.PreferredBackBufferWidth = displayResolution.X;
            mGraphics.PreferredBackBufferHeight = displayResolution.Y;
        }

        mGraphics.ApplyChanges();
    }

    private void IntersectInit()
    {
        (Core.Graphics.Renderer as MonoRenderer)?.Init(GraphicsDevice);

        // TODO: Remove old netcode
        Networking.Network.Socket = new MonoSocket(Context);
        Networking.Network.Socket.Connected += (_, connectionEventArgs) =>
            MainMenu.SetNetworkStatus(connectionEventArgs.NetworkStatus);
        Networking.Network.Socket.ConnectionFailed += (_, connectionEventArgs, _) =>
            MainMenu.SetNetworkStatus(connectionEventArgs.NetworkStatus);
        Networking.Network.Socket.Disconnected += (_, connectionEventArgs) =>
            MainMenu.SetNetworkStatus(connectionEventArgs.NetworkStatus, resetStatusCheck: true);

        Main.Start(Context);

        mInitialized = true;

        PostStartupAction();
    }

    private TimeSpan _elapsedSincePlatformStatisticsRefresh;

    /// <summary>
    ///     Allows the game to run logic such as updating the world,
    ///     checking for collisions, gathering input, and playing audio.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Update(GameTime gameTime)
    {
        _elapsedSincePlatformStatisticsRefresh += gameTime.ElapsedGameTime;
        if (_elapsedSincePlatformStatisticsRefresh.TotalSeconds > 1)
        {
            _elapsedSincePlatformStatisticsRefresh = default;
            PlatformStatistics.Refresh();
        }

        if (mUpdater != null)
        {
            if (mUpdater.CheckUpdaterContentLoaded())
            {
                LoadUpdaterContent();
            }

            if (mUpdater.Status == UpdateStatus.Done || mUpdater.Status == UpdateStatus.None)
            {
                if (updaterGraphicsReset == true)
                {
                    //Drew a frame, now let's initialize the engine
                    IntersectInit();
                    mUpdater = null;
                }
            }
            else if (mUpdater.Status == UpdateStatus.Restart)
            {
                //Auto relaunch on Windows
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        Process.Start(
                            Environment.GetCommandLineArgs()[0],
                            Environment.GetCommandLineArgs().Length > 1
                                ? string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
                                : null
                        );

                        Exit();
                        break;
                }
            }
        }

        if (mUpdater == null)
        {
            if (!mInitialized)
            {
                IntersectInit();
            }

            if (Globals.IsRunning)
            {
                if (mLastUpdateTime < gameTime.TotalGameTime.TotalMilliseconds)
                {
                    lock (Globals.GameLock)
                    {
                        Main.Update(gameTime.ElapsedGameTime);
                    }

                    ///mLastUpdateTime = gameTime.TotalGameTime.TotalMilliseconds + (1000/60f);
                }
            }
            else
            {
                Main.DestroyGame();
                Exit();
            }
        }

        base.Update(gameTime);
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        if (mUpdater != null)
        {
            LoadUpdaterContent();
        }
    }

    /// <summary>
    ///     This is called when the game should draw itself.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;

        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);

        if (mUpdater != null)
        {
            if (mUpdater.Status == UpdateStatus.Done || mUpdater.Status == UpdateStatus.None)
            {
                if (updaterGraphicsReset == false)
                {
                    (Core.Graphics.Renderer as MonoRenderer)?.Init(GraphicsDevice);
                    (Core.Graphics.Renderer as MonoRenderer)?.Init();
                    (Core.Graphics.Renderer as MonoRenderer)?.Begin();
                    (Core.Graphics.Renderer as MonoRenderer)?.End();
                    updaterGraphicsReset = true;
                }
            }
            else
            {
                DrawUpdater();
            }
        }
        else
        {
            if (Globals.IsRunning && mInitialized)
            {
                lock (Globals.GameLock)
                {
                    Core.Graphics.Render(gameTime.ElapsedGameTime, gameTime.TotalGameTime);
                }
            }
        }

        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        ApplicationContext.Context.Value?.Logger.LogInformation("System window closing (due to user interaction most likely).");

        if (Globals.Me != null && Globals.Me.CombatTimer > Timing.Global?.Milliseconds)
        {
            //Try to prevent SDL Window Close
            var exception = false;
            try
            {
                var platform = GetType()
                    .GetField("Platform", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(this);

                var field = platform.GetType()
                    .GetField("_isExiting", BindingFlags.NonPublic | BindingFlags.Instance);

                field.SetValue(platform, 0);
            }
            catch
            {
                //TODO: Should we log here? I really don't know if it's necessary.
                exception = true;
            }

            if (!exception)
            {
                //Show Message Getting Exit Confirmation From Player to Leave in Combat
                _ = new InputBox(
                    title: Strings.Combat.WarningTitle,
                    prompt: Strings.Combat.WarningCharacterSelect,
                    inputType: InputType.YesNo,
                    onSubmit: (s, e) =>
                    {
                        if (Globals.Me != null)
                        {
                            Globals.Me.CombatTimer = 0;
                        }

                        Globals.IsRunning = false;
                    }
                );

                //Restart the MonoGame RunLoop
                Run();
                return;
            }
        }

        try
        {
            mUpdater?.Stop();
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(
                exception,
                "Exception thrown while stopping the updater on game close"
            );
        }

        try
        {
            Interface.Interface.DestroyGwen();
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(
                exception,
                "Exception thrown while destroying GWEN on game close"
            );
        }

        try
        {
            Networking.Network.Close("quitting");
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(
                exception,
                "Exception thrown while closing the network on game close"
            );
        }

        base.OnExiting(sender, args);
    }

    private void DrawUpdater()
    {
        //Draw updating text and show progress bar...

        if (updateBatch == null)
        {
            updateBatch = new SpriteBatch(GraphicsDevice);
        }

        updateBatch.Begin(SpriteSortMode.Immediate);

        //Default Window Size is 800x480
        if (updaterBackground != null)
        {
            updateBatch.Draw(
                updaterBackground, new Rectangle(0, 0, 800, 480),
                new Rectangle?(new Rectangle(0, 0, updaterBackground.Width, updaterBackground.Height)),
                Microsoft.Xna.Framework.Color.White
            );
        }

        var status = string.Empty;
        var progressPercent = 0f;
        var progress = string.Empty;
        var filesRemaining = string.Empty;
        var sizeRemaining = string.Empty;

        switch (mUpdater.Status)
        {
            case UpdateStatus.Checking:
                status = Strings.Update.Checking;
                break;

            case UpdateStatus.Updating:
                status = Strings.Update.Updating;
                progressPercent = mUpdater.Progress / 100f;
                progress = Strings.Update.PercentComplete.ToString((int)mUpdater.Progress);
                filesRemaining = Strings.Update.FilesRemaining.ToString(mUpdater.FilesRemaining);
                sizeRemaining = Strings.Update.RemainingSize.ToString(mUpdater.GetHumanReadableFileSize(mUpdater.SizeRemaining));
                break;

            case UpdateStatus.Restart:
                status = Strings.Update.Restart.ToString(Strings.Main.GameName);
                progressPercent = 100;
                progress = Strings.Update.PercentComplete.ToString(100);
                break;

            case UpdateStatus.Done:
                status = Strings.Update.Done;
                progressPercent = 100;
                progress = Strings.Update.PercentComplete.ToString(100);
                break;

            case UpdateStatus.Error:
                status = Strings.Update.Error;
                progress = mUpdater.Exception?.Message ?? "";
                progressPercent = 100;
                break;

            case UpdateStatus.None:
                //Nothing here!
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        if (updaterFont != null)
        {
            var size = updaterFont.MeasureString(status);
            updateBatch.DrawString(
                updaterFont, status, new Vector2(800 / 2 - size.X / 2, 360), Microsoft.Xna.Framework.Color.White
            );
        }

        //Bar will exist at 400 to 432
        if (updaterProgressBar != null)
        {
            updateBatch.Draw(
                updaterProgressBar, new Rectangle(100, 400, (int)(600 * progressPercent), 32),
                new Rectangle?(
                    new Rectangle(
                        0, 0, (int)(updaterProgressBar.Width * progressPercent), updaterProgressBar.Height
                    )
                ), Microsoft.Xna.Framework.Color.White
            );
        }

        //Bar will be 600 pixels wide
        if (updaterFontSmall != null)
        {
            //Draw % in center of bar
            var size = updaterFontSmall.MeasureString(progress);
            updateBatch.DrawString(
                updaterFontSmall, progress, new Vector2(800 / 2 - size.X / 2, 405),
                Microsoft.Xna.Framework.Color.White
            );

            //Draw files remaining on bottom left
            updateBatch.DrawString(
                updaterFontSmall, filesRemaining, new Vector2(100, 440), Microsoft.Xna.Framework.Color.White
            );

            //Draw total remaining on bottom right
            size = updaterFontSmall.MeasureString(sizeRemaining);
            updateBatch.DrawString(
                updaterFontSmall, sizeRemaining, new Vector2(700 - size.X, 440), Microsoft.Xna.Framework.Color.White
            );
        }

        updateBatch.End();
    }

    private void LoadUpdaterContent()
    {
        if (File.Exists(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "background.png")))
        {
            updaterBackground = Texture2D.FromFile(
                GraphicsDevice, Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "background.png")
            );
        }

        if (File.Exists(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "progressbar.png")))
        {
            updaterProgressBar = Texture2D.FromFile(
                GraphicsDevice, Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "progressbar.png")
            );
        }

        if (File.Exists(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "font.xnb")))
        {
            updaterFont = Content.Load<SpriteFont>(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "font"));
        }

        if (File.Exists(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "fontsmall.xnb")))
        {
            updaterFontSmall = Content.Load<SpriteFont>(Path.Combine(ClientConfiguration.ResourcesDirectory, "updater", "fontsmall"));
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        catch (NullReferenceException)
        {
            throw;
        }

        if (!disposing)
        {
            return;
        }

        if (!Context.IsDisposed)
        {
            Context.Dispose();
        }
    }

    /// <summary>
    /// Implements <see cref="IPlatformRunner"/> for MonoGame.
    /// </summary>
    internal partial class MonoGameRunner : IPlatformRunner
    {
        /// <inheritdoc />
        public void Start(IClientContext context, Action postStartupAction)
        {
            try
            {
                DoSdlInit(12832);
            }
            catch (Exception exception)
            {
                ApplicationContext.Context.Value?.Logger.LogWarning(
                    exception,
                    "Error occurred while trying to initialize SDL"
                );
            }

            using var game = new IntersectGame(context, postStartupAction);
            try
            {
                game.Run();
            }
            catch (Exception exception)
            {
                context.Logger.LogCritical(exception, "Game is crashing due to an exception");
                throw;
            }
        }

        private delegate void SdlInit(int flags);

        private static void DoSdlInit(int flags)
        {
            if (PlatformHelper.CurrentPlatform != Platform.Linux)
            {
                return;
            }

            var assemblyMonoGameFramework = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.FullName?.StartsWith("MonoGame.Framework") ?? false);
            var typeInternalSdl = assemblyMonoGameFramework?.GetType("Sdl");
            var methodSdlInit = typeInternalSdl?.GetMethod("Init");
            var delegateSdlInit = methodSdlInit?.CreateDelegate<SdlInit>();
            if (delegateSdlInit == null)
            {
                throw new InvalidOperationException("Missing Sdl.Init() from MonoGame");
            }
            delegateSdlInit(flags);

            if (!Sdl2.SDL_SetHint(Sdl2.SDL_VIDEO_X11_NET_WM_BYPASS_COMPOSITOR, false))
            {
                ApplicationContext.Context.Value?.Logger.LogWarning("Failed to set X11 Compositor hint");
            }
        }
    }
}
