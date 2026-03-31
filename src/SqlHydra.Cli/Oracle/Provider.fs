module SqlHydra.Oracle.Provider

open SqlHydra.Domain

let provider =
    {
        Provider.Id = "oracle"
        Provider.Name = "SqlHydra.Oracle"
        Provider.Type = Oracle
        Provider.DefaultReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader"
        Provider.DefaultProvider = "Oracle.ManagedDataAccess.Core"
        Provider.GetSchema = OracleSchemaProvider.getSchema
    }

type OracleProvider() =
    interface ISqlHydraDbProvider with
        member _.CreateMappings(_isLegacy) = OracleDataTypes.typeMappingsByName
        member _.GetSchema(cfg, isLegacy) = OracleSchemaProvider.getSchema(cfg, isLegacy)
        member _.ProviderMetadata =
            {
                ProviderMetadata.Id = "oracle"
                ProviderMetadata.Name = "SqlHydra.Oracle"
                ProviderMetadata.Type = Oracle
                ProviderMetadata.DefaultReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader"
                ProviderMetadata.DefaultProvider = "Oracle.ManagedDataAccess.Core"
            }
