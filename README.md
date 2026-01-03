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

[Docker hub](https://hub.docker.com/r/dxsapp/consigliere)

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
  dxsapp/consigliere:latest
```

Use Admin API to add addresses/tokens to watch after startup.

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
