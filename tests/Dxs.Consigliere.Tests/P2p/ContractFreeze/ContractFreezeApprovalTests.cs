using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.WebSockets;
using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.P2p.ContractFreeze;

/// <summary>
/// Wave 1 S0.10 approval test (replaces the weaker reflection-existence
/// check called out by audit A1 M1). Snapshots the exact frozen surface
/// — names, return types, parameter types, DTO constructor properties —
/// across the program-wide contract-freeze set and asserts byte-equal
/// against the checked-in manifest. Drift in either direction (renames,
/// new members, deletions, type changes, callbacks landing on the wrong
/// side of the IWalletHub / IWalletServer split) fails this test.
/// </summary>
public class ContractFreezeApprovalTests
{
    private static readonly string ManifestPath =
        RepoPathResolver.ResolveFromRepoRoot("tests", "Dxs.Consigliere.Tests", "P2p", "ContractFreeze", "manifest.json");

    /// <summary>Members on IWalletHub that the freeze cares about.</summary>
    private static readonly string[] FrozenWalletHubCallbacks =
    {
        nameof(IWalletHub.OnNewBlock),
        nameof(IWalletHub.OnReorg),
    };

    /// <summary>Server methods on IWalletServer that S0 introduces and freezes.</summary>
    private static readonly string[] FrozenWalletServerMethods =
    {
        nameof(IWalletServer.SubscribeToBlockTip),
        nameof(IWalletServer.SubscribeToReorg),
    };

    /// <summary>Members on PeerSession added/frozen by S0.</summary>
    private static readonly string[] FrozenPeerSessionMembers =
    {
        nameof(PeerSession.OnHeadersReceived),
        nameof(PeerSession.OnInvReceived),
        nameof(PeerSession.OnRejectReceived),
        nameof(PeerSession.Telemetry),
        nameof(PeerSession.SendGetHeadersAsync),
    };

    /// <summary>
    /// One-shot bootstrap/regen. Enable by setting environment variable
    /// CONTRACT_FREEZE_REGEN=1. Writes the current reflected surface to
    /// manifest.json. Use only when a contract-freeze amendment slice is
    /// open in docs/stream-tasks/bsv-headers-chain-wave/.
    /// </summary>
    [Fact]
    public void Regenerate_Manifest_From_Reflection()
    {
        if (Environment.GetEnvironmentVariable("CONTRACT_FREEZE_REGEN") != "1")
            return; // silently no-op unless explicitly opted in.
        var json = BuildManifest();
        File.WriteAllText(ManifestPath, json + "\n");
    }

    [Fact]
    public void Manifest_Matches_ReflectedSurface()
    {
        var actual = BuildManifest();
        var expected = File.ReadAllText(ManifestPath);

        // Normalise both to JsonNode for a clean diff message if mismatch.
        var actualJson = JsonNode.Parse(actual)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var expectedJson = JsonNode.Parse(expected)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        if (!string.Equals(actualJson, expectedJson, StringComparison.Ordinal))
        {
            throw new Xunit.Sdk.XunitException(
                $"Contract-freeze drift detected.\n\n--- expected ({ManifestPath}) ---\n{expectedJson}\n\n--- actual (reflected) ---\n{actualJson}\n\n" +
                "If the change is intentional, open a contract-freeze amendment slice in " +
                "docs/stream-tasks/bsv-headers-chain-wave/ and update manifest.json.");
        }
    }

    [Fact]
    public void IWalletServer_DoesNotContainClientCallbacks()
    {
        // Audit-A1-followup M1: hub/server split. Client callbacks must NOT
        // appear as server-callable methods.
        var serverMembers = typeof(IWalletServer).GetMembers().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(nameof(IWalletHub.OnNewBlock), serverMembers);
        Assert.DoesNotContain(nameof(IWalletHub.OnReorg), serverMembers);
    }

    [Fact]
    public void IWalletHub_DoesNotContainSubscriptionMethods()
    {
        // Audit-A1-followup M1: hub/server split. Subscription server methods
        // must NOT appear as client callbacks.
        var hubMembers = typeof(IWalletHub).GetMembers().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(nameof(IWalletServer.SubscribeToBlockTip), hubMembers);
        Assert.DoesNotContain(nameof(IWalletServer.SubscribeToReorg), hubMembers);
    }

    [Fact]
    public void WalletHub_DoesNotExposeClientCallbacksAsServerMethods()
    {
        // The concrete WalletHub : Hub<IWalletHub>, IWalletServer must not
        // define public OnNewBlock / OnReorg methods (would expose them as
        // callable hub methods).
        var hubType = typeof(WalletHub);
        var declared = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(nameof(IWalletHub.OnNewBlock), declared);
        Assert.DoesNotContain(nameof(IWalletHub.OnReorg), declared);
    }

