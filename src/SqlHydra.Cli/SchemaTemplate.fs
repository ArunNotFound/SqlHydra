module SqlHydra.SchemaTemplate

open Domain

let backticks = Fantomas.FCS.Syntax.PrettyNaming.NormalizeIdentifierBackticks
let newLine = "\n"

let versionModule (cfg: Config) (v: Version.InformationalVersion) = $"""
module Version =
    let cli = System.Version({v.Version.Major}, {v.Version.Minor}, {v.Version.Build})
    let ns = "%s{cfg.Namespace}"
    SqlHydra.Query.VersionCheck.assertIsCompatible cli ns
"""

let columnReadersModule = $"""
[<AutoOpen>]
module ColumnReaders =
    type Column(reader: System.Data.IDataReader, getOrdinal: string -> int, column) =
        member __.Name = column
        member __.IsNull() = getOrdinal column |> reader.IsDBNull
        override __.ToString() = __.Name

    type RequiredColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
        inherit Column(reader, getOrdinal, column)
        member __.Read(?alias) = alias |> Option.defaultValue __.Name |> getOrdinal |> getter

    type OptionColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
        inherit Column(reader, getOrdinal, column)
        member __.Read(?alias) = 
            match alias |> Option.defaultValue __.Name |> getOrdinal with
            | o when reader.IsDBNull o -> None
            | o -> Some (getter o)

    type NullableObjectColumn<'T, 'Reader when 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
        inherit Column(reader, getOrdinal, column)
        member __.Read(?alias) = 
            match alias |> Option.defaultValue __.Name |> getOrdinal with
            | o when reader.IsDBNull o -> null
            | o -> (getter o) |> unbox

    type NullableValueColumn<'T, 'Reader when 'T : struct and 'T : (new : unit -> 'T) and 'T :> System.ValueType and 'Reader :> System.Data.IDataReader>(reader: 'Reader, getOrdinal, getter: int -> 'T, column) =
        inherit Column(reader, getOrdinal, column)
        member __.Read(?alias) = 
            match alias |> Option.defaultValue __.Name |> getOrdinal with
            | o when reader.IsDBNull o -> System.Nullable<'T>()
            | o -> System.Nullable<'T> (getter o)
"""

let modernDateExtensionsModule = $"""
[<AutoOpen>]
module private DataReaderExtensions =
    type System.Data.IDataReader with
        member reader.GetDateOnly(ordinal: int) = 
            reader.GetDateTime(ordinal) |> System.DateOnly.FromDateTime
    
    type System.Data.Common.DbDataReader with
        member reader.GetTimeOnly(ordinal: int) = 
            reader.GetFieldValue(ordinal) |> System.TimeOnly.FromTimeSpan

"""

let mkEnum db schema enum = stringBuffer {
    let enumType = 
        db.Enums 
        |> List.find (fun e -> e.Schema = schema && e.Name = enum)

    let labels = 
        enumType.Labels 
        |> List.sortBy _.SortOrder

    $"type {backticks enumType.Name} ="
    indent {
        for label in labels do
            $"| {backticks label.Name} = {label.SortOrder}"
    }
}

let mkTable cfg db (table: Table) schema = stringBuffer {
    let tableType = 
        db.Tables 
        |> List.find (fun t -> t.Schema = schema && t.Name = table.Name)

    if cfg.IsCLIMutable then "[<CLIMutable>]"

    $"type {backticks table.Name} ="
    indent {
        "{"
        indent {
            for col in tableType.Columns do
                let baseType = 
                    // Handles array types: "byte[]", "string[]", "int[]", "int []", "int array"
                    if col.TypeMapping.ClrType.EndsWith "[]" || col.TypeMapping.ClrType.EndsWith "array" then
                        let baseTypeNm = col.TypeMapping.ClrType.Split([| "[]"; " []"; " array" |], System.StringSplitOptions.RemoveEmptyEntries) |> Array.head
                        $"{baseTypeNm} []"
                    else
                        col.TypeMapping.ClrType

                let columnPropertyType =
                    if col.IsNullable then
                        match cfg.NullablePropertyType with
                        | NullablePropertyType.Option ->
                            $"Option<{baseType}>"
                        | NullablePropertyType.Nullable ->
                            if col.TypeMapping.IsValueType() 
                            then $"System.Nullable<{baseType}>"
                            else baseType
                    else 
                        baseType

                let providerDbTypeAttribute =
                    match col.TypeMapping.ProviderDbType with
                    | Some providerDbType when cfg.ProviderDbTypeAttributes -> 
                        Some $"[<ProviderDbType(\"{providerDbType}\")>]"
                    | _ -> 
                        None

                if providerDbTypeAttribute.IsSome then providerDbTypeAttribute.Value
                $"""{if cfg.IsMutableProperties then "mutable " else ""}{backticks col.Name}: {columnPropertyType}"""
        }
        "}"
    }
}

