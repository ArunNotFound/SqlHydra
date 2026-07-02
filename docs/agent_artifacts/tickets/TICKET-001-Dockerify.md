# TICKET-001: Dockerify Test Environment

## Activity Tracking
- **Goal**: Establish a unified, repeatable "lab" environment for the SqlHydra integration test suite that doesn't rely on local host binaries for SQL Server, PostgreSQL, Oracle, and MySQL.
- **Implementation**: 
  - Created individual `Dockerfile` configurations under the `docker/` folder.
  - Authored a root `docker-compose.yml` to orchestrate building these files, exposing the database ports exactly as expected by the existing TOML configurations in `src/Tests/`.
- **Status**: Completed successfully.

## Git Details
- **Commit Hash**: `fe6ff46`
- **Commit Message**: "feat: Dockerify test environment and initialize AdventureWorks data"
- **Files Modified**: 
  - `[NEW] docker-compose.yml`
  - `[NEW] docker/mssql/Dockerfile`
  - `[NEW] docker/postgres/Dockerfile`
  - `[NEW] docker/oracle/Dockerfile`
  - `[NEW] docker/mysql/Dockerfile`
