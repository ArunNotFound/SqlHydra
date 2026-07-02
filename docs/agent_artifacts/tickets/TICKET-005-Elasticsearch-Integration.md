# TICKET-005: Elasticsearch Integration

## Overview
This ticket tracks the major work involved in bringing Elasticsearch into the SqlHydra ecosystem. The goal is to provide a multi-phase implementation for Elasticsearch, catering to both trivial queries (ES SQL) and complex, idiomatic F# queries (Native JSON Query AST).

## Status
- **Phase**: Execution (Phase 1)
- **Assignee**: Antigravity
- **Related Artifacts**:
  - `01_deep_thinking.html`
  - `02_brainstorming.html`
  - `03_swot_analysis.html`

## Phases

### Phase 1: ES SQL (The Low Hanging Fruit)
Leverage the ES `/_sql` endpoint and SqlHydra's existing SQL AST builder. 
* [x] Scaffold `SqlHydra.Elasticsearch.fsproj`
* [x] Implement `ElasticsearchProvider` stub
* [ ] Spin up local ES 8 via Docker Compose
* [ ] Blend dummy data into ES
* [ ] Implement `ElasticsearchSchemaProvider` to read mappings/schema
* [ ] Write integration tests for trivial queries (filter, map, aggregate)

### Phase 2: Native JSON Query AST
Leverage a natively typed F# AST that maps directly to the Elasticsearch JSON Query DSL. This unlocks ES-specific features like `nested` queries and custom scoring.
* [ ] Define `Expr<'row, 'value>` and `Raw` AST (inspired by Symphony)
* [ ] Implement strict `Refined` constraint system
* [ ] Create custom Computation Expression (e.g., `esquery { ... }`)

## Acceptance Criteria
1. Elasticsearch 8 is running locally via Docker.
2. Dummy data is seeded successfully.
3. `SqlHydra.Elasticsearch` can extract schema from the ES cluster.
4. Trivial queries successfully translate to ES SQL and return valid F# records.
