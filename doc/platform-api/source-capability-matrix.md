# Source Capability Matrix

## Purpose

`Consigliere` consumes multiple upstream BSV sources to maintain its own API SLA and cost profile.

These sources are supply inputs, not public API contracts.

The platform must route by capability, health, and economics rather than binding core logic to a single provider.

## Source Roles

A source may be assigned one or more internal roles:
- `authoritative`
- `fast`
- `cheap`
- `fallback`
- `verification`
- `degraded_only`

Roles are capability-specific, not source-global.

## Decision: Source Strategy Uses Preferred Mode Plus Capability Overrides

`v1` source strategy is:
- one preferred operating mode for the instance
- capability-specific overrides when needed

This is intentionally not:
- a hard single-source model
- nor a fully free-form routing system exposed to users from day one

Rationale:
- keeps the default mental model simpler for users who want an easy entry point
- still allows `Consigliere` to optimize SLA and cost per capability
- leaves room for stronger routing policy without forcing every user to understand the full source matrix immediately

## Decision: Supported Preferred Modes in v1

The supported preferred source modes for `v1` are:
- `node`
- `junglebus`
- `bitails`
- `hybrid`

These mode names are intentionally provider-visible rather than abstract.

Rationale:
- easier for users to understand at setup time
- closer to how operators already think about available supply sources

## Decision: Configuration Model Uses Three Layers

The `v1` source configuration model is split into three layers:

1. `providers`
2. `routing`
3. `capabilities`

### `providers`
Describes what sources exist and how to connect to them.

### `routing`
Describes preferred mode, primary/default routing posture, fallbacks, and verification role.

### `capabilities`
Describes per-capability overrides and special routing behavior.

Rationale:
- keeps provider connection details separate from routing intent
- avoids a flat config surface that becomes hard to reason about
- scales better as more capabilities and providers are introduced

## Decision: Canonical Config Examples Use `appsettings` JSON

`v1` should document source configuration examples in `appsettings`-style JSON rather than YAML or TOML.

Rationale:
- the repository is .NET-first
- runtime configuration will naturally map to `IOptions` / `appsettings.*.json`
- the same shape can later be projected into environment variables without changing the conceptual config model

## Decision: Top-Level Source Config Path

The top-level configuration path for the source model is:
- `Consigliere:Sources`

Rationale:
- short and specific
- aligned with the product name
- avoids introducing a broader config namespace before it is actually needed

## Decision: Minimal Routing Section Shape in v1

The baseline `routing` section includes:
- `preferredMode`
- `primarySource`
- `fallbackSources[]`
- `verificationSource`

Notes:
- capability-specific behavior such as broadcast fanout remains in `capabilities`
- routing holds the default posture, not every special-case rule

## Decision: Provider Config Uses a Stable Known Schema

`v1` should expose a stable known provider config surface with explicit `enabled` flags.

This means:
- known provider sections exist as part of the config model
- each provider can be enabled or disabled explicitly

Rationale:
- easier to document
- easier to template
- easier to validate
- easier for operators to understand what the platform supports

## Decision: Canonical Provider Sections in v1

The canonical provider sections in `v1` are:
- `node`
- `junglebus`
- `bitails`
- `whatsonchain`

Notes:
- `whatsonchain` remains part of the supported schema despite weaker practical stability
- its wide rate limits still make it useful as an available supply source

## Decision: Minimal Provider Section Shape in v1

Each provider section should have a common baseline shape:
- `enabled`
- `connectTimeout`
- `requestTimeout`
- `streamTimeout?`
- `idleTimeout?`
- `rateLimits?`
- `enabledCapabilities`
- `connection`

Interpretation:
- `connectTimeout` is required
- `requestTimeout` is required
- `streamTimeout` is optional
- `idleTimeout` is optional
- `rateLimits` is optional when not configurable or not known
- `enabledCapabilities` declares what the provider is allowed to serve in this instance
- `connection` contains provider-specific transport and credential details

This gives all providers a recognizable common frame while still allowing provider-specific connection fields.

## Decision: No Provider-Level `priority` Field in v1

