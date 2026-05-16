# Source Provider Policy

## Purpose

This document captures the current product policy for upstream BSV sources.

It is intentionally narrower than the full source-capability matrix:
- it states the baseline operator-facing decision
- it explains which providers are default, advanced, or fallback
- it records the intended future Bitails transport shape

## Current Product Decision

`Consigliere` standardizes on:
- `bitails` as the baseline provider-first realtime source
- `junglebus` as an optional advanced source, not the default managed-mode source
- `whatsonchain` as REST-only assist and fallback, not a realtime source

This decision is product-driven, not merely adapter-driven.

The key requirement is:
- managed-scope selective realtime ingest must be practical to operate dynamically

The operator-facing admin shell should mirror this posture explicitly:
- `/providers` is the advanced provider settings and provider-docs surface after first-run setup
- `bitails` should appear as the recommended realtime default
- `junglebus / gorillapool` should appear as the default block-sync path
- `whatsonchain` should appear as the recommended REST default
- `junglebus / gorillapool` should appear as the recommended practical raw transaction fetch path
- `junglebus` and `zmq` should be positioned as advanced options, not the first-run path

Important authority rule:
- providers supply chain data
- `Consigliere` supplies authoritative local `(D)STAS` validation and rooted token truth
- no provider should be described as if it validates `(D)STAS` for the service

## Why Bitails Is The Baseline

`bitails` fits the product posture best because it aligns with selective managed scope.

The intended model is:
- subscribe narrowly around operator-managed addresses, script hashes, spends, and locks
- avoid pretending that a broad external firehose is the default operational shape
- keep the entry barrier low for self-hosted operators who do not want to run a full node on day one

Official Bitails websocket examples are shown without an API key.
`Consigliere` should therefore present Bitails websocket as the default first-run path with API key optional at start, while still treating a key as the paid or higher-limit upgrade path.

## Why JungleBus Is Advanced

`junglebus` remains useful, but it is not the baseline source for managed realtime ingest.

In the current product posture it should be treated as:
- a strong optional source
- useful for advanced operators
- acceptable when a broader or more manually managed stream is intentional

It should not be treated as the default runtime-controlled dynamic subscription provider.

It can still be the recommended practical `raw_tx_fetch` path when operators want a strong transaction-get provider without using `WhatsOnChain` as the primary raw transaction dependency.

## Why WhatsOnChain Stays REST-Only

`whatsonchain` remains useful for:
- degraded fallback lookup
- assist and fallback historical reads where acceptable
- easy REST onboarding

It is not part of the baseline realtime ingest strategy.

The product should not model `whatsonchain` as if it were a first-class websocket or selective realtime source.

In current product messaging, `whatsonchain` should be positioned as:
- the simple REST default
- an easy fallback and onboarding choice
- not the preferred raw transaction source when `JungleBus / GorillaPool` is available

## Current Routing Posture

The intended default posture is:
- `bitails` for provider-first realtime ingest
- `junglebus / gorillapool` for block sync
- `bitails` for provider-first historical address and token scans
- `junglebus / gorillapool` as the recommended practical raw transaction source
- `validation_fetch` as local-validation dependency acquisition rather than an external truth provider
- `whatsonchain` as REST-only fallback and onboarding default
- `junglebus` as optional advanced realtime source
- `node` as the strongest advanced verification and authority-assist path when present

Public validation posture:
- `(D)STAS` legality verdicts still come from `Consigliere`'s local lineage-aware projection
- `validation_fetch` only helps acquire missing dependency data needed for that verdict

Current historical-scan posture:
- `historical_address_scan` is still Bitails-backed in `v1`
- `historical_token_scan` is still Bitails-backed in `v1`
- current runtime does not pretend these scans have provider failover; if Bitails is unavailable, the scan job fails honestly
- rooted token truth remains local to `Consigliere`, even when history is fetched from external providers

## Future Bitails Transport Contract

Future config work should preserve one provider identity for Bitails while allowing transport selection inside it.

Planned transport modes:
- `websocket`
- `zmq`

Interpretation:
- `websocket` is the initial baseline provider-first path
- `zmq` is a future advanced operator option when Bitails-backed node-adjacent subscriptions are desired

This should remain:
- one provider
- multiple transport modes

It should not become:
- separate fake providers for Bitails WebSocket and Bitails ZMQ

## Non-Goals

This policy does not claim:
- that `Consigliere` should become a universal explorer
- that every source must support every capability
- that all providers should participate equally in realtime ingest
- that provider identity must leak into ordinary public business APIs
