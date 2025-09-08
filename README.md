# Consigliere
*Developed by DXS*

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

## 📦 Build and Run

> ⚠️ Consigliere was developed by **DXS** for internal operations. External deployment may require adjustments.

```bash
# Clone the repository
git clone https://github.com/dxsapp/dxs-consigliere.git
cd dxs-consigliere/src/Dxs.Consigliere
```

- Add some BSV addresses and/or STAS token IDs to TransactionFilter config:
  "TransactionFilter": {...}

- Update the appsettings.json file with your RavenDB connection details:
  "RavenDb": {...}

- Add your BSV Node API and ZMQ client settings to appsettings.json:
  "ZmqClient": {...},
  "BsvNodeApi": {...}
  
```bash

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the project
dotnet run
```
---

Swagger available by url http://localhost:5000/swagger

