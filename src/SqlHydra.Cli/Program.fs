module SqlHydra.Program

open System
open FSharp.SystemCommandLine
open Input
open Console
open Domain

/// A resolved built-in provider, or a deferred custom provider that needs assembly loading.
type ProviderArg =
    | BuiltIn of ISqlHydraDbProvider
    | Custom

let run (providerArg: ProviderArg, tomlFile: IO.FileInfo option, project: IO.FileInfo option, connString: string option, providerAssembly: string option) =

    let projectOrFirstFound =
        project
        |> Option.map (fun p -> if p.Exists then p else failwith $"Unable to find the specified project file: '{p.FullName}'.")
        |> Option.orElse (IO.DirectoryInfo(".").EnumerateFiles("*.fsproj") |> Seq.tryHead)
        |> Option.defaultWith (fun () -> failwith "Unable to find a .fsproj file in the run directory. Please specify one using the `--project` option.")

    let provider =
        match providerArg with
        | BuiltIn p -> p
        | Custom ->
            match providerAssembly with
            | Some asm -> Extensions.loadProvider projectOrFirstFound asm
            | None -> failwith "The 'custom' provider requires the '--provider-assembly' option specifying the assembly containing an ISqlHydraDbProvider implementation."

    let tomlFile = defaultArg tomlFile (IO.FileInfo($"sqlhydra-{provider.Id}.toml"))

    {
        Provider = provider
        TomlFile = tomlFile
        Project = projectOrFirstFound
        Version = Version.get()
        ConnectionString = connString
    }
    |> Console.run

[<EntryPoint>]
let main argv =
    rootCommand argv {
        description "SqlHydra.Cli"
        inputs (
            argument "provider"
            |> required
            |> desc "The database provider id (e.g. 'mssql', 'npgsql', 'sqlite', 'mysql', 'oracle', 'custom')"
            |> tryParse (fun res ->
                match res.Tokens[0].Value with
                | "mssql" ->  Ok (BuiltIn SqlServer.Provider.instance)
                | "npgsql" -> Ok (BuiltIn Npgsql.Provider.instance)
                | "sqlite" -> Ok (BuiltIn Sqlite.Provider.instance)
                | "mysql" ->  Ok (BuiltIn MySql.Provider.instance)
                | "oracle" -> Ok (BuiltIn Oracle.Provider.instance)
                | "custom" -> Ok Custom
                | providerId -> Error $"Invalid provider id: '{providerId}'. Valid options are: 'mssql', 'npgsql', 'sqlite', 'mysql', 'oracle', or 'custom'."
            ),

            optionMaybe "--toml-file"
            |> alias "-t"
            |> desc "The toml configuration filename. Default: 'sqlhydra-{provider}.toml'",

            optionMaybe "--project"
            |> alias "-p"
            |> desc "The project file to update. If not configured, the first .fsproj found in the run directory will be used.",

            optionMaybe "--connection-string"
            |> alias "-cs"
            |> desc "The DB connection string to use. This will override the connection string in the toml file.",

            optionMaybe "--provider-assembly"
            |> alias "-pa"
            |> desc "The assembly name containing a custom ISqlHydraDbProvider implementation. Required when using the 'custom' provider."
        )
        setAction run
    }
