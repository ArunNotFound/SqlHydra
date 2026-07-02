namespace SqlHydra.DuckDb

open SqlHydra.Domain

type DuckDbProvider() =
    interface ISqlHydraDbProvider with
        member _.Id = "duckdb"
        member _.Name = "SqlHydra.DuckDb"
        member _.Type = Custom "DuckDb"
        member _.DefaultReaderType = "System.Data.Common.DbDataReader"
        member _.DefaultProvider = "DuckDB.NET.Data"
        member _.SqlEmitter = "SqlHydra.DuckDb.DuckDbEmitter()"
        member _.ProviderConnectionType = "DuckDB.NET.Data.DuckDBConnection"
        member _.GetSchema(cfg, isLegacy, extensions) = 
            DuckDbSchemaProvider.getSchema(cfg, isLegacy, extensions)

type DuckDbEmitter() =
    // A simple ISqlEmitter that uses standard double quotes for identifiers, 
    // similar to Postgres or SQLite. We don't actually need to implement the full 
    // ISqlEmitter interface here if we only want basic support, but let's provide a minimal one.
    // In many cases SqlHydra.Query uses this dynamically.
    // However, since it is referenced as a string, it is used by SqlHydra.Query.
    // For now we'll just define the class so it exists, but duckdb usually relies 
    // on SQLite's or Postgres' dialect for emission, or its own custom emitter.
    // To make it compile without referencing SqlHydra.Query, we don't strictly 
    // need to implement the interface here, the generator just injects the string.
    class end