`priority` is intentionally omitted from provider sections.

Rationale:
- it creates noise
- it overlaps with preferred mode, primary/fallback routing, and capability overrides
- routing order should live in routing policy, not inside provider connection objects

## Decision: Provider Capability Config Is Policy, Not Encyclopedia

Provider capability config uses `enabledCapabilities`, not a generic `supports` declaration.

Meaning:
- config describes what the instance is allowed to use this provider for
- config does not try to encode the full theoretical capability catalog of the provider

This keeps configuration focused on runtime policy rather than duplicating provider documentation.

## Decision: Rate Limits Use Two Levels

`v1` rate-limit config uses:

1. provider-level baseline limits
2. optional per-capability overrides

Rationale:
- some sources impose broad shared limits
- some capabilities have meaningfully different cost or quota behavior
- a two-level model is expressive without forcing every capability to define its own full budget

## Decision: Meaning of `hybrid`

In `v1`, `hybrid` means:
- one configured primary source
- multiple configured fallback sources

This does not mean:
- equal-weight multi-primary routing
- arbitrary dynamic source mesh

The purpose of `hybrid` is to preserve a clear main operating source while still giving the runtime enough fallback options to protect SLA and cost.

## Decision: Capability Overrides Fully Override Preferred Mode

The preferred mode defines the default routing behavior.

However:
- a capability override may fully replace the preferred-mode default for that capability

Interpretation:
- preferred mode = baseline routing intent
- capability override = explicit routing rule for one capability

This allows `Consigliere` to keep setup simple while still giving precise control where the economics or correctness profile of a capability is different from the default mode.

## Special Note: Broadcast May Be Multi-Target

Broadcast is allowed to behave as a multi-target capability rather than a strict single-destination request.

Example:
- a broadcast policy may push the same transaction to several configured destinations in parallel in order to maximize delivery success

This is treated as an explicit capability policy, not as a contradiction of the preferred-mode model.

## Decision: Minimal Routing-Sensitive Capabilities in v1

The minimal set of routing-sensitive capabilities in `v1` is:
- `broadcast`
- `realtime_ingest`
- `block_backfill`
- `raw_tx_fetch`
- `validation_fetch`

These are the first capabilities expected to benefit materially from explicit overrides because their SLA, correctness, and economics profiles are meaningfully different.

## Decision: `validation_fetch` Is a Broad Truth-Critical Capability

`validation_fetch` is intentionally defined broadly in `v1`.

It is not limited only to `(D)STAS` Back-to-Genesis lineage fetches.

Meaning:
- any fetch whose main purpose is to protect or restore truth-critical correctness belongs to this capability class

Key examples:
- `(D)STAS` Back-to-Genesis validation
- token lineage recovery
- authoritative re-checks after suspicious or disputed state transitions
- integrity-sensitive recovery after reorg or source disagreement

This keeps the routing model focused on why the fetch exists, not only on one protocol-specific use case.

## Decision: `verification_source` Is a First-Class Role in v1

`verification_source` is a first-class source role in `v1`.

It answers a different question from primary or fallback routing:
- not "who answers if the main source is unavailable?"
- but "who is trusted when truth must be confirmed?"

Meaning:
- verification is about correctness arbitration
- fallback is about availability

The same physical source may serve as:
- primary
- fallback
- verification

But these roles remain logically separate in the routing model.

## Decision: Minimal Verification Perimeter in v1

Verification is not required equally for every capability.

### Mandatory verification path
- `validation_fetch`

### Conditional verification path
- `block_backfill` when arbitration, disagreement, or suspicious integrity conditions appear
- `realtime_ingest` when gap, reorg, or source-conflict arbitration is required

### No mandatory verification path by default
- `raw_tx_fetch`
- `broadcast`

Notes:
- `raw_tx_fetch` may still participate in a broader validation workflow
- `broadcast` may still have a post-broadcast confirmation workflow
- the rule here is only that these capabilities do not require verification on every normal invocation

## Decision: Broadcast Uses Observe-and-Rebroadcast Semantics

`broadcast` in `v1` is not treated as a synchronous final-confirmation operation.

