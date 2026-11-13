using System;
using System.IO;
using System.Xml.Serialization;
using MultigridProjector.Config;

namespace VanillaServerPlugin;

// Server configuration for the vanilla Dedicated Server. Same options as the Magnetar build
// (ServerPlugin/PluginConfig.cs), but stored as a plain XML file next to the server's user data,
// because the vanilla server has no PluginSdk / Quasar UI. Edit the file while the server is
// stopped; it is read once at startup.
[XmlRoot("MultigridProjectorConfig")]
public class PluginConfig : IPluginConfig
{
    // Let the plugin manage how the not-yet-built (preview) blocks of a multigrid projection look.
    // Turn it OFF only for compatibility with mods that set projector / preview-block transparency.
    public bool PreviewBlockVisuals { get; set; } = true;

    // Limit on the PCU of the projections a player may load, in percent of the player's PCU limit.
    // 0 (the default) turns the limit off.
    public int ProjectionPcuLimitPercent { get; set; }

    // Seconds from a player joining to the first presence packet, which tells their Multigrid
    // Projector client plugin that the server has the plugin too (full feature set), and seconds
    // between the repeats of that packet to every connected player.
    public int PresenceDelaySeconds { get; set; } = 10;
    public int PresenceIntervalSeconds { get; set; } = 300;

    // Let projected blocks be built beside another subgrid of the same blueprint mounted at an
    // angle, as long as that subgrid does not reach into the inner half of the block.
    public bool BuildBesideAngledSubgrids { get; set; } = true;

    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PluginConfig));

    public static PluginConfig Load(string path)
    {
        if (!File.Exists(path))
            return new PluginConfig();

        using (var stream = File.OpenRead(path))
            return (PluginConfig)Serializer.Deserialize(stream) ?? new PluginConfig();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        using (var stream = File.Create(tmp))
            Serializer.Serialize(stream, this);

        if (File.Exists(path))
            File.Delete(path);
        File.Move(tmp, path);
    }

    public void Normalize()
    {
        ProjectionPcuLimitPercent = Math.Max(0, Math.Min(100000, ProjectionPcuLimitPercent));
        PresenceDelaySeconds = Math.Max(0, Math.Min(600, PresenceDelaySeconds));
        PresenceIntervalSeconds = Math.Max(1, Math.Min(3600, PresenceIntervalSeconds));
    }
}
