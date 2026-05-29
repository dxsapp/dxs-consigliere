# Consigliere

A high-performance BSV indexer designed for scalable payment processing and real-time UTXO tracking.
Built for payment processors that need real-time visibility, low latency,
and cost-efficient blockchain monitoring at scale.

- **Selective UTXO indexing** – Track only relevant payment and settlement addresses, not the full chain  
- **High-throughput ready** – Designed for large volumes of micro-payments with minimal latency  
- **Dynamic address onboarding** – Add new payment or merchant addresses instantly, without reindexing  
- **Back-to-Genesis for STAS** – Full provenance verification for token-based payment flows  
- **Real-time updates** – Live transaction and balance notifications via SignalR  
- **RavenDB-powered** – Fast, scalable document storage optimized for transactional workloads  


---

## 📌 Overview

**Consigliere** is a high-performance blockchain indexer for the Bitcoin SV (BSV) network, purpose-built to handle **STAS token** transactions and fully resolve the **Back to Genesis** problem.  
It maintains an accurate, real-time state of all STAS token UTXOs by tracing their provenance back to the original issuance transaction, ensuring reliable token ownership verification.

Rather than indexing the entire blockchain, Consigliere is built around **selective UTXO indexing**. It monitors only explicitly configured addresses—such as payment, settlement, or merchant deposit addresses—making it well suited for high-throughput payment flows and micro-payment workloads. This targeted approach significantly reduces infrastructure load, storage requirements, and operational costs.

Addresses can be **added dynamically at runtime**, allowing payment processors to onboard new merchants, rotate addresses, or scale transaction volume without reindexing or service interruption. Combined with real-time updates delivered via SignalR and a RavenDB-backed data model, Consigliere delivers low-latency visibility into both confirmed and unconfirmed funds, enabling fast payment detection, reconciliation, and settlement at scale.

---

## 🚀 Key Features

- **Selective UTXO Indexing**  
  Indexes only explicitly configured addresses (payment, settlement, merchant deposits), avoiding full-chain address tracking and significantly reducing infrastructure load and operating costs.

- **High-Throughput Payment Processing**  
  Designed to handle large volumes of transactions and micro-payments with low latency, making it suitable for sustained, high-frequency payment workloads.

- **STAS Back-to-Genesis Resolution**  
  Fully resolves token provenance by tracing each STAS UTXO back to its original genesis transaction, ensuring accurate ownership and lineage verification.

- **Multiple Transaction Types Support (STAS & P2PKH)**  
  Natively indexes various transaction types, including STAS tokens and standard P2PKH transactions, enabling unified handling of token-based and native BSV payment flows.

- **Dynamic Address Onboarding**  
  Allows new addresses to be added at runtime without reindexing or downtime, supporting merchant onboarding, address rotation, and scalable payment operations.

- **Real-Time Event Streaming (SignalR)**  
  Push-based WebSocket notifications for transaction detection, balance changes, and UTXO state updates, enabling immediate reaction to incoming payments.

- **RavenDB-Powered Data Model**  
  Uses RavenDB’s document-oriented architecture for fast writes, efficient queries, and scalable storage of UTXO state and transaction history.

- **Blockchain Reorganization Safety**  
  Automatically detects and handles chain reorganizations, reindexing affected data to maintain a consistent and correct view of the blockchain state.

---

## 🛠 Tech Stack

- **Language:** C# (.NET)
- **Blockchain:** Bitcoin SV (BSV)
- **Database:** RavenDB
- **Realtime Updates:** SignalR WebSockets

---

## Docker Setup