Model:
- the broadcast path sends the transaction to one or more configured destinations
- success is determined operationally by later observation from source sockets or ingest feeds
- long-unconfirmed transactions may be checked by a background recovery job
- if no configured source can see the transaction, the recovery path may rebroadcast it

Implications:
- broadcast submission and broadcast confirmation are separate concerns
- confirmation is established through observed network visibility, not by trusting a single immediate broadcast response
- rebroadcast is a resilience mechanism, not a separate public product promise

## Capability Set

Core internal capabilities:
- raw transaction fetch
- broadcast transaction
- block tip and chain status
- block crawl / historical ingest
- mempool observation
- transaction confirmation tracking
- address history lookup
- UTXO lookup
- spend lookup
- websocket or streaming event delivery

## Candidate Sources

### Node
Strengths:
- strongest local authority when available
- native RPC plus ZMQ
- good for verification and final state confirmation

Weaknesses:
- infrastructure cost
- not every business wants to run one
- operational burden

Likely internal roles:
- authoritative
- verification
- broadcast
- confirmation

### JungleBus
Strengths:
- useful streaming and historical ingestion model
- good fit for selective ingest and event flow
- can reduce need for self-hosted node in many scenarios

Weaknesses:
- provider-specific availability and subscription constraints
- current subscription model is not suitable for `Consigliere` default dynamic managed-scope onboarding
- external dependency for uptime and semantics

Likely internal roles:
- fast
- block crawl
- mempool or near-realtime ingest
- optional advanced stream source

### Bitails
Strengths:
- useful REST capability set
- practical lookup source in provider-only or hybrid modes
- websocket topics map well to managed-scope selective subscriptions
- provider also offers a node-adjacent subscription path through ZMQ, which is a good future fit for config-driven realtime ingest

Weaknesses:
- rate limits and provider constraints
- not ideal as the only truth source for every workload

Likely internal roles:
- baseline provider-first realtime source
- cheap or assist lookup
- fallback read path
- selective supplemental fetch

### WhatsOnChain
Strengths:
- broad ecosystem familiarity
- can be useful as an auxiliary assist source
- wide rate limits are operationally attractive

Weaknesses:
- not the preferred core product dependency for authoritative managed state
- semantics and cost profile must be treated as provider-specific
- operational stability is weaker than preferred primary-grade sources
- no documented realtime subscription surface is assumed in the product model

Likely internal roles:
- optional assist lookup
- degraded fallback

## Decision: Baseline Provider Policy

`Consigliere` now standardizes on:
- `bitails` as the baseline provider-first realtime source
- `junglebus` as an optional advanced source, not the default managed-mode source
- `whatsonchain` as REST-only assist and fallback, not a realtime source

Rationale:
- `bitails` fits the product best when operators need dynamic subscriptions around managed addresses, script hashes, and spend/lock event surfaces
- `junglebus` remains useful, but its subscription model is better treated as an advanced operator path rather than the default runtime-controlled source
- `whatsonchain` is still useful for fallback lookup and historical assist, but should not be modeled as if it participates in realtime ingest

## Decision: Realtime Filtered Event Surface Is A Product Requirement

Provider-first realtime support is not judged only by "has websocket".

For `Consigliere`, a provider is a first-class realtime source only if it can support managed-scope filtered event delivery such as:
- address-oriented events
- script-hash-oriented events
- spend vs lock distinction where available
- similar selective event filters that let the runtime subscribe narrowly rather than consume an unbounded global stream

This requirement explains why `bitails` is the preferred provider-first realtime source in the current product posture.

## Decision: JungleBus Is Explicitly Advanced/Manual

`junglebus` remains part of the supported source schema, but its intended posture is now:
- available for advanced operators
- not the default provider selected for managed realtime ingest
- not assumed to support runtime-created fine-grained dynamic subscriptions in the same way as the baseline Bitails model

When operators choose `junglebus`, they are intentionally accepting a broader and more manual streaming posture.

## Decision: WhatsOnChain Is REST-Only In Product Policy

Even though `whatsonchain` stays in the provider schema, the product policy treats it as:
- `raw_tx_fetch`
- assist lookup
- degraded fallback