    private static string BuildManifest()
    {
        var root = new JsonObject
        {
            ["IWalletHub"] = ReflectInterfaceMethods(typeof(IWalletHub), FrozenWalletHubCallbacks),
            ["IWalletServer"] = ReflectInterfaceMethods(typeof(IWalletServer), FrozenWalletServerMethods),
            ["PeerSession"] = ReflectTypeMembers(typeof(PeerSession), FrozenPeerSessionMembers),
            ["PeerTelemetry"] = ReflectRecord(typeof(PeerTelemetry)),
            ["IPeerTelemetrySink"] = ReflectInterfaceAllMethods(typeof(IPeerTelemetrySink)),
            ["BlockTipDto"] = ReflectRecord(typeof(BlockTipDto)),
            ["ReorgEventDto"] = ReflectRecord(typeof(ReorgEventDto)),
            ["BroadcastReceiptDto"] = ReflectRecord(typeof(BroadcastReceiptDto)),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject ReflectInterfaceMethods(Type ifaceType, string[] names)
    {
        var result = new JsonObject();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            var method = ifaceType.GetMethod(name)
                ?? throw new InvalidOperationException($"Missing frozen member {ifaceType.Name}.{name}");
            result[name] = MethodSignature(method);
        }
        return result;
    }

    private static JsonObject ReflectInterfaceAllMethods(Type ifaceType)
    {
        var result = new JsonObject();
        foreach (var method in ifaceType.GetMethods().OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            result[method.Name] = MethodSignature(method);
        }
        return result;
    }

    private static JsonObject ReflectTypeMembers(Type type, string[] names)
    {
        var result = new JsonObject();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            var members = type.GetMember(name, BindingFlags.Public | BindingFlags.Instance);
            if (members.Length == 0)
                throw new InvalidOperationException($"Missing frozen member {type.Name}.{name}");
            // Choose method overload deterministically; for properties/fields one member exists.
            var member = members[0];
            switch (member)
            {
                case MethodInfo mi:
                    result[name] = MethodSignature(mi);
                    break;
                case PropertyInfo pi:
                    result[name] = new JsonObject
                    {
                        ["kind"] = "property",
                        ["type"] = TypeName(pi.PropertyType),
                        ["canRead"] = pi.CanRead,
                        ["canWrite"] = pi.CanWrite,
                    };
                    break;
                case FieldInfo fi:
                    result[name] = new JsonObject
                    {
                        ["kind"] = "field",
                        ["type"] = TypeName(fi.FieldType),
                    };
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled member kind for {type.Name}.{name}: {member.MemberType}");
            }
        }
        return result;
    }

    private static JsonObject ReflectRecord(Type recordType)
    {
        var props = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == recordType) // skip record-synthesised inherited members like EqualityContract
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        var properties = new JsonObject();
        foreach (var p in props)
        {
            properties[p.Name] = TypeName(p.PropertyType);
        }
        // Primary-constructor parameter order is the canonical record contract.
        var primaryCtor = recordType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var ctorParams = new JsonArray();
        foreach (var p in primaryCtor.GetParameters())
        {
            ctorParams.Add(new JsonObject
            {
                ["name"] = p.Name,
                ["type"] = TypeName(p.ParameterType),
            });
        }
        return new JsonObject
        {
            ["properties"] = properties,
            ["primaryConstructor"] = ctorParams,
        };
    }

    private static JsonObject MethodSignature(MethodInfo method)
    {
        var parameters = new JsonArray();
        foreach (var p in method.GetParameters())
        {
            parameters.Add(new JsonObject
            {
                ["name"] = p.Name,
                ["type"] = TypeName(p.ParameterType),
            });
        }
        return new JsonObject
        {
            ["kind"] = "method",
            ["returnType"] = TypeName(method.ReturnType),
            ["parameters"] = parameters,
        };
    }

    private static string TypeName(Type t)
    {
        if (t.IsByRef) return TypeName(t.GetElementType()!) + "&";
        if (t.IsArray) return TypeName(t.GetElementType()!) + "[]";
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var name = def.FullName ?? def.Name;
            var tickIdx = name.IndexOf('`');
            if (tickIdx >= 0) name = name[..tickIdx];
            var args = string.Join(", ", t.GetGenericArguments().Select(TypeName));
            return $"{name}<{args}>";
        }
        return t.FullName ?? t.Name;
    }
}
