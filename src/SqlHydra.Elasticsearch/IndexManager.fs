module SqlHydra.Elasticsearch.IndexManager

open SqlHydra.Domain

type EsField = {
    Name: string
    Type: string
    Properties: EsField list option
}

let mapClrTypeToEsType (clrType: string) =
    match clrType with
    | "System.String" -> "text" // Default to text for strings, could also be keyword
    | "System.Int32" -> "integer"
    | "System.Int64" -> "long"
    | "System.Single" -> "float"
    | "System.Double" -> "double"
    | "System.Boolean" -> "boolean"
    | "System.DateTime" -> "date"
    | "System.Byte[]" -> "binary"
    | "System.Decimal" -> "scaled_float"
    | _ -> "keyword"

let buildIndexMappingJson (columns: Column list) =
    let rec buildProperties (cols: Column list) =
        cols 
        |> List.map (fun c -> 
            let esType = mapClrTypeToEsType c.TypeMapping.ClrType
            sprintf """"%s": {"type": "%s"}""" c.Name esType
        )
        |> String.concat ", "
    
    let props = buildProperties columns
    sprintf """{"mappings": {"properties": {%s}}}""" props

let buildReindexJson (sourceIndex: string) (destIndex: string) =
    sprintf """{"source": {"index": "%s"}, "dest": {"index": "%s"}}""" sourceIndex destIndex
