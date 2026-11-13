using System;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MultigridProjector.Logic;
using MultigridProjector.Utilities;
using MultigridProjectorDedicated;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using VanillaServerPlugin;
using VRage.FileSystem;
using VRage.Utils;

namespace TorchServerPlugin;

// Multigrid Projector server plugin for Torch (Torch.Server.exe, .NET Framework 4.8).
// Same patches and projector logic as the vanilla Dedicated Server build (VanillaServerPlugin);
// only the entry point differs:
// - Torch creates the plugin and calls Init before the game is fully initialized. The patches are
//   applied as soon as the game logger and file system are ready, at the latest when the session
//   starts loading (before any grid with a projector is loaded).
// - The "Late" patch category is applied once the session has loaded.
// - Torch plugins are not in MyPlugins, so the game does not register the plugin's session component
//   (ServerPlugin/PluginSession). The plugin creates the MultigridProjectorSession itself when the
//   session has loaded and updates it from Torch's plugin update. Without it the server never tells
//   clients that it has the plugin and they fall back to compatibility mode.
// - Configuration: MultigridProjector.cfg in the Torch instance folder (same file as the vanilla build).
// Install as a plugin folder: Plugins\MultigridProjector\ with MultigridProjectorTorch.dll,
// 0Harmony.dll and manifest.xml.
// ReSharper disable once UnusedType.Global
public class Plugin : TorchPluginBase
{
    public const string Name = "MultigridProjector";

    private const string HarmonyId = "com.spaceengineers.multigridprojector";
    private static Harmony Harmony => new Harmony(HarmonyId);

    private static readonly string ConfigFileName = "MultigridProjector.cfg";
    private static PluginConfig config;

    private static bool started;
    private static bool failed;
    private static bool lateApplied;
    private static MultigridProjectorSession mgpSession;

    public static Plugin Instance { get; private set; }

    public override void Init(ITorchBase torch)
    {
        base.Init(torch);
        Instance = this;
        Startup("Init");

        var sessions = torch.Managers.GetManager<ITorchSessionManager>();
        if (sessions != null)
            sessions.SessionStateChanged += SessionStateChanged;
        else
            Console.WriteLine("Multigrid Projector: no Torch session manager, patches only from Init");
    }

    private void SessionStateChanged(ITorchSession session, TorchSessionState state)
    {
        switch (state)
        {
            case TorchSessionState.Loading:
                Startup("session loading");
                break;
            case TorchSessionState.Loaded:
                Startup("session loaded");
                ApplyLate();
                StartSession();
                break;
            case TorchSessionState.Unloading:
                StopSession();
                break;
        }
    }

    private static void ApplyLate()
    {
        if (failed || !started || lateApplied)
            return;
        lateApplied = true;
        try
        {
            PatchHelpers.PatchCategory(Harmony, PatchHelpers.LateCategory);
            PluginLog.Info("Loaded (Torch build)");
        }
        catch (Exception e)
        {
            failed = true;
            PluginLog.Error(e, "Failed to apply late patches");
        }
    }

    private static void StartSession()
    {
        if (failed || !started || mgpSession != null)
            return;
        try
        {
            mgpSession = new MultigridProjectorSession();
            PluginLog.Info("Session started (clients are told that the server has the plugin)");
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Failed to start the session");
        }
    }

    private static void StopSession()
    {
        if (mgpSession == null)
            return;
        try
        {
            mgpSession.Dispose();
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Failed to stop the session");
        }
        mgpSession = null;
    }

    public override void Update()
    {
        try
        {
            mgpSession?.Update();
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Session update failed");
        }
    }

    // NoInlining: EnsureOriginal.VerifyAll() resolves the plugin assembly from this method's
    // stack frame, so it must remain a distinct frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Startup(string from)
    {
        if (started)
            return;

        if (MyLog.Default == null || string.IsNullOrEmpty(MyFileSystem.UserDataPath))
        {
            Console.WriteLine($"Multigrid Projector: game not initialized yet ({from}), retrying later");
            return;
        }

        started = true;

        try
        {
            PluginLog.Logger = new PluginLogger();
            PluginLog.Info($"Loading (Torch build, from {from})");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = LoadConfig(configPath);
            MultigridProjector.Config.PluginConfig.Current = config;
            PluginLog.Info($"Configuration: PreviewBlockVisuals={config.PreviewBlockVisuals}, ProjectionPcuLimitPercent={config.ProjectionPcuLimitPercent}, PresenceDelaySeconds={config.PresenceDelaySeconds}, PresenceIntervalSeconds={config.PresenceIntervalSeconds}, BuildBesideAngledSubgrids={config.BuildBesideAngledSubgrids} ({configPath})");

            if (Environment.GetEnvironmentVariable("SE_PLUGIN_DISABLE_METHOD_VERIFICATION") == null)
            {
                try
                {
                    EnsureOriginal.VerifyAll();
                }
                catch (NotSupportedException e)
                {
                    failed = true;
                    PluginLog.Error(e, "Disabled the plugin due to potentially incompatible code changes in the game or plugin patch collisions:");
                    return;
                }
            }

            PatchHelpers.PatchUncategorized(Harmony);
            PluginLog.Info("Patches applied");
        }
        catch (Exception e)
        {
            failed = true;
            if (PluginLog.Logger != null)
                PluginLog.Error(e, "Startup failed");
            else
                Console.WriteLine($"Multigrid Projector: Startup failed: {e}");
        }
    }

    private static PluginConfig LoadConfig(string path)
    {
        PluginConfig cfg;
        try
        {
            cfg = PluginConfig.Load(path);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Failed to load configuration file, using defaults: {path}");
            cfg = new PluginConfig();
        }

        cfg.Normalize();

        try
        {
            cfg.Save(path);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Failed to save configuration file: {path}");
        }

        return cfg;
    }

    public override void Dispose()
    {
        StopSession();
        if (PluginLog.Logger != null)
        {
            PluginLog.Info("Unloaded");
            PluginLog.Logger = null;
        }

        Instance = null;
        base.Dispose();
    }
}