It is not part of the baseline realtime ingest strategy.

## Decision: Future Bitails Transport Model Must Leave Room For ZMQ

The future Bitails connection contract should leave room for two realtime transport modes:
- `websocket`
- `zmq`

This is a transport choice inside the Bitails provider contract, not a separate provider brand.

The product intent is:
- `websocket` for the initial provider-first baseline path
- `zmq` as a future config-selectable transport when operators want a more node-adjacent Bitails integration

## Future Source Type: `network_connector`

`network_connector` is a planned future source type representing a direct-to-network connector rather than a third-party API provider.

Its intended scope is intentionally narrow.

Expected future roles:
- `realtime_ingest`
- tip and header observation
- `seen_in_mempool`
- `seen_by_source`

Not the intended initial role:
- full historical replacement for every provider
- complete truth replacement for all validation paths
- general-purpose explorer-style chain access

Rationale:
- gives `Consigliere` a path toward higher-fidelity direct network observation
- reduces dependence on third-party providers for realtime visibility
- avoids pretending that a lightweight connector is immediately a full replacement for node-grade or provider-grade capabilities

## Routing Principles

1. Route by capability, not by source brand.
2. Prefer the cheapest source that still satisfies the required consistency class.
3. Preserve a separate verification path for high-confidence state transitions.
4. Separate hot realtime paths from backfill and historical read paths.
5. Protect upstream quotas with capability-aware budgeting and backpressure.

## Initial Role Matrix

| Capability | Preferred role | Fallback role | Notes |
|---|---|---|---|
| raw tx fetch | fast or cheap provider | node or secondary provider | depends on latency/cost budget |
| broadcast | node when present | provider fallback if supported | higher confidence path preferred |
| block crawl | JungleBus or node | secondary provider | must support backfill well |
| mempool observation | JungleBus or node/ZMQ | degraded provider mode | realtime-critical |
| confirmation tracking | node or verification source | provider read fallback | should not rely on weakest source only |
| address history | indexed internal state first | assist provider for untracked reads | tracked scope stays internal |
| UTXO lookup | indexed internal state first | provider lookup for assist mode | authoritative for tracked scope must come from index |
| token state | internal index only | no provider passthrough promise | normalized domain logic |
| websocket events | internal only | none | public realtime is a Consigliere contract |

## Economics Model

Each capability should eventually have policy inputs for:
- cost per request or batch
- latency target
- rate-limit budget
- failure rate
- freshness requirement
- verification requirement

This allows operating modes such as:
- `no_node`
- `hybrid`
- `provider_only_high_availability`
- `verification_heavy`

## Product Constraint

No public endpoint should expose raw provider identity as part of its contract.

Provider choice is an internal routing concern unless explicitly surfaced in ops diagnostics.

## Decision: Provider Diagnostics Are First-Class Ops Data

While provider identity must not leak into ordinary business-facing state APIs, provider diagnostics are first-class in the ops surface.

This allows:
- health visibility
- fallback visibility
- degraded-mode investigation
- source-level troubleshooting

## Decision: Provider Diagnostics Are Provider-First

The primary ops status model is per-provider rather than per-capability.

Each provider status object may then expose nested capability-specific status for the routing-sensitive capabilities that matter operationally.

This keeps the operator's view aligned to concrete upstream sources while preserving the ability to diagnose capability-specific failures inside each source.

The minimum `provider status` object in `v1` is:
- `provider`
- `enabled`
- `configured`
- `roles[]`
- `healthy`
- `degraded`
- `lastSuccessAt?`
- `lastErrorAt?`
- `lastErrorCode?`
- `rateLimitState?`
- `capabilities`

Each nested capability status uses the following minimum shape in `v1`:
- `enabled`
- `healthy`
- `degraded`
- `lastSuccessAt?`
- `lastErrorAt?`
- `lastErrorCode?`
- `rateLimitState?`
- `active`

The minimum `rateLimitState` shape in `v1` is:
- `limited`
- `remaining?`
- `resetAt?`
- `scope?`
- `sourceHint?`
