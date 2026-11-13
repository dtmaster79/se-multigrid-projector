using System;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MultigridProjector.Utilities;
using MultigridProjectorDedicated;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;

namespace VanillaServerPlugin;

// Multigrid Projector server plugin for the vanilla Space Engineers Dedicated Server
// (SpaceEngineersDedicated.exe, .NET Framework 4.8). Load it by adding the DLL path to the
// <Plugins> list in SpaceEngineers-Dedicated.cfg; 0Harmony.dll must be in the same folder.
//
// Differences to the Magnetar build (ServerPlugin/Plugin.cs):
// - Configuration is a plain XML file (MultigridProjector.cfg in the server's user data folder)
//   instead of Magnetar's PluginSdk.
// - No Preloader: the vanilla server creates the plugin instance (MyPlugins.Load, in the
//   MySandboxGame constructor) before the world is loaded, but calls IPlugin.Init only after the
//   world has loaded. The patches are therefore applied from the constructor, where the game
//   logger and the file system are already initialized. Init remains as a fallback.
// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin
{
    public const string Name = "MultigridProjector";

    private const string HarmonyId = "com.spaceengineers.multigridprojector";
    private static Harmony Harmony => new Harmony(HarmonyId);

    private static readonly string ConfigFileName = "MultigridProjector.cfg";
    private static PluginConfig config;
    private static string configPath;

    private static bool started;
    private static bool failed;

    public static Plugin Instance { get; private set; }

    public Plugin()
    {
        Instance = this;
        Startup("constructor");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Init(object gameInstance)
    {
        // Normally already done in the constructor (before world load). Idempotent.
        Startup("Init");
        if (failed)
            return;

        try
        {
            PatchHelpers.PatchCategory(Harmony, PatchHelpers.LateCategory);
        }
        catch (Exception e)
        {
            failed = true;
            PluginLog.Error(e, "Failed to apply late patches");
            return;
        }

        PluginLog.Info("Loaded (vanilla Dedicated Server build)");
    }

    // NoInlining: EnsureOriginal.VerifyAll() resolves the plugin assembly from this method's
    // stack frame, so it must remain a distinct frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Startup(string from)
    {
        if (started)
            return;

        // The vanilla server initializes logging and the file system before it creates the
        // plugin instances. If that ever changes, retry from Init instead of failing.
        if (MyLog.Default == null || string.IsNullOrEmpty(MyFileSystem.UserDataPath))
        {
            Console.WriteLine($"Multigrid Projector: game not initialized yet ({from}), retrying later");
            return;
        }

        started = true;

        try
        {
            PluginLog.Logger = new PluginLogger();
            PluginLog.Info($"Loading (from {from})");

            configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
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
            // Writes the file with all options on the first start, so admins can edit it.
            cfg.Save(path);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Failed to save configuration file: {path}");
        }

        return cfg;
    }

    public void Dispose()
    {
        if (PluginLog.Logger != null)
        {
            PluginLog.Info("Unloaded");
            PluginLog.Logger = null;
        }

        Instance = null;
    }

    public void Update()
    {
    }
}
