module SqlHydra.Npgsql.Provider

open SqlHydra.Domain

let provider =
    {
        Provider.Id = "npgsql"
        Provider.Name = "SqlHydra.Npgsql"
        Provider.Type = Npgsql
        Provider.DefaultReaderType = "Npgsql.NpgsqlDataReader"
        Provider.DefaultProvider = "Npgsql"
        Provider.GetSchema = NpgsqlSchemaProvider.getSchema
    }

type NpgsqlProvider() =
    interface ISqlHydraDbProvider with
        member _.CreateMappings(isLegacy) = NpgsqlDataTypes.typeMappingsByName isLegacy
        member _.GetSchema(cfg, isLegacy) = NpgsqlSchemaProvider.getSchema(cfg, isLegacy)
        member _.ProviderMetadata =
            {
                ProviderMetadata.Id = "npgsql"
                ProviderMetadata.Name = "SqlHydra.Npgsql"
                ProviderMetadata.Type = Npgsql
                ProviderMetadata.DefaultReaderType = "Npgsql.NpgsqlDataReader"
                ProviderMetadata.DefaultProvider = "Npgsql"
            }
