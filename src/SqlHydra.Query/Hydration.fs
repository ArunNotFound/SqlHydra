module SqlHydra.Query.Hydration

open System
open System.Data.Common
open FSharp.Reflection

/// Tracks ordinal position across joined tables.
[<Sealed>]
type OrdinalTracker(reader: DbDataReader) =
    let mutable accFieldCount = 0

    member _.Reader = reader

    /// Builds an ordinal lookup for a record type's fields, advancing the ordinal counter.
    member _.BuildGetOrdinal(tableType: Type) =
        let fieldNames =
            FSharpType.GetRecordFields(tableType)
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
        dictionary

    /// Gets the next ordinal and increments the counter.
    member _.GetOrdinalAndIncrement() =
        let ordinal = accFieldCount
        accFieldCount <- accFieldCount + 1
        ordinal

/// Module containing methods accessed via reflection. Must be a static class.
type ColumnReadMethods private () =
    static member ReadRequired<'T>(reader: DbDataReader, ordinal: int) : obj =
        reader.GetFieldValue<'T>(ordinal) |> box

    static member ReadOption<'T>(reader: DbDataReader, ordinal: int) : obj =
        if reader.IsDBNull(ordinal) then box None
        else reader.GetFieldValue<'T>(ordinal) |> Some |> box

    static member ReadNullableStruct<'T when 'T : struct and 'T :> ValueType and 'T : (new: unit -> 'T)>(reader: DbDataReader, ordinal: int) : obj =
        if reader.IsDBNull(ordinal) then Nullable() |> box
        else Nullable(reader.GetFieldValue<'T>(ordinal)) |> box

    static member ReadNullableObj<'T when 'T : not struct>(reader: DbDataReader, ordinal: int) : obj =
        if reader.IsDBNull(ordinal) then null |> box
        else reader.GetFieldValue<'T>(ordinal) |> box

/// Determines if a type is Option<_>.
let private isOptionType (t: Type) =
    t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>>

/// Determines if a type is Nullable<_>.
let private isNullableType (t: Type) =
    t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Nullable<_>>

/// Unwraps Option<T> or Nullable<T> to get the inner type T.
let private unwrapType (t: Type) =
    if isOptionType t then t.GenericTypeArguments.[0]
    elif isNullableType t then t.GenericTypeArguments.[0]
    else t

/// Checks if a type is a primitive/scalar (not an F# record).
let private isPrimitive (t: Type) =
    let inner = unwrapType t
    not (FSharpType.IsRecord inner)

/// Gets a function that reads a single column value from the reader at a given ordinal.
let private makeColumnReader (reader: DbDataReader) (baseType: Type) (isOpt: bool) (isNullable: bool) : (int -> obj) =
    let methodName =
        if isOpt then "ReadOption"
        elif isNullable && baseType.IsValueType then "ReadNullableStruct"
        elif isNullable && not baseType.IsValueType then "ReadNullableObj"
        else "ReadRequired"

    let methodInfo =
        typeof<ColumnReadMethods>.GetMethod(methodName).MakeGenericMethod(baseType)

    fun (ordinal: int) ->
        methodInfo.Invoke(null, [| reader :> obj; ordinal :> obj |])

/// Builds field readers for a record type using pre-computed ordinals.
let private buildRecordFieldReaders (reader: DbDataReader) (recordType: Type) (ordinalLookup: System.Collections.Generic.IDictionary<string, int>) =
    let fields = FSharpType.GetRecordFields(recordType)
    fields
    |> Array.map (fun pi ->
        let fieldType = pi.PropertyType
        let isOpt = isOptionType fieldType
        let isNullable = isNullableType fieldType
        let baseType = unwrapType fieldType
        // Reference types (e.g. string, byte[]) can be NULL in SQL even without Option/Nullable wrapper
        let isNullable = isNullable || (not isOpt && baseType.IsClass)
        let columnReader = makeColumnReader reader baseType isOpt isNullable
        let ordinal = ordinalLookup.[pi.Name]
        (ordinal, columnReader)
    )

/// Builds a read function for a single entity type that may be:
/// - A primitive/scalar type (Option<int>, string, int, etc.)
/// - An Option<Record> (for left joins)
/// - A record type
let private buildEntityReadFn (tracker: OrdinalTracker) (entityType: Type) : (unit -> obj) =
    let reader = tracker.Reader
    let isOpt = isOptionType entityType
    let isNullable = isNullableType entityType
    let innerType = unwrapType entityType

    if FSharpType.IsRecord innerType then
        // Record type (possibly wrapped in Option for left joins)
        let ordinalLookup = tracker.BuildGetOrdinal(innerType)
        let fieldReaders = buildRecordFieldReaders reader innerType ordinalLookup

        if isOpt then
            // Option<Record> — left join: check first column for DBNull → return None
            let firstOrdinal = fst fieldReaders.[0]
            let someCase = FSharpType.GetUnionCases(entityType) |> Array.find (fun c -> c.Name = "Some")
            let noneCase = FSharpType.GetUnionCases(entityType) |> Array.find (fun c -> c.Name = "None")
            let noneValue = FSharpValue.MakeUnion(noneCase, [||])

            fun () ->
                if reader.IsDBNull(firstOrdinal) then
                    noneValue
                else
                    let values = fieldReaders |> Array.map (fun (ord, read) -> read ord)
                    let record = FSharpValue.MakeRecord(innerType, values)
                    FSharpValue.MakeUnion(someCase, [| record |])
        else
            // Plain record type
            fun () ->
                let values = fieldReaders |> Array.map (fun (ord, read) -> read ord)
                FSharpValue.MakeRecord(innerType, values)
    else
        // Scalar/primitive type (possibly wrapped in Option/Nullable)
        let baseType = unwrapType entityType
        let columnReader = makeColumnReader reader baseType isOpt isNullable
        let ordinal = tracker.GetOrdinalAndIncrement()
        fun () -> columnReader ordinal

/// Builds a function that reads one row from the reader and returns 'T.
/// Called once per query (after reader is opened), returned fn is called per row.
let buildRowReader<'T> (reader: DbDataReader) : (unit -> 'T) =
    let t = typeof<'T>
    let tracker = OrdinalTracker(reader)

    if FSharpType.IsTuple(t) then
        let elementTypes = FSharpType.GetTupleElements(t)
        let readFns = elementTypes |> Array.map (buildEntityReadFn tracker)
        fun () ->
            let values = readFns |> Array.map (fun read -> read())
            FSharpValue.MakeTuple(values, t) :?> 'T
    else
        let readFn = buildEntityReadFn tracker t
        fun () ->
            readFn() :?> 'T

/// Builds a function for selectExpr queries using leaf metadata.
let internal buildSelectExprReader (reader: DbDataReader) (exprInfo: LinqExpressionVisitors.SelectExprInfo) : (unit -> obj[]) =
    let tracker = OrdinalTracker(reader)
    let leafTupleType = exprInfo.LeafTupleType

    if FSharpType.IsTuple(leafTupleType) then
        let elementTypes = FSharpType.GetTupleElements(leafTupleType)
        let readFns = elementTypes |> Array.map (buildEntityReadFn tracker)
        fun () -> readFns |> Array.map (fun read -> read())
    else
        let readFn = buildEntityReadFn tracker leafTupleType
        fun () -> [| readFn() |]
