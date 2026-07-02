module SqlHydra.Elasticsearch.Tests.ElasticsearchIndexTests

open NUnit.Framework
open Swensen.Unquote
open SqlHydra.Elasticsearch.IndexManager
open SqlHydra.Domain

let makeCol name clrType =
    {
        Name = name
        TypeMapping = { ClrType = clrType; DbType = System.Data.DbType.String; ProviderDbType = None; ColumnTypeAlias = clrType }
        IsNullable = false
        IsPK = false
    }

[<Test>]
let ``Build Index Mapping JSON translates F# Columns to ES Properties`` () =
    let cols = [
        makeCol "id" "System.Int32"
        makeCol "title" "System.String"
        makeCol "released" "System.DateTime"
    ]
    let json = buildIndexMappingJson cols
    let expected = """{"mappings": {"properties": {"id": {"type": "integer"}, "title": {"type": "text"}, "released": {"type": "date"}}}}"""
    test <@ json = expected @>

[<Test>]
let ``Build Reindex JSON generates correct reindex payload`` () =
    let json = buildReindexJson "movies_v1" "movies_v2"
    let expected = """{"source": {"index": "movies_v1"}, "dest": {"index": "movies_v2"}}"""
    test <@ json = expected @>
