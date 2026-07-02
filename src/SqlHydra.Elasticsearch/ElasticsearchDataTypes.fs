namespace SqlHydra.Elasticsearch

open SqlHydra.Domain

module ElasticsearchDataTypes =
    
    /// Maps Elasticsearch types to F# types
    let getMappableType (esType: string) =
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
        | _ -> "System.String" // Fallback for complex nested types in ES SQL flavor

    /// Gets F# type from Elasticsearch column metadata
    let getClrType (dataType: string, isNullable: bool) =
        let clrType = getMappableType dataType
        if isNullable then
            clrType
        else
            clrType
