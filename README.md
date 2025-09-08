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
-e "RavenDb:Urls:0=RAVEN URL" \
-e "RavenDb:DbName=Consigliere" \
-e "ZmqClient:RawTx2Address=tcp://NODE URL" \
-e "ZmqClient:RemovedFromMempoolBlockAddress=tcp://NODE URL" \
-e "ZmqClient:DiscardedFromMempoolAddress=tcp://NODE URL" \
-e "ZmqClient:HashBlock2Address=tcp://NODE URL" \
-e "BsvNodeApi:BaseUrl=NODE URL" \
-e "BsvNodeApi:User=USER" \
-e "BsvNodeApi:Password=PASSWORD \
-e "TransactionFilter:Addresses:0=BSV ADDRESS 1" \
-e "TransactionFilter:Addresses:1=BSV ADDRESS 2" \
-e "TransactionFilter:Addresses:2=BSV ADDRESS ETC" \
-e "TransactionFilter:Tokens:0=STAS TOKEN ID" \
-e "TransactionFilter:Tokens:1=STAS TOKEN ID" \
consigliere:latest
```

## 📦 Manual Setup

> ⚠️ Consigliere was developed by **DXS** for internal operations. External deployment may require adjustments.

```bash
# Clone the repository
git clone https://github.com/dxsapp/dxs-consigliere.git
cd dxs-consigliere/src/Dxs.Consigliere
```

## Setup

Update the appsettings.json file according to the requirements.

```json
// Add some BSV addresses and/or STAS token IDs to TransactionFilter:
"TransactionFilter": {
  "Addresses": [
    // If you specify a BSV address here, Consigliere will build an index for all new transactions related to this address of all output types it recognizes: P2PKH, STAS, 1SatMnee.
  ],
  "Tokens": [
    // If you specify a STAS token ID here, Consigliere will build an index for all new STAS transactions related to this STAS token.
  ]
},

// Update your RavenDB connection details:
"RavenDb": {
  "Urls": [
    "http://localhost:8080"
  ],
  "DbName": "Consigliere"
},

// Add your BSV Node API and ZMQ client settings:
"ZmqClient": {
  "RawTx2Address": "tcp://{Node IP or Domain}:{Default port is 28332}",
  "RemovedFromMempoolBlockAddress": "tcp://{Node IP or Domain}:{Default port is 28332}",
  "DiscardedFromMempoolAddress": "tcp://{Node IP or Domain}:{Default port is 28332}",
  "HashBlock2Address": "tcp://{Node IP or Domain}:{Default port is 28332}"
},
"BsvNodeApi": {
  "BaseUrl": "http{s}://{Node IP or Domain}:{Default port is 8332}",
  "User": "{Node user}",
  "Password": "{Node user password}"
},
```

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
