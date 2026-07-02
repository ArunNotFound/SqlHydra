# SqlHydra .NET 10 Migration Complete

The migration of the SqlHydra repository to exclusively support `.NET 10` has been successfully implemented and pushed to the remote repository.

## Summary of Changes

1. **Updated Projects to `.net10.0`**:
   - `src/Tests/Tests.fsproj`: Removed all `net8.0` and `net9.0` specific conditionals, keeping `net10.0`.
   - `src/SqlHydra.Cli/SqlHydra.Cli.fsproj`: Updated `TargetFrameworks` to `net10.0` and updated/simplified dependencies.
   - `src/SqlHydra.Query/SqlHydra.Query.fsproj`: Kept `netstandard2.0` and `net10.0` and cleaned up package configurations.

2. **Cleaned Up Obsolete Files**:
   - Removed 20+ `.fs` and `.bat/.toml` files from the `src/Tests/` directory that were specifically used for `net8` and `net9` tests.
   - Updated the `src/Tests/regen-all.bat` script to run only the `.net10` batch files.

3. **Sample Applications**:
   - Updated `src/SampleApp/sqlhydra-mssql.bat` and `sqlhydra-sqlite.bat` to target the `net10.0` framework in the `SqlHydra.Cli` project instead of older versions.

4. **Build & CI Pipeline**:
   - `src/Build/Program.fs`: Simplified the build pipeline by removing `BuildCliNet8`, `TestNet8`, `BuildCliNet9`, and `TestNet9` targets. The pipeline now cleanly progresses with only the `net10` equivalents.
   - `.github/workflows/build-and-test.yml`: Configured the GitHub Actions workflow to setup only `10.x` and run tests against the `net10.0` framework.

5. **Commit and Push**:
   - Staged all modifications and deleted files.
   - Pushed successfully to the `main` branch.

> [!WARNING]
> **GitHub Issue Workflow**
> I noticed you requested that I track this via a GitHub Issue (`gh issue create` / `gh issue close`). However, the GitHub CLI tool (`gh`) is currently unauthenticated on your system and yielded a `401 Bad Credentials` error. If you'd like me to manage GitHub issues directly in the future, you will first need to run `gh auth login` in your terminal!

## Project Tracking

Going forward, the project's strategy for issue and project management heavily utilizes native GitHub capabilities:
- **Issues & Sub-issues**: All tasks are tracked using GitHub Issues. For larger initiatives, we leverage parent and sub-issue progress fields to map out hierarchies and measure granular completion statuses.
- **Projects & Insights**: GitHub Projects gives us customizable views (lists, boards) along with Insights charts built from our data.
- **Fields**: Organization-level custom fields are used on issues to establish `priority`, `effort`, and other necessary metadata.
- **Milestones & Iterations**: Epics and broad features are tracked via Milestones, while short-term task grouping utilizes Iteration fields.
- **Pull Requests**: Pull Request tracking fields are deeply embedded into the project boards, linking reviews and statuses back to the original issues automatically.