let mkCompiler (providerType: ProviderType) =
    match providerType with
    | ProviderType.SqlServer -> "SqlKata.Compilers.SqlServerCompiler()"
    | ProviderType.Npgsql -> "SqlKata.Compilers.PostgresCompiler()"
    | ProviderType.MySql -> "SqlKata.Compilers.MySqlCompiler()"
    | ProviderType.Sqlite -> "SqlKata.Compilers.SqliteCompiler()"
    | ProviderType.Oracle -> "SqlKata.Compilers.OracleCompiler()"

let mkConnectionType (providerType: ProviderType) =
    match providerType with
    | ProviderType.SqlServer -> "Microsoft.Data.SqlClient.SqlConnection"
    | ProviderType.Npgsql -> "Npgsql.NpgsqlConnection"
    | ProviderType.MySql -> "MySql.Data.MySqlClient.MySqlConnection"
    | ProviderType.Sqlite -> "Microsoft.Data.Sqlite.SqliteConnection"
    | ProviderType.Oracle -> "Oracle.ManagedDataAccess.Client.OracleConnection"

let generate (cfg: Config) (provider: Provider) (db: Schema) (version: Version.InformationalVersion) isLegacy = stringBuffer {
    let filteredTables = 
        db.Tables 
        |> List.sortBy (fun tbl -> tbl.Schema, tbl.Name)

    let schemas = 
        let enumSchemas = db.Enums |> List.map (fun e -> e.Schema)
        let tableSchemas = filteredTables |> List.map (fun t -> t.Schema) 
        enumSchemas @ tableSchemas |> List.distinct

    $$"""
// This code was generated by `{{provider.Name}}` -- v%%s{{version.InformationalVersion}}.
namespace {{cfg.Namespace}}
    """

    if cfg.ProviderDbTypeAttributes then
        "open SqlHydra"

    if cfg.TableDeclarations then
        "open SqlHydra.Query.Table"

    versionModule cfg version

    if cfg.Readers.IsSome then 
        columnReadersModule

        if not isLegacy then 
            modernDateExtensionsModule

    for schema in schemas do
        $"module {backticks schema} ="

        let enums = 
            db.Enums 
            |> List.filter (fun e -> e.Schema = schema)
            |> List.map _.Name

        indent {
            for enum in enums do 
                mkEnum db schema enum
                newLine
        }

        let tables = 
            filteredTables 
            |> List.filter (fun t -> t.Schema = schema)

        indent {
            for table in tables do
                mkTable cfg db table schema
                newLine

                if cfg.TableDeclarations then 
                    $"let {backticks table.Name} = table<{backticks table.Name}>"
                    newLine
        }

        if cfg.Readers.IsSome then 
            let reader = cfg.Readers.Value
            indent {
                $"module Readers ="
                indent {
                    for table in tables do 
                        let readerClassName = $"{table.Name}Reader"
                        $"type {backticks readerClassName}(reader: {reader.ReaderType}, getOrdinal) ="
                        indent {
                            for col in table.Columns do 
                                let columnReaderType =
                                    if col.IsNullable then 
                                        match cfg.NullablePropertyType with
                                        | NullablePropertyType.Option ->
                                            "OptionColumn"                  // Returns None for DBNull.Value
                                        | NullablePropertyType.Nullable ->
                                            if col.TypeMapping.IsValueType() 
                                            then "NullableValueColumn"      // Returns System.Nullable<> for DBNull.Value
                                            else "NullableObjectColumn"     // Returns null for DBNull.Value
                                    else 
                                        "RequiredColumn"

                                $"member __.{backticks col.Name} = {columnReaderType}(reader, getOrdinal, reader.{col.TypeMapping.ReaderMethod}, \"{col.Name}\")"

                            "member __.Read() ="
                            indent {
                                "{"
                                indent {
                                    for col in table.Columns do 
                                        $"{backticks col.Name} = __.{backticks col.Name}.Read()"
                                }
                                $$"""} : {{backticks table.Name}}"""
                            }

                            "member __.ReadIfNotNull() ="
                            indent {
                                $"if __.{backticks table.Columns.Head.Name}.IsNull() then None else Some(__.Read())"
                            }
                        }
                        newLine
                }
            }

    // Create "HydraReader" below all generated tables/readers...
    if cfg.Readers.IsSome then
        let reader = cfg.Readers.Value
        let allTables = schemas |> List.collect (fun schema -> filteredTables |> List.filter (fun t -> t.Schema = schema))
        $"type HydraReader(reader: {cfg.Readers.Value.ReaderType}) ="
        indent {
            """
let mutable accFieldCount = 0
let buildGetOrdinal tableType =
    let fieldNames = 
        FSharp.Reflection.FSharpType.GetRecordFields(tableType)
        |> Array.map _.Name

    let dictionary = 
        [| 0 .. reader.FieldCount - 1 |] 
        |> Array.map (fun i -> reader.GetName(i), i)
        |> Array.sortBy snd
        |> Array.skip accFieldCount
        |> Array.filter (fun (name, _) -> Array.contains name fieldNames)
        |> Array.take fieldNames.Length
        |> dict
    accFieldCount <- accFieldCount + fieldNames.Length
    fun col -> dictionary.Item col
            """

            // Create public reader properties
            // Ex: member __.``HumanResources.Department`` = HumanResources.Readers.DepartmentReader(reader, buildGetOrdinal typeof<HumanResources.Department>)
            for table in allTables do
                let schemaTable = $"{table.Schema}.{table.Name}"
                let readerClassName = $"{table.Name}Reader"
                $"member __.{backticks schemaTable} = {backticks table.Schema}.Readers.{backticks readerClassName}(reader, buildGetOrdinal typeof<{backticks table.Schema}.{backticks table.Name}>)"

            newLine

            // AccFieldCount property
            "member private __.AccFieldCount with get () = accFieldCount and set (value) = accFieldCount <- value"
            newLine

            // Method: member private __.GetReaderByName(entity: string, isOption: bool) =
            "member private __.GetReaderByName(entity: string, isOption: bool) ="
            indent { 
                "match entity, isOption with"
                for table in allTables do
                    // | "OT.CONTACTS", false -> __.``OT.CONTACTS``.Read >> box
                    let schemaTable = $"{table.Schema}.{table.Name}"
                    $"| \"{table.Schema}.{table.Name}\", false -> __.{backticks schemaTable}.Read >> box"
                    $"| \"{table.Schema}.{table.Name}\", true -> __.{backticks schemaTable}.ReadIfNotNull >> box"

                $$"""| _ -> failwith $"Could not read type '{entity}' because no generated reader exists." """
                
            }
            newLine 

            // Method: static member private GetPrimitiveReader(t: System.Type, reader: Microsoft.Data.SqlClient.SqlDataReader, isOpt: bool, isNullable: bool) =// Method: member __.Read(entity: string, isOption: bool) = 
            $"static member private GetPrimitiveReader(t: System.Type, reader: {reader.ReaderType}, isOpt: bool, isNullable: bool) ="
            indent {
                """
let wrapValue get (ord: int) = 
    if isOpt then (if reader.IsDBNull ord then None else get ord |> Some) |> box 
    elif isNullable then (if reader.IsDBNull ord then System.Nullable() else get ord |> System.Nullable) |> box
    else get ord |> box

let wrapRef get (ord: int) = 
    if isOpt then (if reader.IsDBNull ord then None else get ord |> Some) |> box 
    else get ord |> box
                """

                let wrapFnName (ptr: PrimitiveTypeReader) = 
                    if ptr.ClrType |> isValueType
                    then "wrapValue"
                    else "wrapRef"

                    
                // Exclude the SqlHierarchyId primitive type reader if the schema doesn't use it because it requires an addition NuGet package.
                let primitiveTypeReaders = 
                    let hierarchyIdClrType = "Microsoft.SqlServer.Types.SqlHierarchyId"

                    let schemaHasHierarchyIdColumns = 
                        db.Tables
                        |> Seq.collect _.Columns
                        |> Seq.exists (fun c -> c.TypeMapping.ClrType = hierarchyIdClrType)

                    if schemaHasHierarchyIdColumns 
                    then db.PrimitiveTypeReaders
                    else db.PrimitiveTypeReaders |> Seq.filter (fun ptr -> ptr.ClrType <> hierarchyIdClrType)
                
                for i, ptr in primitiveTypeReaders |> Seq.indexed do
                    let if_elif = if i = 0 then "if" else "elif"
                    let readerGetFieldValueMethod =
                        if ptr.ClrType.EndsWith "[]"
                        then $"GetFieldValue<{ptr.ClrType}>" // handles array types
                        else $"{ptr.ReaderMethod}"

                    $"{if_elif} t = typedefof<{ptr.ClrType}> then Some({wrapFnName ptr} reader.{readerGetFieldValueMethod})"

                "else None"
            }

            // Method: member __.Read(entity: string, isOption: bool) =
            $"""
static member Read(reader: {reader.ReaderType}) = 
    let hydra = HydraReader(reader)
    {if provider.Type = Oracle then "reader.SuppressGetDecimalInvalidCastException <- true" else ""}
                    
    let getOrdinalAndIncrement() = 
        let ordinal = hydra.AccFieldCount
        hydra.AccFieldCount <- hydra.AccFieldCount + 1
        ordinal
            
    let buildEntityReadFn (t: System.Type) = 
        let t, isOpt, isNullable = 
            if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>> then t.GenericTypeArguments[0], true, false
            elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<System.Nullable<_>> then t.GenericTypeArguments[0], false, true
            else t, false, false
            
        match HydraReader.GetPrimitiveReader(t, reader, isOpt, isNullable) with
        | Some primitiveReader -> 
            let ord = getOrdinalAndIncrement()
            fun () -> primitiveReader ord
        | None ->
            let nameParts =
                match t.FullName with
                | null -> failwith $"Type {{t}} has no FullName"
                | fullName -> fullName.Split([| '.'; '+' |])
            let schemaAndType = nameParts |> Array.skip (nameParts.Length - 2) |> fun parts -> System.String.Join(".", parts)
            hydra.GetReaderByName(schemaAndType, isOpt)
            
    // Return a fn that will hydrate 'T (which may be a tuple)
    // This fn will be called once per each record returned by the data reader.
    let t = typeof<'T>
    if FSharp.Reflection.FSharpType.IsTuple(t) then
        let readEntityFns = FSharp.Reflection.FSharpType.GetTupleElements(t) |> Array.map buildEntityReadFn
        fun () ->
            let entities = readEntityFns |> Array.map (fun read -> read())
            Microsoft.FSharp.Reflection.FSharpValue.MakeTuple(entities, t) :?> 'T
    else
        let readEntityFn = t |> buildEntityReadFn
        fun () -> 
            readEntityFn() :?> 'T
            """

            // If the user configures ProviderDbTypeAttributes, we know they are using SqlHydra.Query.
            if cfg.ProviderDbTypeAttributes then
                let compiler = mkCompiler provider.Type
                let connectionType = mkConnectionType provider.Type
                $"""
/// Provides select builders that use the generated HydraReader.Read.
module [<AutoOpen>] HydraBuilders =
    open SqlHydra.Query
    /// Builds a select query with a QueryContext - returns a Task query result
    let inline selectTask ct = selectTask<'Selected, 'Mapped, {reader.ReaderType}, _> HydraReader.Read ct

    /// Builds a select query with a QueryContext - returns an Async query result
    let inline selectAsync ct = selectAsync<'Selected, 'Mapped, {reader.ReaderType}, _> HydraReader.Read ct

open SqlHydra.Query

type QueryContextFactory =
    {{
        CreateConnection: unit -> System.Data.Common.DbConnection
        OpenContext: unit -> QueryContext
        OpenContextAsync: unit -> System.Threading.Tasks.Task<QueryContext>
    }}
    interface IQueryContextFactory with
        member this.CreateConnection() = this.CreateConnection()
        member this.OpenContext() = this.OpenContext()
        member this.OpenContextAsync() = this.OpenContextAsync()
    static member Create(connectionString: string, ?sqlLogger) =
        let compiler = {compiler}

        let createConn () : System.Data.Common.DbConnection =
            new {connectionType}(connectionString)

        let openContext () =
            let conn = createConn ()
            conn.Open()
            let ctx = new QueryContext(conn, compiler)
            sqlLogger |> Option.iter (fun logger -> ctx.Logger <- logger)
            ctx

        let openContextAsync () =
            task {{
                let conn = createConn ()
                do! conn.OpenAsync()
                let ctx = new QueryContext(conn, compiler)
                sqlLogger |> Option.iter (fun logger -> ctx.Logger <- logger)
                return ctx
            }}

        {{
            CreateConnection = createConn
            OpenContext = openContext
            OpenContextAsync = openContextAsync
        }}
    """

        }

}