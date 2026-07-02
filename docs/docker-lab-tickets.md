# Docker Lab Integration - Tickets and Issues

## [TICKET-001] Dockerify Supported Database Providers
**Status**: Closed
**Description**: The project requires a local "lab" environment that spins up the supported database providers for SqlHydra using Docker, enabling consistent and isolated testing without modifying native host configurations.
**Resolution**:
Created an "umbrella" `docker-compose.yml` file in the repository root. It includes services for MS SQL Server, PostgreSQL, Oracle, and MySQL. 

## [TICKET-002] Map Docker Ports to Test Configurations
**Status**: Closed
**Description**: Ensure that the database containers use the ports and credentials expected by the SqlHydra integration tests in `src/Tests/`.
**Resolution**:
Port mappings and environment variables were assigned directly in the `docker-compose.yml` to mirror the existing TOML configurations:
- `mssql`: Port `12019`, User `sa`, Pass `Password#123`
- `postgres`: Port `54320`, User `postgres`, Pass `postgres`, DB `Adventureworks`
- `oracle`: Port `1521`, Service `XEPDB1`, User `OT`, Pass `Oracle1`
- `mysql`: Port `33060`, Root Pass `mysql`, DB `Adventureworks`

## [TICKET-003] Database Initialization and Test Verification
**Status**: Open / Pending Data
**Description**: The tests assume the existence of an `AdventureWorks` schema and data. While the database engines themselves spin up, they are currently empty.
**Action Items**:
- Volume mount `.sql` scripts or backup files in the `docker-compose.yml` for each respective provider to seed the DB on startup.
- Once seeded, run `dotnet test src/Tests/Tests.fsproj` to verify passing integrations.
