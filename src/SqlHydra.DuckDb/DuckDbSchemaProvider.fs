module SqlHydra.DuckDb.DuckDbSchemaProvider

open DuckDB.NET.Data
open SqlHydra.Domain

type private SchemaRow = {
    Catalog: string
    Schema: string
    Table: string
    Type: string
    Column: ColumnSchema
}

let private readSchema (conn: DuckDBConnection) =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        SELECT
            c.table_catalog,
            c.table_schema,
            c.table_name,
            t.table_type,
            c.column_name,
            c.data_type,
            c.ordinal_position,
            c.is_nullable
        FROM information_schema.columns c
        JOIN information_schema.tables t 
          ON c.table_catalog = t.table_catalog 
         AND c.table_schema = t.table_schema 
         AND c.table_name = t.table_name
        WHERE t.table_type IN ('BASE TABLE', 'VIEW')
          AND c.table_schema NOT IN ('information_schema', 'pg_catalog')
        ORDER BY c.table_catalog, c.table_schema, c.table_name, c.ordinal_position
    """
    
    use reader = cmd.ExecuteReader()
    let catalog = reader.GetOrdinal("table_catalog")
    let schema = reader.GetOrdinal("table_schema")
    let table = reader.GetOrdinal("table_name")
    let ttype = reader.GetOrdinal("table_type")
    let col = reader.GetOrdinal("column_name")
    let dtype = reader.GetOrdinal("data_type")
    let ord = reader.GetOrdinal("ordinal_position")
    let isNull = reader.GetOrdinal("is_nullable")

    [ while reader.Read() do
        let c = if reader.IsDBNull(catalog) then "" else reader.GetString(catalog)
        let s = if reader.IsDBNull(schema) then "" else reader.GetString(schema)
        let t = reader.GetString(table)
        let tt = reader.GetString(ttype)
        let cn = reader.GetString(col)
        let dt = reader.GetString(dtype)
        let o = reader.GetInt32(ord)
        let nullableStr = reader.GetString(isNull)
        let isNullable = (nullableStr = "YES")

        yield {
            Catalog = c
            Schema = s
            Table = t
            Type = tt
            Column = {
                Catalog = c
                Schema = s
                Table = t
                Name = cn
                ProviderTypeName = dt
                Ordinal = o
                IsNullable = isNullable
                IsPrimaryKey = false // DuckDB information_schema doesn't easily expose PK info yet
                Precision = None
                Scale = None
                IsComputed = false
                DefaultValue = None
            }
        } ]

let getSchema (cfg: Config, isLegacy: bool, extensions: IExtendTypeMapping list) : Schema =
    use conn = new DuckDBConnection(cfg.ConnectionString)
    conn.Open()

    // If we want to automatically load sqlite and attach it
    // if cfg.ConnectionString includes it, we could do it here, 
    // but typically DuckDB config will handle it or we execute it explicitly.

    let rows = readSchema conn

    let columnsByTable =
        rows
        |> List.map _.Column
        |> List.sortBy _.Ordinal
        |> Seq.groupBy (fun c -> c.Catalog, c.Schema, c.Table)
        |> Map.ofSeq

    let tryFindTypeMapping =
        let baseTryFind = DuckDbDataTypes.tryFindTypeMapping
        extensions |> List.fold (fun acc (ext: IExtendTypeMapping) -> ext.Extend(acc)) baseTryFind

    let tablesList = 
        rows
        |> List.map (fun r -> r.Catalog, r.Schema, r.Table, r.Type)
        |> List.distinct
    
    let tableSchemas =
        tablesList
        |> List.map (fun (c, s, t, tt) ->
            let cols =
                columnsByTable
                |> Map.tryFind (c, s, t)
                |> Option.map Seq.toList
                |> Option.defaultValue []
            
            {
                TableSchema.Catalog = c
                Schema = s
                Name = t
                Type = if tt.Equals("VIEW", System.StringComparison.OrdinalIgnoreCase) then TableType.View else TableType.Table
                Columns = cols
            }
        )

    let tables =
        tableSchemas
        |> Seq.choose (fun tableSchema ->
            let supportedColumns =
                tableSchema.Columns
                |> List.choose (fun col ->
                    let ctx = { TypeMappingContext.Table = tableSchema; TypeMappingContext.Column = col }
                    tryFindTypeMapping ctx
                    |> Option.map (fun typeMapping ->
                        {
                            Column.Name = col.Name
                            Column.IsNullable = col.IsNullable
                            Column.TypeMapping = typeMapping
                            Column.IsPK = col.IsPrimaryKey
                        }
                    )
                )

            if supportedColumns |> List.isEmpty then None
            else
                Some {
                    Table.Catalog = tableSchema.Catalog
                    Table.Schema = tableSchema.Schema
                    Table.Name = tableSchema.Name
                    Table.Type = tableSchema.Type
                    Table.Columns = supportedColumns
                    Table.TotalColumns = tableSchema.Columns |> List.length
                }
        )
        |> Seq.toList

    {
        Tables = tables
        Enums = []
    }
