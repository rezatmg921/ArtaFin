# ADR-0001: Start with a modular monolith

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

ArtaFin is a financial application whose correctness, auditability, and maintainability matter more initially than independent deployment of many services. Premature distribution would introduce network failure, eventual consistency, deployment, and operational costs into critical money workflows.

## Decision

Begin with one deployable application organized into explicit modules and bounded contexts. Use DDD where it clarifies domain invariants, and CQRS at the application boundary for distinct commands and queries. Keep module internals encapsulated and expose deliberate contracts.

## Consequences

- Core posting and transfer workflows can use local ACID transactions.
- Modules must not query or mutate another module's private persistence structures.
- Cross-module interaction uses application contracts or domain events in-process.
- Use a transactional outbox before reliable asynchronous integration is required.
- Extract a module into a service only when justified by scaling, isolation, team ownership, or release requirements; extraction requires explicit data ownership and integration contracts.

## Initial modules

Identity & Access; Finance; Transactions; Accounting; Contacts & Debts; Planning & Reporting.
