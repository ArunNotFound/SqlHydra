module SqlHydra.MySql.Provider

open SqlHydra.Domain

let provider =
    {
        Provider.Id = "mysql"
        Provider.Name = "SqlHydra.MySql"
        Provider.Type = MySql
        Provider.DefaultReaderType = "System.Data.Common.DbDataReader"
        Provider.DefaultProvider = "MySql.Data"
        Provider.GetSchema = MySqlSchemaProvider.getSchema
    }

type MySqlProvider() =
    interface ISqlHydraDbProvider with
        member _.CreateMappings(isLegacy) = MySqlDataTypes.typeMappingsByName isLegacy
        member _.GetSchema(cfg, isLegacy) = MySqlSchemaProvider.getSchema(cfg, isLegacy)
        member _.ProviderMetadata =
            {
                ProviderMetadata.Id = "mysql"
                ProviderMetadata.Name = "SqlHydra.MySql"
                ProviderMetadata.Type = MySql
                ProviderMetadata.DefaultReaderType = "System.Data.Common.DbDataReader"
                ProviderMetadata.DefaultProvider = "MySql.Data"
            }
