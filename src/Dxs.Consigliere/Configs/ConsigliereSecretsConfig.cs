namespace Dxs.Consigliere.Configs;

/// <summary>
/// wave-A3 S5 — bind shape for the on-disk secrets directory.
/// Default is <c>data/secrets</c> relative to the process
/// content root; the compose <c>prod</c> profile mounts a
/// Docker secret volume at <c>/var/lib/consigliere/secrets</c>
/// and points <c>Consigliere__Secrets__Dir</c> at it.
/// </summary>
public sealed class ConsigliereSecretsConfig
{
    public string Dir { get; init; } = "data/secrets";
}
