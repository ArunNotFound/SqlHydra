module SqlHydra.DuckDb.DuckDbDataTypes

open SqlHydra.Domain
open System.Data

let tryFindTypeMapping (ctx: TypeMappingContext) =
    let colType = ctx.Column.ProviderTypeName.ToUpperInvariant()
    
    // Simplistic mapping for DuckDB
    let clrType, dbType = 
        match colType with
        | t when t.Contains("INT") || t = "INTEGER" -> "int", DbType.Int32
        | t when t.Contains("BIGINT") || t = "HUGEINT" -> "int64", DbType.Int64
        | t when t.Contains("TINYINT") -> "byte", DbType.Byte
        | t when t.Contains("SMALLINT") -> "int16", DbType.Int16
        | t when t.Contains("DOUBLE") || t = "REAL" || t = "FLOAT" -> "double", DbType.Double
        | t when t.Contains("DECIMAL") || t = "NUMERIC" -> "decimal", DbType.Decimal
        | t when t.Contains("VARCHAR") || t = "TEXT" || t = "STRING" -> "string", DbType.String
        | t when t.Contains("BLOB") || t = "BYTEA" -> "byte array", DbType.Binary
        | t when t.Contains("BOOLEAN") || t = "BOOL" -> "bool", DbType.Boolean
        | t when t.Contains("DATE") -> "System.DateTime", DbType.Date
        | t when t.Contains("TIME") -> "System.TimeSpan", DbType.Time
        | t when t.Contains("TIMESTAMP") -> "System.DateTime", DbType.DateTime
        | t when t.Contains("UUID") -> "System.Guid", DbType.Guid
        | _ -> "string", DbType.String // Fallback

    Some {
        ClrType = clrType
        DbType = dbType
        ProviderDbType = None
        ColumnTypeAlias = colType
    }