[Docker Hub](https://hub.docker.com/r/dxs/consigliere)

### Run locally (recommended for self-hosting)

The fastest path to a working node on your own machine: pull
the published image, run one compose command, finish the
first-run wizard in the browser. No domain, no TLS cert, no
manual config.

```bash
# 1. Get a free JungleBus subscription id from GorillaPool
#    (https://gorillapool.io) — you'll paste it into the wizard.

# 2. Start the stack (RavenDB + Consigliere, published image):
docker compose -f compose.local.yml up -d

# 3. Open the admin UI and complete the first-run wizard:
#    http://localhost:5000
#    → admin account → providers → block-sync (paste the
#      JungleBus subscription id) → confirm.

# 4. Add a watched address (Tracked Addresses screen). It starts
#    indexing live — no restart needed: the block-sync + realtime
#    ingest tasks watch the provider config and re-bind themselves
#    when the wizard writes it.
```

Pin a specific release instead of `latest`:

```bash
CONSIGLIERE_TAG=1.2.3 docker compose -f compose.local.yml up -d
```

Stop / wipe:

```bash
docker compose -f compose.local.yml down       # stop
docker compose -f compose.local.yml down -v     # stop + delete data
```

> This local profile serves **plain HTTP on `localhost:5000`**
> (cookie `Secure` flag relaxed so login works over http). It is
> for a single machine on a trusted network — **do not expose it
> to the public internet as-is**. For an internet-facing
> deployment use the TLS-fronted prod profile
> (`docker compose -f compose.yml -f compose.prod.yml --profile
> prod up -d`) and follow [`docs/runbook.md`](docs/runbook.md).

### Advanced: bring-your-own RavenDB + BSV node (ZMQ)

If you run your own RavenDB + a full BSV node and prefer the
node/ZMQ ingest path over managed providers, run the image
directly and add watched addresses via the Admin API:

```bash
docker run -p 5000:5000 \
  -e "RavenDb__Urls__0=http://ravendb:8080" \
  -e "RavenDb__DbName=Consigliere" \
  -e "BsvNodeApi__BaseUrl=http://your-node:18332" \
  -e "BsvNodeApi__User=your_user" \
  -e "BsvNodeApi__Password=your_password" \
  -e "ZmqClient__RawTx2Address=tcp://your-node:28332" \
  -e "ZmqClient__RemovedFromMempoolBlockAddress=tcp://your-node:28332" \
  -e "ZmqClient__DiscardedFromMempoolAddress=tcp://your-node:28332" \
  -e "ZmqClient__HashBlock2Address=tcp://your-node:28332" \
  dxs/consigliere:latest
```

Use the Admin API to add addresses/tokens to watch after startup.

### Docker Release Policy

Release images are published automatically from Git tags.

Stable release trigger:

- push tag `vX.Y.Z`

Published Docker tags for `vX.Y.Z`:

- `dxs/consigliere:X.Y.Z`
- `dxs/consigliere:X.Y`
- `dxs/consigliere:X`
- `dxs/consigliere:latest`

Notes:

- prerelease tags are ignored by the DockerHub workflow in v1
- `latest` always points to the most recent stable `vX.Y.Z` release
- Git tag is the release source of truth

Maintainer release steps:

```bash
git checkout main
git pull --ff-only
git tag vX.Y.Z
git push origin vX.Y.Z
```

Required GitHub secrets for the workflow:

- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

### Docker Compose E2E Smoke (contributors / CI)

> Running the product? Use [Run locally](#run-locally-recommended-for-self-hosting)
> above — it pulls the published image and gives you live ingest
> through the wizard. The `compose.yml` stack below **builds from
> source** and **disables background tasks** (no live ingest); it
> exists for admin-shell / API smoke + SPA validation, not as a
> product run.

For local end-to-end smoke testing of the source tree, the repository includes a root `compose.yml`.
This stack is intentionally minimal:

- `ravendb`
- `consigliere` (built from source)

It is designed for admin-shell and API smoke testing, not for live chain ingest.
The compose profile:

- enables admin auth
- disables background tasks that require node/ZMQ connectivity
- uses RavenDB only

Run:

```bash
docker compose up --build
```

Stop and clean it:

```bash
docker compose down -v
```

Endpoints:

- Consigliere: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- RavenDB Studio: `http://localhost:8080`

Default admin credentials for compose smoke:

- username: `admin`
- password: `admin123!`

Notes:

- compose uses `ravendb/ravendb:7.1-latest`, which resolves to a native multi-arch RavenDB image on both `amd64` and `arm64`
- the compose profile is intended for admin/API smoke and SPA validation, not live node/ZMQ ingest

Deep-link SPA routes are expected to work in this mode because the admin bundle is published into `wwwroot` and ASP.NET serves `index.html` as fallback.

## 📦 Manual Setup

> ⚠️ Consigliere was developed by **DXS** for internal operations. External deployment may require adjustments.

```bash
# Clone the repository
git clone https://github.com/dxsapp/dxs-consigliere.git
cd dxs-consigliere/src/Dxs.Consigliere
```

## Configuration

### Using appsettings.json

Create `src/Dxs.Consigliere/appsettings.Development.json` for local development:

```json
{
  "Network": "Testnet",
  "RavenDb": {
    "Urls": ["http://localhost:8080"],
    "DbName": "Consigliere"
  },
  "ZmqClient": {
    "RawTx2Address": "tcp://localhost:28332",
    "RemovedFromMempoolBlockAddress": "tcp://localhost:28332",
    "DiscardedFromMempoolAddress": "tcp://localhost:28332",
    "HashBlock2Address": "tcp://localhost:28332"
  },
  "BsvNodeApi": {
    "BaseUrl": "http://localhost:18332",
    "User": "your_rpc_user",
    "Password": "your_rpc_password"
  },
  "TransactionFilter": {
    "Addresses": [],
    "Tokens": []
  }
}
```

**Configuration Notes**:
- `Network`: Set to `"Mainnet"` or `"Testnet"` to match your BSV node
- RavenDB: `8080` (default)
- BSV Node RPC: `8332` (mainnet) or `18332` (testnet)
- BSV Node ZMQ: `28332` (default)

### Managing Watched Addresses & Tokens

Use the **Admin API** to dynamically add/remove addresses and tokens (recommended):

```bash
# Add an address to watch
POST /api/admin/manage/address
{
  "address": "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
  "name": "Genesis Address"
}

# Add a STAS token to watch
POST /api/admin/manage/stas-token
{
  "tokenId": "542a56ec7a307fd68bf925d8f4d525ca61e868ad",
  "symbol": "USDT-TON"
}
```

These settings persist in RavenDB and survive restarts. Alternatively, you can bootstrap addresses/tokens in `TransactionFilter` config, but the API approach is more flexible.

## Run

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the project
dotnet run
```

## Usage

Swagger can be found at the http://localhost:5000/swagger

## WebSocket API (SignalR)

Hub route: `/ws/consigliere`

Server methods (client calls):
- `SubscribeToTransactionStream({ address, slim })`
- `UnsubscribeToTransactionStream({ address, slim })`
- `GetBalance({ addresses, tokenIds })`
- `GetHistory({ address, tokenIds, desc, skipZeroBalance, skip, take })`
- `GetUtxoSet({ tokenId, address, satoshis })`
- `GetTransactions([txId, ...])`
- `Broadcast(rawTxHex)`

Client callbacks (server calls):
- `OnTransactionFound(hex)`
- `OnTransactionDeleted(txid)`
- `OnBalanceChanged(balanceDto)`

### Client example (JavaScript, SignalR)

```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/ws/consigliere")
  .withAutomaticReconnect()
  .build();

connection.on("OnTransactionFound", (hex) => {
  console.log("tx found", hex);
});

connection.on("OnBalanceChanged", (balanceDto) => {
  console.log("balance changed", balanceDto);
});

await connection.start();

await connection.invoke("SubscribeToTransactionStream", {
  address: "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
  slim: false
});
```

### Client example (.NET, SignalR)

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/ws/consigliere")
    .WithAutomaticReconnect()
    .Build();

connection.On<string>("OnTransactionFound", hex =>
{
    Console.WriteLine($"tx found {hex}");
});

connection.On<object>("OnBalanceChanged", balance =>
{
    Console.WriteLine($"balance changed {balance}");
});

await connection.StartAsync();

await connection.InvokeAsync("SubscribeToTransactionStream", new
{
    address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
    slim = false
});
```

## Ops

Production deployment, monitoring, secret rotation, and
disaster recovery are documented in
[`docs/runbook.md`](docs/runbook.md). That document is the
operator-facing source of truth — read it instead of the
source code for any production-operations question.

## Author

- Author: [Oleg Panagushin](https://github.com/panagushin)  
  CTO / System Architect — Crypto & FinTech
