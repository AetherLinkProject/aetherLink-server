# AetherLinkServer: Off-chain Oracle System Overview & Structure

## Overview

AetherLinkServer is an off-chain oracle and consensus service system designed for multi-chain ecosystems. It is responsible for off-chain data collection, consensus, on-chain synchronization, price aggregation, and backend API services. The system emphasizes modularity, extensibility, and automated development, supporting multi-node collaboration, cross-chain messaging, distributed task scheduling, and high availability.

- **Decentralized Oracle Cluster:** The AetherLink.Worker module forms a decentralized, distributed, and secure oracle node cluster, ensuring trustless and resilient off-chain computation and consensus.
- **Multi-chain Event Indexing:** The AetherLink.Indexer module supports on-chain event subscription for aelf, EVM-compatible chains (including Ethereum Base and BSC), and TON, enabling broad interoperability and real-time data flow across heterogeneous blockchains.
- **Multi-source Price Aggregation:** The PriceServer module aggregates price data from multiple sources, providing robust and reliable price oracles for downstream applications and consensus processes.
- **Centralized Backend Aggregation:** The AetherLink.Server.HttpApi module serves as the official backend service, aggregating oracle-related data and providing a centralized platform backend for external integrations and data consumers.
- **Distributed & Multisignature Security:** Orleans-based distributed architecture and multisignature mechanisms for security and resilience.

---

## Directory Structure

```
/
├── docs/                         # Project documentation
│   ├── project_tracker.md        # Task tracking and development status
│   └── PROJECT_OVERVIEW_AND_STRUCTURE.md  # Architecture and module documentation
├── src/
│   ├── AetherLink.Worker/        # Off-chain consensus and task scheduling core
│   ├── AetherLink.Worker.Core/   # Core logic for consensus, scheduling, node management
│   ├── AetherLink.Indexer/       # On-chain event scanning and synchronization
│   ├── Aetherlink.PriceServer/   # Price aggregation and service
│   ├── AetherLink.Server.HttpApi.Host/ # Backend API service host
│   ├── AetherLink.Server.HttpApi/      # API definitions and controllers
│   ├── AetherLink.Server.Grain/        # Orleans Grain distributed services for HttpApi
│   ├── AetherLink.Multisignature/      # Multisignature and security mechanisms
│   ├── AetherLink.Metric/              # Monitoring and metrics
│   ├── AetherLink.Server.Domain/       # Domain modeling and core business logic
│   ├── AetherLink.Server.Domain.Shared/# Shared domain and constants
│   ├── AetherLink.Server.EntityFrameworkCore/ # Data persistence
│   ├── AetherLink.Server.DbMigrator/   # Database migration and initialization
│   ├── AetherLink.Server.Silo/         # Orleans Silo host
│   ├── AetherLink.MockServer/          # Testing and mock services
```

---

## Module Documentation

### 1. Off-chain Consensus & Worker
- **AetherLink.Worker / AetherLink.Worker.Core**
  - Decentralized, distributed, and secure oracle node cluster for off-chain computation and consensus.
  - Multi-node task scheduling, message signing, consensus aggregation, node health management.
  - Supports automated task assignment and off-chain consensus workflows.

### 2. Blockchain Scanner
- **AetherLink.Indexer**
  - Supports on-chain event subscription for aelf, EVM-compatible chains (including Base and BSC), and TON.
  - Real-time monitoring of on-chain events, triggering off-chain consensus and data synchronization.
  - Enables broad interoperability and real-time data flow across heterogeneous blockchains.
  - Supports multi-chain extension and event filtering.

### 3. Price Feed Service
- **Aetherlink.PriceServer**
  - Aggregates price data from multiple sources, providing highly available and robust price oracles.
  - Supports API queries and outputs from off-chain consensus results.

### 4. Backend API Service
- **AetherLink.Server.HttpApi.Host / HttpApi**
  - Official backend service for AetherLink, aggregating oracle-related data as a centralized platform backend.
  - Provides REST/gRPC interfaces for external integration, data access, and management.
  - Supports access control and multi-tenant extension.

### 5. Distributed & Security
- **AetherLink.Server.Grain**
  - Orleans Grain distributed services for high availability and scalability.
- **AetherLink.Multisignature**
  - Multisignature mechanisms to secure critical operations.

### 6. Monitoring & Metrics
- **AetherLink.Metric**
  - Monitors system health and key metrics.

### 7. Domain & Persistence
- **AetherLink.Server.Domain / Domain.Shared**
  - Domain modeling, core business logic, and constants.
- **AetherLink.Server.EntityFrameworkCore**
  - Data persistence and ORM.
- **AetherLink.Server.DbMigrator**
  - Database migration and initialization.

### 8. Silo Host & Mock
- **AetherLink.Server.Silo**
  - Orleans Silo host for distributed services.
- **AetherLink.MockServer**
  - Testing and mock services for integration and end-to-end testing.

---

## Data Flow & Relationships

**Data Flow Summary:**
1. On-chain events are monitored by the Indexer, triggering off-chain Worker tasks.
2. Worker nodes collaborate to complete data collection, signing, and consensus.
3. Consensus results are provided externally via the API service or synchronized back on-chain.
4. PriceServer aggregates price data for consensus and API queries.
5. Multisignature and distributed mechanisms ensure security and high availability.

**Relationship Diagram (Text):**

```
[Blockchain] → (Indexer) → (Worker/Consensus) → (API/PriceServer) → [External]
                                 ↓
                         (Multisignature)
                                 ↓
                             (Grain/Silo)
```

---

## Automation & Extensibility
- Supports automated development and task tracking (see project_tracker.md).
- Modular design for easy feature extension and multi-chain adaptation.
- Emphasizes testing, monitoring, and continuous integration.

---

_This file is part of the automated development workflow. Structure and content may be updated as the project evolves._ 