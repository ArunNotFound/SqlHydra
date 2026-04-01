module SqlHydra.Oracle.Provider

open SqlHydra.Domain

type OracleProvider() =
    interface ISqlHydraDbProvider with
        member _.Id = "oracle"
        member _.Name = "SqlHydra.Oracle"
        member _.Type = Oracle
        member _.DefaultReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader"
        member _.DefaultProvider = "Oracle.ManagedDataAccess.Core"
        member _.SqlKataCompiler = "SqlKata.Compilers.OracleCompiler()"
        member _.ProviderConnectionType = "Oracle.ManagedDataAccess.Client.OracleConnection"
        member _.GetSchema(cfg, isLegacy) = OracleSchemaProvider.getSchema(cfg, isLegacy)
