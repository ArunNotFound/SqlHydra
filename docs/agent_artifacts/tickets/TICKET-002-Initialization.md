# TICKET-002: Initialize AdventureWorks Test Data

## Activity Tracking
- **Goal**: Supply the dockerized database instances with the `AdventureWorks` schema and data required for the `dotnet test` suite to run correctly.
- **Implementation**: 
  - Analyzed the repository and discovered existing initialization scripts and backup files located in `src/.devcontainer/`.
  - Re-mapped the `docker-compose.yml` build contexts and volume mounts directly to these `.devcontainer` folders (e.g. `src/.devcontainer/mssql`, `src/.devcontainer/postgresql`, `src/.devcontainer/orcl`).
  - Updated the MS SQL `Dockerfile` base image to `mcr.microsoft.com/mssql/server:2022-latest` to ensure `/opt/mssql-tools18/bin/sqlcmd` compatibility for the data restore script.
  - Successfully booted the containers via `docker-compose up -d --build`, resulting in fully seeded databases.
- **Status**: Completed successfully.

## Git Details
- **Commit Hash**: `fe6ff46`
- **Commit Message**: "feat: Dockerify test environment and initialize AdventureWorks data"
- **Files Modified**: 
  - `[MODIFIED] src/.devcontainer/mssql/Dockerfile`
  - `[MODIFIED] docker-compose.yml`
