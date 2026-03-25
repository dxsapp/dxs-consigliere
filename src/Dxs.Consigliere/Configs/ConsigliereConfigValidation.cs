using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Configs;

public class ConsigliereSourcesConfigValidation : IValidateOptions<ConsigliereSourcesConfig>
{
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

        ValidateProviderReference(options.Routing.PrimarySource, "routing.primarySource", errors);
        ValidateProviderReference(options.Routing.VerificationSource, "routing.verificationSource", errors);

        foreach (var fallbackSource in options.Routing.FallbackSources)
            ValidateProviderReference(fallbackSource, "routing.fallbackSources", errors);

        ValidateCapabilityReference(options.Capabilities.Broadcast.Source, "capabilities.broadcast.source", errors);
        ValidateCapabilitySources(options.Capabilities.Broadcast.Sources, "capabilities.broadcast.sources", errors);
        ValidateCapabilitySources(options.Capabilities.Broadcast.FallbackSources, "capabilities.broadcast.fallbackSources", errors);

        ValidateCapabilityReference(options.Capabilities.RealtimeIngest.Source, "capabilities.realtime_ingest.source", errors);
        ValidateCapabilitySources(options.Capabilities.RealtimeIngest.FallbackSources, "capabilities.realtime_ingest.fallbackSources", errors);

        ValidateCapabilityReference(options.Capabilities.BlockBackfill.Source, "capabilities.block_backfill.source", errors);
        ValidateCapabilitySources(options.Capabilities.BlockBackfill.FallbackSources, "capabilities.block_backfill.fallbackSources", errors);

        ValidateCapabilityReference(options.Capabilities.RawTxFetch.Source, "capabilities.raw_tx_fetch.source", errors);
        ValidateCapabilitySources(options.Capabilities.RawTxFetch.FallbackSources, "capabilities.raw_tx_fetch.fallbackSources", errors);

        ValidateCapabilityReference(options.Capabilities.ValidationFetch.Source, "capabilities.validation_fetch.source", errors);
        ValidateCapabilitySources(options.Capabilities.ValidationFetch.FallbackSources, "capabilities.validation_fetch.fallbackSources", errors);

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

    private static void ValidateProviderReference(string provider, string path, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return;

        if (!KnownProviders.Contains(provider))
            errors.Add($"Unknown provider '{provider}' referenced by {path}.");
    }
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
