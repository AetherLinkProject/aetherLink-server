# Project Development Tracker

## Status Legend

* 🔜 - Planned (Ready for development)
* 🚧 - In Progress (Currently being developed)
* ✅ - Completed
* 🧪 - In Testing
* 🐛 - Has known issues

## Test Status Legend

* ✓ - Tests Passed
* ✗ - Tests Failed
* ⏳ - Tests In Progress
* ⚠️  - Tests Blocked
* * * Not Started

## Feature Tasks

| ID   | Feature Name                                      | Status | Priority | Branch | Assigned To (MAC) | Coverage | Unit Tests | Regression Tests | Notes                                                                 |
| ---- | ------------------------------------------------- | ------ | -------- | ------ | ----------------- | -------- | ---------- | ---------------- | --------------------------------------------------------------------- |
| F001 | Off-chain Consensus Engine                        | ✅     | High     |        |                   |          |            |                  | Responsible for node consensus, message relay, signature aggregation  |
| F002 | Blockchain Scanner                                | ✅     | High     |        |                   |          |            |                  | Monitors on-chain events, triggers consensus workflow                 |
| F003 | Price Feed Service                                | ✅     | High     |        |                   |          |            |                  | Fetches and aggregates price data from multiple sources               |
| F004 | Project Backend API                               | ✅     | High     |        |                   |          |            |                  | Provides REST/gRPC API for external integration                      |
| F005 | Node Management                                   | ✅     | Medium   |        |                   |          |            |                  | Node registration, permission, and health monitoring                  |
| F006 | Cross-chain Message Relay                         | ✅     | Medium   |        |                   |          |            |                  | Handles cross-chain message delivery and verification                 |
| F007 | TON Address Balance Monitoring                    | 🔜     | Low      |        |                   |          |            |                  | Monitor address balances on TON chain                                |
| F008 | EVM Chain Address Balance Monitoring              | 🔜     | Low      |        |                   |          |            |                  | Monitor address balances on EVM-compatible chains                    |
| F009 | Github Unit Test Coverage                         | 🔜     | Routine  |        |                   |          |            |                  | Integrate and track unit test coverage via Github                    |

## Technical Debt & Refactoring

| ID | Task Description | Status | Priority | Branch | Assigned To (MAC) | Unit Tests | Regression Tests | Notes |
| -- | ---------------- | ------ | -------- | ------ | ----------------- | ---------- | ---------------- | ----- |

## Bug Fixes

| ID  | Bug Description                                              | Status | Priority | Branch | Assigned To (MAC) | Unit Tests | Regression Tests | Notes |
| --- |--------------------------------------------------------------| ------ | -------- | ------ | ----------------- | ---------- | ---------------- | ----- |
| B01 | Oracle Node Periodic DataFeeds Job Cancellation Check        | ✅     | Medium   |        |                   |            |                  | Periodically check if existing DataFeeds Jobs have been cancelled    |
| B02 | OCR Leader Exception Handling                                | 🔜     | Medium   |        |                   |            |                  | Design and implement OCR Leader exception handling (refer to ChainLink or other oracles) |

## Development Metrics

* Total Test Coverage: 0.86%
* Last Updated: 2025-04-29

## Upcoming Automated Tasks

| ID | Task Description | Dependency | Estimated Completion |
| -- | ---------------- | ---------- | -------------------- |

## Notes & Action Items

* Off-chain consensus, blockchain scanning, price feed, and backend API are core modules
* Recommend adding CI/CD automation and improving developer documentation
* Documentation should be updated after core features implementation

---

_This file is maintained automatically as part of the development workflow._ 