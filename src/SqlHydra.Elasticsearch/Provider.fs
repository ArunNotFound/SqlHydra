namespace SqlHydra.Elasticsearch

open SqlHydra.Domain

type ElasticsearchProvider() =
    interface ISqlHydraDbProvider with
        member _.Id = "elasticsearch"
        member _.Name = "SqlHydra.Elasticsearch"
        member _.Type = Custom "Elasticsearch"
        member _.DefaultReaderType = "System.Data.Common.DbDataReader"
        member _.DefaultProvider = "Elastic.Clients.Elasticsearch"
        member _.SqlEmitter = "SqlHydra.Elasticsearch.ElasticsearchEmitter()"
        member _.ProviderConnectionType = "Elastic.Clients.Elasticsearch.ElasticsearchClient"
        member _.GetSchema(cfg, isLegacy, extensions) = 
            ElasticsearchSchemaProvider.getSchema(cfg, isLegacy, extensions)

type ElasticsearchEmitter() =
    class end
