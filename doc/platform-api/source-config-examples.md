# Source Config Examples

This document contains `appsettings`-style JSON examples for the `Consigliere:Sources` configuration model.

Runtime-ready templates also live in:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.node.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.hybrid.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.provider-only.example.json`

These examples are intentionally explicit.

The repository is AI-first, so the examples prefer:
- stable key names
- obvious role assignment
- low ambiguity between connection policy and routing policy
- enough inline explanation to make future edits safer

## Structure Reminder

The top-level path is:
- `Consigliere:Sources`

The configuration model is split into:
- `providers`
- `routing`
- `capabilities`

## Example 1: `node` Preferred Mode

Use this when the operator runs a node and wants it to be the default source for the instance.

```json
{
  "Consigliere": {
    "Sources": {
      "providers": {
        "node": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "streamTimeout": "00:00:30",
          "idleTimeout": "00:01:00",
          "enabledCapabilities": [
            "broadcast",
            "realtime_ingest",
            "block_backfill",
            "raw_tx_fetch",
            "validation_fetch"
          ],
          "connection": {
            "rpcUrl": "http://127.0.0.1:8332",
            "rpcUser": "bitcoin",
            "rpcPassword": "replace-me",
            "zmqTxUrl": "tcp://127.0.0.1:28332",
            "zmqBlockUrl": "tcp://127.0.0.1:28333"
          }
        },
        "junglebus": {
          "enabled": false,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "streamTimeout": "00:00:30",
          "enabledCapabilities": [],
          "connection": {}
        },
        "bitails": {
          "enabled": false,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "enabledCapabilities": [],
          "connection": {}
        },
        "whatsonchain": {
          "enabled": false,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "enabledCapabilities": [],
          "connection": {}
        }
      },
      "routing": {
        "preferredMode": "node",
        "primarySource": "node",
        "fallbackSources": [],
        "verificationSource": "node"
      },
      "capabilities": {
        "broadcast": {
          "mode": "single",
          "source": "node"
        },
        "realtime_ingest": {
          "source": "node"
        },
        "block_backfill": {
          "source": "node"
        },
        "raw_tx_fetch": {
          "source": "node"
        },
        "validation_fetch": {
          "source": "node"
        }
      }
    }
  }
}
```

Notes:
- simplest authoritative setup
- easiest mental model
- highest operational burden because the operator runs the node

## Example 2: `hybrid` Mode With One Primary And Multiple Fallbacks

Use this when the operator wants one clear default source, plus fallbacks and a stronger verification path.

```json
{
  "Consigliere": {
    "Sources": {
      "providers": {
        "node": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "streamTimeout": "00:00:30",
          "idleTimeout": "00:01:00",
          "enabledCapabilities": [
            "broadcast",
            "raw_tx_fetch",
            "validation_fetch"
          ],
          "connection": {
            "rpcUrl": "http://127.0.0.1:8332",
            "rpcUser": "bitcoin",
            "rpcPassword": "replace-me",
            "zmqTxUrl": "tcp://127.0.0.1:28332",
            "zmqBlockUrl": "tcp://127.0.0.1:28333"
          }
        },
        "junglebus": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "streamTimeout": "00:00:30",
          "enabledCapabilities": [
            "realtime_ingest",
            "block_backfill"
          ],
          "connection": {
            "baseUrl": "https://junglebus.gorillapool.io",
            "apiKey": "replace-me"
          }
        },
        "bitails": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "enabledCapabilities": [
            "raw_tx_fetch",
            "block_backfill"
          ],
          "connection": {
            "baseUrl": "https://api.bitails.io",
            "apiKey": "replace-me"
          }
        },
        "whatsonchain": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "rateLimits": {
            "requestsPerMinute": 600
          },
          "enabledCapabilities": [
            "raw_tx_fetch"
          ],
          "connection": {
            "baseUrl": "https://api.whatsonchain.com/v1/bsv/main"
          }
        }
      },
      "routing": {
        "preferredMode": "hybrid",
        "primarySource": "junglebus",
        "fallbackSources": [
          "bitails",
          "node",
          "whatsonchain"
        ],
        "verificationSource": "node"
      },
      "capabilities": {
        "broadcast": {
          "mode": "multi",
          "sources": [
            "node",
            "bitails"
          ]
        },
        "realtime_ingest": {
          "source": "junglebus",
          "fallbackSources": [
            "node"
          ]
        },
        "block_backfill": {
          "source": "junglebus",
          "fallbackSources": [
            "bitails"
          ]
        },
        "raw_tx_fetch": {
          "source": "bitails",
          "fallbackSources": [
            "whatsonchain",
            "node"
          ]
        },
        "validation_fetch": {
          "source": "node"
        }
      }
    }
  }
}
```

Notes:
- good default serious-business profile
- fast ingest through `junglebus`
- cheaper lookup elasticity through `bitails` and `whatsonchain`
- truth-critical validation anchored to `node`

## Example 3: Provider-Only Setup Without A Node

Use this when the operator wants the lowest entry barrier and accepts stronger dependence on upstream APIs.

```json
{
  "Consigliere": {
    "Sources": {
      "providers": {
        "node": {
          "enabled": false,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "enabledCapabilities": [],
          "connection": {}
        },
        "junglebus": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "streamTimeout": "00:00:30",
          "enabledCapabilities": [
            "realtime_ingest",
            "block_backfill"
          ],
          "connection": {
            "baseUrl": "https://junglebus.gorillapool.io",
            "apiKey": "replace-me"
          }
        },
        "bitails": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "enabledCapabilities": [
            "broadcast",
            "raw_tx_fetch",
            "validation_fetch"
          ],
          "connection": {
            "baseUrl": "https://api.bitails.io",
            "apiKey": "replace-me"
          }
        },
        "whatsonchain": {
          "enabled": true,
          "connectTimeout": "00:00:03",
          "requestTimeout": "00:00:10",
          "rateLimits": {
            "requestsPerMinute": 600
          },
          "enabledCapabilities": [
            "raw_tx_fetch"
          ],
          "connection": {
            "baseUrl": "https://api.whatsonchain.com/v1/bsv/main"
          }
        }
      },
      "routing": {
        "preferredMode": "junglebus",
        "primarySource": "junglebus",
        "fallbackSources": [
          "bitails",
          "whatsonchain"
        ],
        "verificationSource": "bitails"
      },
      "capabilities": {
        "broadcast": {
          "mode": "multi",
          "sources": [
            "bitails",
            "whatsonchain"
          ]
        },
        "realtime_ingest": {
          "source": "junglebus"
        },
        "block_backfill": {
          "source": "junglebus",
          "fallbackSources": [
            "bitails"
          ]
        },
        "raw_tx_fetch": {
          "source": "bitails",
          "fallbackSources": [
            "whatsonchain"
          ]
        },
        "validation_fetch": {
          "source": "bitails",
          "fallbackSources": [
            "whatsonchain"
          ]
        }
      }
    }
  }
}
```

Notes:
- cheapest entry point
- no self-hosted node required
- best for businesses that need fast adoption more than full source sovereignty
- weaker truth posture than node-backed hybrid mode

## Modeling Notes

- Disabled provider sections remain present on purpose.
- `routing` carries the default posture.
- `capabilities` only overrides where the economics, correctness, or delivery profile materially differs from the default route.
- `broadcast` may be single-target or multi-target.
- `verificationSource` is a role, not a guarantee that every request goes through that source.

## Non-Goals For These Examples

These examples do not try to show:
- environment-variable projection
- secret-management strategy
- payload store configuration
- future `network_connector` wiring
- every provider-specific connection field

Those should be documented separately once the runtime binding is implemented.
