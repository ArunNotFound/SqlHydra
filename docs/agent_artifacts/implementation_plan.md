# Migrate SqlHydra to .NET 10 and Remove .NET 8/9 Targets

This issue tracks the work to upgrade the SqlHydra repository to target exclusively .NET 10, removing legacy support for .NET 8, .NET 9, and any older frameworks like .NET 5/6.

## Proposed Changes

### Tests Project & Associated Files
- **`src/Tests/Tests.fsproj`**:
  - Update `<TargetFrameworks>` to `net10.0`.
  - Remove all conditional `<Compile>` inclusions for `net8.0` and `net9.0`.
  - Remove conditional `FSharp.Core` package updates for `net8.0` and `net9.0`.
- **`src/Tests/`**:
  - Delete all generated `.fs` files specific to `.net8` and `.net9` (e.g. `AdventureWorksNet8.fs`, `AdventureWorksNet9.fs` across all DB providers).
  - Delete all generation batch files targeting `.net8` and `.net9` (e.g. `sqlhydra-mssql-net8.bat`, `sqlhydra-npgsql-net9.toml`, etc.).
  - Clean up `regen-all.bat` to only run the `net10` batch scripts.

### CLI & Query Projects
- **`src/SqlHydra.Cli/SqlHydra.Cli.fsproj`**:
  - Update `<TargetFrameworks>` to `net10.0`.
  - Clean up conditional `<PackageReference>` for `FSharp.Core` and `Npgsql` to use the latest versions unconditionally.
- **`src/SqlHydra.Query/SqlHydra.Query.fsproj`**:
  - Update `<TargetFrameworks>` to `netstandard2.0;net10.0` (retaining netstandard2.0 for library broad compatibility).
  - Clean up conditional `<PackageReference>` for `FSharp.Core`.

### Sample App
- **`src/SampleApp/sqlhydra-mssql.bat` & `sqlhydra-sqlite.bat`**:
  - Update to use `--framework net10.0`.

### Build System & CI
- **`src/Build/Program.fs`**:
  - Remove build and test steps for `net8.0` and `net9.0`. Ensure it compiles and tests exclusively against `net10.0`.
- **`.github/workflows/build-and-test.yml`**:
  - Update `actions/setup-dotnet` to install `10.x` instead of `8.x` and `9.x`.
  - Consolidate build and test matrix to run only `dotnet test --framework net10.0`.

## Workflow Plan
1. Receive approval for this plan.
2. Create this plan as a GitHub Issue using the `gh` CLI.
3. Execute the code changes in the repository.
4. Verify changes by building the solution.
5. Commit and push the changes.
6. Close the GitHub Issue via the `gh` CLI.

## Project Tracking Strategy

As part of our commitment to visibility and effective execution:
- **Milestones**: This migration will be tracked under a dedicated `.NET 10 Migration` milestone.
- **Iterations**: Execution will be structured within current iteration blocks to monitor timeline accuracy.
- **Insights & Fields**: This epic issue will act as a parent issue for granular tasks, leveraging organization-level custom fields for `priority` and `effort` mapping in GitHub Projects. Pull Requests associated with this work will dynamically reflect their status into our primary project boards.
