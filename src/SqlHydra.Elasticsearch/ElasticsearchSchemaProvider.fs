module SqlHydra.Elasticsearch.ElasticsearchSchemaProvider

open SqlHydra.Domain
open System.Net.Http
open System.Text.Json
open System.Collections.Generic

let private getMappableType (esType: string) =
    match esType.ToLowerInvariant() with
    | "text" | "keyword" -> "System.String"
    | "integer" | "short" | "byte" -> "System.Int32"
    | "long" -> "System.Int64"
    | "float" -> "System.Single"
    | "double" -> "System.Double"
    | "boolean" -> "System.Boolean"
    | "date" -> "System.DateTime"
    | "binary" -> "System.Byte[]"
    | "scaled_float" -> "System.Decimal"
    | _ -> "System.String"

let getSchema (cfg: Config, isLegacy: bool, extensions: IExtendTypeMapping list) : Schema =
    use client = new HttpClient()
    let response = client.GetStringAsync("http://localhost:9200/_mapping").Result
    let root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response)
    
    let tables = ResizeArray<Table>()
    let tableColumns = Dictionary<Table, Column list>()

    for index in root.Keys do
        // Filter out system indices
        if not (index.StartsWith(".")) then
            let mapping = root.[index].GetProperty("mappings")
            if mapping.TryGetProperty("properties") |> fst then
                let properties = mapping.GetProperty("properties")
                let columns = ResizeArray<Column>()
                
                for prop in properties.EnumerateObject() do
                    let propName = prop.Name
                    
                    let propType = 
                        if prop.Value.TryGetProperty("type") |> fst then
                            prop.Value.GetProperty("type").GetString()
                        else
                            "text" // default
                            
                    let typeMapping = {
                        ClrType = getMappableType propType
                        DbType = System.Data.DbType.String // Mock for ES SQL
                        ProviderDbType = None
                        ColumnTypeAlias = propType
                    }
                    
                    let col = {
                        Name = propName
                        TypeMapping = typeMapping
                        IsNullable = true // ES fields are usually optional
                        IsPK = (propName.ToLower() = "id")
                    }
                    columns.Add(col)

                let table = {
                    Catalog = "es"
                    Schema = "public"
                    Name = index
                    Type = TableType.Table
                    Columns = columns |> List.ofSeq
                    TotalColumns = columns.Count
                }
                tables.Add(table)
                tableColumns.[table] <- columns |> List.ofSeq

    {
        Tables = tables |> List.ofSeq
        Enums = []
    }
