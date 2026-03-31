module SqlHydra.Sqlite.Provider

open SqlHydra.Domain

let provider =
    {
        Provider.Id = "sqlite"
        Provider.Name = "SqlHydra.Sqlite"
        Provider.Type = Sqlite
        Provider.DefaultReaderType = "System.Data.Common.DbDataReader"
        Provider.DefaultProvider = "System.Data.SQLite"
        Provider.GetSchema = SqliteSchemaProvider.getSchema
    }

type SqliteProvider() =
    interface ISqlHydraDbProvider with
        member _.CreateMappings(isLegacy) = SqliteDataTypes.typeMappingsByName isLegacy
        member _.GetSchema(cfg, isLegacy) = SqliteSchemaProvider.getSchema(cfg, isLegacy)
        member _.ProviderMetadata =
            {
                ProviderMetadata.Id = "sqlite"
                ProviderMetadata.Name = "SqlHydra.Sqlite"
                ProviderMetadata.Type = Sqlite
                ProviderMetadata.DefaultReaderType = "System.Data.Common.DbDataReader"
                ProviderMetadata.DefaultProvider = "System.Data.SQLite"
            }
