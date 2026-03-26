using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Configs;

public class ConsigliereSourcesConfigValidation : IValidateOptions<ConsigliereSourcesConfig>
{
    private static readonly HashSet<string> KnownModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "node",
        "junglebus",
        "bitails",
        "hybrid"
    };

    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node",
        "junglebus",
        "bitails",
        "whatsonchain"
    };

    public ValidateOptionsResult Validate(string name, ConsigliereSourcesConfig options)
    {
        var errors = new List<string>();
        var providerStates = GetProviderStates(options);

        if (!string.IsNullOrWhiteSpace(options.Routing.PreferredMode)
            && !KnownModes.Contains(options.Routing.PreferredMode))
        {
            errors.Add($"Unknown preferred mode '{options.Routing.PreferredMode}'.");
        }

        if (!string.IsNullOrWhiteSpace(options.Routing.PreferredMode)
            && string.IsNullOrWhiteSpace(options.Routing.PrimarySource))
        {
            errors.Add("routing.primarySource must be set when routing.preferredMode is configured.");
        }

        ValidateProviderReference(options.Routing.PrimarySource, "routing.primarySource", errors);
        ValidateEnabledProviderReference(options.Routing.PrimarySource, "routing.primarySource", providerStates, errors);
        ValidateProviderReference(options.Routing.VerificationSource, "routing.verificationSource", errors);
        ValidateEnabledProviderReference(options.Routing.VerificationSource, "routing.verificationSource", providerStates, errors);

        foreach (var fallbackSource in options.Routing.FallbackSources)
        {
            ValidateProviderReference(fallbackSource, "routing.fallbackSources", errors);
            ValidateEnabledProviderReference(fallbackSource, "routing.fallbackSources", providerStates, errors);
        }

        ValidateCapabilityReference(options.Capabilities.Broadcast.Source, "capabilities.broadcast.source", errors);
        ValidateEnabledProviderReference(options.Capabilities.Broadcast.Source, "capabilities.broadcast.source", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.Broadcast.Sources, "capabilities.broadcast.sources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.Broadcast.Sources, "capabilities.broadcast.sources", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.Broadcast.FallbackSources, "capabilities.broadcast.fallbackSources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.Broadcast.FallbackSources, "capabilities.broadcast.fallbackSources", providerStates, errors);

        ValidateCapabilityReference(options.Capabilities.RealtimeIngest.Source, "capabilities.realtime_ingest.source", errors);
        ValidateEnabledProviderReference(options.Capabilities.RealtimeIngest.Source, "capabilities.realtime_ingest.source", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.RealtimeIngest.FallbackSources, "capabilities.realtime_ingest.fallbackSources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.RealtimeIngest.FallbackSources, "capabilities.realtime_ingest.fallbackSources", providerStates, errors);

        ValidateCapabilityReference(options.Capabilities.BlockBackfill.Source, "capabilities.block_backfill.source", errors);
        ValidateEnabledProviderReference(options.Capabilities.BlockBackfill.Source, "capabilities.block_backfill.source", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.BlockBackfill.FallbackSources, "capabilities.block_backfill.fallbackSources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.BlockBackfill.FallbackSources, "capabilities.block_backfill.fallbackSources", providerStates, errors);

        ValidateCapabilityReference(options.Capabilities.RawTxFetch.Source, "capabilities.raw_tx_fetch.source", errors);
        ValidateEnabledProviderReference(options.Capabilities.RawTxFetch.Source, "capabilities.raw_tx_fetch.source", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.RawTxFetch.FallbackSources, "capabilities.raw_tx_fetch.fallbackSources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.RawTxFetch.FallbackSources, "capabilities.raw_tx_fetch.fallbackSources", providerStates, errors);

        ValidateCapabilityReference(options.Capabilities.ValidationFetch.Source, "capabilities.validation_fetch.source", errors);
        ValidateEnabledProviderReference(options.Capabilities.ValidationFetch.Source, "capabilities.validation_fetch.source", providerStates, errors);
        ValidateCapabilitySources(options.Capabilities.ValidationFetch.FallbackSources, "capabilities.validation_fetch.fallbackSources", errors);
        ValidateEnabledProviderReferences(options.Capabilities.ValidationFetch.FallbackSources, "capabilities.validation_fetch.fallbackSources", providerStates, errors);

        if (!string.IsNullOrWhiteSpace(options.Capabilities.Broadcast.Mode)
            && !string.Equals(options.Capabilities.Broadcast.Mode, "single", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Capabilities.Broadcast.Mode, "multi", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Unsupported broadcast mode '{options.Capabilities.Broadcast.Mode}'.");
        }

        if (string.Equals(options.Capabilities.Broadcast.Mode, "multi", StringComparison.OrdinalIgnoreCase)
            && options.Capabilities.Broadcast.Sources.Length == 0)
        {
            errors.Add("capabilities.broadcast.sources must contain at least one provider when broadcast.mode is 'multi'.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateCapabilityReference(string provider, string path, ICollection<string> errors)
        => ValidateProviderReference(provider, path, errors);

    private static void ValidateCapabilitySources(IEnumerable<string> providers, string path, ICollection<string> errors)
    {
        foreach (var provider in providers)
            ValidateProviderReference(provider, path, errors);
    }

    private static void ValidateEnabledProviderReferences(
        IEnumerable<string> providers,
        string path,
        IReadOnlyDictionary<string, bool> providerStates,
        ICollection<string> errors
    )
    {
        foreach (var provider in providers)
            ValidateEnabledProviderReference(provider, path, providerStates, errors);
    }

    private static void ValidateProviderReference(string provider, string path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return;

        if (!KnownProviders.Contains(provider))
            errors.Add($"Unknown provider '{provider}' referenced by {path}.");
    }

    private static void ValidateEnabledProviderReference(
        string provider,
        string path,
        IReadOnlyDictionary<string, bool> providerStates,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(provider))
            return;

        if (providerStates.TryGetValue(provider, out var enabled) && !enabled)
            errors.Add($"Provider '{provider}' referenced by {path} is disabled.");
    }

    private static IReadOnlyDictionary<string, bool> GetProviderStates(ConsigliereSourcesConfig options)
        => new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["node"] = options.Providers.Node.Enabled,
            ["junglebus"] = options.Providers.JungleBus.Enabled,
            ["bitails"] = options.Providers.Bitails.Enabled,
            ["whatsonchain"] = options.Providers.Whatsonchain.Enabled
        };
}

public class ConsigliereStorageConfigValidation : IValidateOptions<ConsigliereStorageConfig>
{
    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "raven",
        "filesystem",
        "s3"
    };

    public ValidateOptionsResult Validate(string name, ConsigliereStorageConfig options)
    {
        var payloads = options.RawTransactionPayloads;

        if (!payloads.Enabled)
            return ValidateOptionsResult.Success;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(payloads.Provider))
        {
            errors.Add("Consigliere:Storage:RawTransactionPayloads:Provider must be set when the payload store is enabled.");
        }
        else if (!KnownProviders.Contains(payloads.Provider))
        {
            errors.Add($"Unknown payload store provider '{payloads.Provider}'.");
        }
        else
        {
            switch (payloads.Provider.ToLowerInvariant())
            {
                case "raven" when string.IsNullOrWhiteSpace(payloads.Location.Collection):
                    errors.Add("Raven payload storage requires location.collection.");
                    break;
                case "filesystem" when string.IsNullOrWhiteSpace(payloads.Location.RootPath):
                    errors.Add("FileSystem payload storage requires location.rootPath.");
                    break;
                case "s3" when string.IsNullOrWhiteSpace(payloads.Location.Bucket):
                    errors.Add("S3 payload storage requires location.bucket.");
                    break;
            }
        }

        if (payloads.Compression is { Enabled: true } compression)
        {
            if (string.IsNullOrWhiteSpace(compression.Algorithm))
            {
                errors.Add("Payload compression requires compression.algorithm when enabled.");
            }
            else if (!string.Equals(compression.Algorithm, "gzip", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(compression.Algorithm, "zstd", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Unsupported compression algorithm '{compression.Algorithm}'.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
