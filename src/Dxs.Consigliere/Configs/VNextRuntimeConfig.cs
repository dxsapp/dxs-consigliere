namespace Dxs.Consigliere.Configs;

public class VNextRuntimeConfig
{
    public string CutoverMode { get; set; } = VNextCutoverMode.Legacy;
}

public static class VNextCutoverMode
{
    public const string Legacy = "legacy";
    public const string MirrorWrite = "mirror_write";
    public const string ShadowRead = "shadow_read";
    public const string VNextDefault = "vnext_default";

    public static bool IsJournalFirst(string mode)
        => string.Equals(mode, ShadowRead, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, VNextDefault, StringComparison.OrdinalIgnoreCase);
}
