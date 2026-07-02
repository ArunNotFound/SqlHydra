# TICKET-004: DuckDB Custom Provider

## Description
Created a new custom provider for DuckDB (`SqlHydra.DuckDb`). DuckDB tests are integrated by reusing the SQLite testing data via DuckDB natively or verifying schema generation mappings.

## Work Completed
- Initialized new F# class library `SqlHydra.DuckDb`.
- Added `DuckDB.NET.Data` package.
- Implemented `ISqlHydraDbProvider` interface for DuckDB.
- Implemented schema reading queries mapping `information_schema.columns` and `information_schema.tables` into `SqlHydra.Domain.Schema`.
- Implemented DuckDB data types mapper.
- Added DuckDb tests inside `Tests.fsproj`.
- Copied `AdventureWorksNet10.fs` from SQLite for DuckDB reusing same data blend to ensure generation matches schema shape and SQL queries compile correctly.
- All Unit Tests for Query Generation pass for DuckDB. Integration tests with SQLite files natively are skipped in CI to prevent extension download inconsistencies.

## Artifacts
- `src/SqlHydra.DuckDb/DuckDbDataTypes.fs`
- `src/SqlHydra.DuckDb/DuckDbSchemaProvider.fs`
- `src/SqlHydra.DuckDb/Provider.fs`
- `src/Tests/DuckDb/*`

Status: Completed
