module internal SqlHydra.Query.InsertOrUpdateOnUnique

open System

/// Brackets a table name like "dbo.ErrorLog" to "[dbo].[ErrorLog]".
let private bracketTable (table: string) =
    table.Split('.')
    |> Array.map (fun part -> $"[{part}]")
    |> String.concat "."

/// Builds a TRY/CATCH upsert SQL batch and additional parameters for the UPDATE fallback.
/// On duplicate key (error 2627/2601), updates the specified columns; retries the INSERT if
/// the row vanished between the failed INSERT and the UPDATE (concurrent delete race).
let apply
    (tableName: string)
    (keyColumns: string list)
    (updateColumns: string list)
    (insertCmdText: string)
    (existingParams: Data.IDbDataParameter list)
    (createParam: string -> obj -> Data.IDbDataParameter)
    (getColumnValue: string -> obj)
    : string * Data.IDbDataParameter list =

    let bracketedTable = bracketTable tableName

    let updateSetClause =
        updateColumns
        |> List.map (fun col -> $"[{col}] = @__update_{col}")
        |> fun parts -> String.Join(", ", parts)

    let whereClause =
        keyColumns
        |> List.map (fun col -> $"t.[{col}] = @__key_{col}")
        |> fun parts -> String.Join(" AND ", parts)

    let finalSql =
        $"""
BEGIN TRY
    {insertCmdText}
END TRY
BEGIN CATCH
    DECLARE @err INT = ERROR_NUMBER();
    IF @err NOT IN (2627, 2601) THROW;

    UPDATE t SET {updateSetClause}
    FROM {bracketedTable} AS t
    WHERE {whereClause};

    IF @@ROWCOUNT = 0
    BEGIN
        {insertCmdText}
    END
END CATCH;"""

    let additionalParams =
        [
            for col in updateColumns do
                createParam $"@__update_{col}" (getColumnValue col)
            for col in keyColumns do
                createParam $"@__key_{col}" (getColumnValue col)
        ]

    finalSql, existingParams @ additionalParams
