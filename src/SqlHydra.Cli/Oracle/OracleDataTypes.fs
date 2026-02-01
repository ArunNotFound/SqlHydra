module SqlHydra.Oracle.OracleDataTypes

open System.Data
open SqlHydra.Domain

/// A list of supported column type mappings
let supportedTypeMappings =
    [   // https://docs.oracle.com/cd/B19306_01/win.102/b14306/appendixa.htm
        "PLS_INTEGER",                                  "int",                          DbType.Int32
        "LONG",                                         "int64",                        DbType.Int64
        "NUMBER",                                       "decimal",                      DbType.Decimal
        "FLOAT",                                        "double",                       DbType.Double
        "BINARY_DOUBLE",                                "double",                       DbType.Double
        "BINARY_FLOAT",                                 "System.Single",                DbType.Single
        "REAL",                                         "System.Single",                DbType.Single
        "ROWID",                                        "string",                       DbType.String
        "UROWID",                                       "string",                       DbType.String
        "VARCHAR",                                      "string",                       DbType.String
        "VARCHAR2",                                     "string",                       DbType.String
        "NVARCHAR",                                     "string",                       DbType.String
        "NVARCHAR2",                                    "string",                       DbType.String
        "CHAR",                                         "string",                       DbType.String
        "XMLType",                                      "string",                       DbType.String
        "NCHAR",                                        "string",                       DbType.StringFixedLength
        "TEXT",                                         "string",                       DbType.String
        "NTEXT",                                        "string",                       DbType.String
        "CLOB",                                         "string",                       DbType.String
        "NCLOB",                                        "string",                       DbType.String
        "DATE",                                         "System.DateTime",              DbType.Date
        "TIMESTAMP",                                    "System.DateTime",              DbType.Date
        "TIMESTAMP WITH LOCAL TIME ZONE",               "System.DateTime",              DbType.Date
        "TIMESTAMP WITH TIME ZONE",                     "System.DateTime",              DbType.Date
        "INTERVAL DAY TO SECOND",                       "System.TimeSpan",              DbType.Time

        for x in 0 .. 9 do
            $"TIMESTAMP({x})",                          "System.DateTime",              DbType.Date
            $"TIMESTAMP({x}) WITH LOCAL TIME ZONE",     "System.DateTime",              DbType.Date
            $"TIMESTAMP({x}) WITH TIME ZONE",           "System.DateTime",              DbType.Date
            for y in 0 .. 9 do
                $"INTERVAL DAY({x}) TO SECOND({y})",    "System.TimeSpan",              DbType.Time

        "BFILE",                                        "byte[]",                       DbType.Binary
        "BLOB",                                         "byte[]",                       DbType.Binary
        "LONG RAW",                                     "byte[]",                       DbType.Binary
        "RAW",                                          "byte[]",                       DbType.Binary
    ]

let typeMappingsByName =
    supportedTypeMappings
    |> List.map (fun (columnTypeAlias, clrType, dbType) ->
        columnTypeAlias,
        {
            TypeMapping.ColumnTypeAlias = columnTypeAlias
            TypeMapping.ClrType = clrType
            TypeMapping.DbType = dbType
            TypeMapping.ProviderDbType = None
        }
    )
    |> Map.ofList

let tryFindTypeMapping (providerTypeName: string, precisionMaybe: int option, scaleMaybe: int option) =
    typeMappingsByName.TryFind (providerTypeName.ToUpper())
    |> Option.map (fun mapping ->
        // Precision and scale defaults:
        // https://docs.oracle.com/cd/B28359_01/server.111/b28318/datatype.htm#CNCPT313
        let precision = precisionMaybe |> Option.defaultValue 38
        let scale = scaleMaybe |> Option.defaultValue 0

        // NUMBER -> CLR mappings:
        // https://docs.oracle.com/cd/B19306_01/gateways.102/b14270/apa.htm
        match mapping.ColumnTypeAlias, precision, scale with
        | "NUMBER", precision, 0 when 0 <= precision && precision < 6 ->
            { mapping with ClrType = "int16"; DbType = DbType.Int16 }

        | "NUMBER", precision, 0 when precision < 11 ->
            { mapping with ClrType = "int"; DbType = DbType.Int32 }

        | "NUMBER", precision, 0 when precision < 20 ->
            { mapping with ClrType = "int64"; DbType = DbType.Int64 }

        | "NUMBER", precision, 0 when precision >= 20 ->
            { mapping with ClrType = "decimal"; DbType = DbType.Decimal }

        | "NUMBER", precision, scale when scale >= 4 ->
            { mapping with ClrType = "decimal"; DbType = DbType.Decimal }

        | "NUMBER", precision, scale when 0 <= precision && precision < 8 && scale > 0 ->
            { mapping with ClrType = "System.Single"; DbType = DbType.Single }

        | "NUMBER", precision, scale when precision < 16 && scale > 0 ->
            { mapping with ClrType = "double"; DbType = DbType.Double }

        | "NUMBER", precision, scale when precision >= 16 && scale > 0 ->
            { mapping with ClrType = "decimal"; DbType = DbType.Decimal }

        | _ ->
            mapping
    )
