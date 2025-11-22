# Consigliere

_Developed by DXS_

> **Consigliere – The DXS-built BSV STAS indexer with Back-to-Genesis resolution, powered by RavenDB and real-time SignalR updates.**

---

## 📌 Overview

**Consigliere** is a high-performance blockchain indexer for the Bitcoin SV (BSV) network, purpose-built to handle **STAS token** transactions and fully resolve the **Back to Genesis** problem.  
It maintains an accurate, real-time state of all STAS token UTXOs by tracing their provenance back to the original issuance transaction, ensuring reliable token ownership verification.

---

## 🚀 Key Features

- **Back to Genesis Resolution** – Automatically walks transaction history to the original genesis UTXO for each STAS token, guaranteeing accurate lineage tracking.
- **Real-Time Indexing** – Efficiently processes BSV blocks as they are mined, keeping the index up to date.
- **Robust Data Model with RavenDB** – Stores STAS token metadata, ownership, and transaction history in a highly-performant RavenDB database.
- **Fault Tolerance** – Resilient to blockchain reorganizations, with automatic reindexing of affected transactions.
- **Live WebSocket Updates (SignalR)** – Push-based notifications for token balance changes, transaction events, and provenance updates.

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
