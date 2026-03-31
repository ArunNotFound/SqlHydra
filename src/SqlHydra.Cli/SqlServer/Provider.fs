module SqlHydra.SqlServer.Provider

open SqlHydra.Domain

let provider =
    {
        Provider.Id = "mssql"
        Provider.Name = "SqlHydra.SqlServer"
        Provider.Type = SqlServer
        Provider.DefaultReaderType = "Microsoft.Data.SqlClient.SqlDataReader"
        Provider.DefaultProvider = "Microsoft.Data.SqlClient"
        Provider.GetSchema = SqlServerSchemaProvider.getSchema
    }

type SqlServerProvider() =
    interface ISqlHydraDbProvider with
        member _.CreateMappings(isLegacy) = SqlServerDataTypes.typeMappingsByName isLegacy
        member _.GetSchema(cfg, isLegacy) = SqlServerSchemaProvider.getSchema(cfg, isLegacy)
        member _.ProviderMetadata =
            {
                ProviderMetadata.Id = "mssql"
                ProviderMetadata.Name = "SqlHydra.SqlServer"
                ProviderMetadata.Type = SqlServer
                ProviderMetadata.DefaultReaderType = "Microsoft.Data.SqlClient.SqlDataReader"
                ProviderMetadata.DefaultProvider = "Microsoft.Data.SqlClient"
            }
