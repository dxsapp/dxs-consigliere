using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;

using BsvBroadcastNode;

using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("BSV_");   // BSV_POOLSIZE=8, BSV_USERAGENT=..., etc.

builder.Services
    .Configure<BroadcastNodeOptions>(builder.Configuration.GetSection(BroadcastNodeOptions.Section))
    .AddSingleton<TxStore>()
    .AddSingleton<LogRing>(sp =>
    {
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BroadcastNodeOptions>>().Value;
        return new LogRing(opts.LogRingSize);
    })
    .AddSingleton<BroadcastService>()
    .AddSingleton<PeerNodeHost>()
    .AddHostedService(sp => sp.GetRequiredService<PeerNodeHost>());

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

var startedAt = DateTimeOffset.UtcNow;

// ── POST /broadcast ───────────────────────────────────────────────────────
app.MapPost("/broadcast", (
    [FromBody] BroadcastRequest req,
    BroadcastService svc,
    LogRing log) =>
{
    var (record, error) = svc.Submit(req.Hex ?? "");
    if (error is not null)
        return Results.BadRequest(new { error });

    log.Add("info", $"POST /broadcast → {record.TxId}");
    return Results.Ok(new
    {
        txid = record.TxId,
        state = record.State.ToString(),
        createdAt = record.CreatedAt,
    });
});

// ── GET /tx/{txid} ────────────────────────────────────────────────────────
app.MapGet("/tx/{txid}", (string txid, TxStore txStore) =>
{
    var r = txStore.Get(txid);
    if (r is null) return Results.NotFound(new { error = "Unknown txid" });

    return Results.Ok(new
    {
        txid = r.TxId,
        state = r.State.ToString(),
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
        peersServed = r.PeersServed,
        relayBackCount = r.RelayBackCount,
        error = r.Error,
    });
});

// ── GET /health ───────────────────────────────────────────────────────────
app.MapGet("/health", (PeerNodeHost host) =>
{
    var manager = host.Manager;
    var peers = manager?.ActiveSessions.Values
        .Select(s => new
        {
            endpoint = s.Remote.ToString(),
            state = s.State.ToString(),
            ua = s.PeerVersion?.UserAgent,
            version = s.PeerVersion?.ProtocolVersion,
        })
        .ToList();

    return Results.Ok(new
    {
        status = "ok",
        uptimeSeconds = (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
        poolSize = manager?.PoolSize ?? 0,
        targetPoolSize = manager?.TargetPoolSize ?? 0,
        subnet24Diversity = manager?.Subnet24Diversity ?? 0,
        peers,
    });
});

// ── GET /txs ──────────────────────────────────────────────────────────────
app.MapGet("/txs", (TxStore txStore, [FromQuery] int? limit) =>
{
    var all = txStore.All
        .OrderByDescending(r => r.CreatedAt)
        .Take(limit ?? 50)
        .Select(r => new { txid = r.TxId, state = r.State.ToString(), r.CreatedAt, r.Error });
    return Results.Ok(all);
});

// ── GET /log ──────────────────────────────────────────────────────────────
app.MapGet("/log", (LogRing ring, [FromQuery] int? n) =>
    Results.Ok(ring.Recent(n ?? 100).Select(e => new
    {
        at = e.At,
        level = e.Level,
        msg = e.Message,
        txid = e.TxId,
    })));

app.Run();

// ── Request DTO ───────────────────────────────────────────────────────────
public sealed record BroadcastRequest(string? Hex);
