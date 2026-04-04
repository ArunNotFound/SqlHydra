namespace SqlHydra.Query

open System
open System.Text
open SqlHydra.Domain

// ─── SQL Server Emitter ───

type SqlServerEmitter() =
    inherit SqlEmitterBase()

    override _.QuoteIdentifier(name) = $"[{name}]"
    override _.ParameterPrefix = "@p"

    override this.EmitPagination(skip, take, sb, collector) =
        match skip, take with
        | Some s, Some t ->
            let skipParam = collector.Add(box s)
            let takeParam = collector.Add(box t)
            sb.Append($" OFFSET {skipParam} ROWS FETCH NEXT {takeParam} ROWS ONLY") |> ignore
        | None, Some t ->
            let skipParam = collector.Add(box 0)
            let takeParam = collector.Add(box t)
            sb.Append($" OFFSET {skipParam} ROWS FETCH NEXT {takeParam} ROWS ONLY") |> ignore
        | Some s, None ->
            let skipParam = collector.Add(box s)
            sb.Append($" OFFSET {skipParam} ROWS") |> ignore
        | None, None -> ()

    override _.EmitInsertIdentity(_field) =
        ";SELECT scope_identity() as Id"

    override _.EmitInsertOutput(outputFields, insertSql) =
        let outputCsv =
            outputFields
            |> List.map (fun f -> $"INSERTED.{f.ColumnName}")
            |> String.concat ", "
        let outputClause = $"\nOUTPUT {outputCsv}\n"
        let valuesIndex = insertSql.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase)
        if valuesIndex > -1 then
            insertSql.Insert(valuesIndex, outputClause)
        else
            insertSql + outputClause

    override _.EmitUpdateOutput(outputFields, updateSql) =
        let outputCsv =
            outputFields
            |> List.map (fun f -> $"INSERTED.{f.ColumnName}")
            |> String.concat ", "
        let outputClause = $"\nOUTPUT {outputCsv}\n"
        let whereIndex = updateSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)
        if whereIndex > -1 then
            updateSql.Insert(whereIndex, outputClause)
        else
            updateSql + outputClause

    override this.EmitInsertConflict(insertType, insertSql, _columns, _rows, collector) =
        match insertType with
        | InsertOrUpdateOnUnique (keyFields, updateFields) ->
            // SQL Server TRY/CATCH upsert pattern
            // NOTE: The actual parameter creation for update/key values happens in QueryContext
            // since we need access to the entity values. This just returns the base INSERT.
            // The TRY/CATCH wrapping is handled in QueryContext.PrepareInsertCommand.
            insertSql
        | _ -> insertSql

    interface ISqlEmitter with
        member _.Provider = SqlServer
        member this.EmitSelect(ir) = this.EmitSelectCore(ir)
        member this.EmitInsert(ir) = this.EmitInsertCore(ir)
        member this.EmitUpdate(ir) = this.EmitUpdateCore(ir)
        member this.EmitDelete(ir) = this.EmitDeleteCore(ir)

// ─── PostgreSQL Emitter ───

type PostgresEmitter() =
    inherit SqlEmitterBase()

    override _.QuoteIdentifier(name) = $"\"{name}\""
    override _.ParameterPrefix = "@p"

    override _.EmitInsertIdentity(field) =
        $" RETURNING \"{field}\";"

    override this.EmitInsertConflict(insertType, insertSql, columns, _rows, _collector) =
        match insertType with
        | OnConflictDoUpdate (conflictFields, updateFields) ->
            // Separate insert from identity query
            let insertQuery, identityQuery =
                match insertSql.Split([| ";" |], StringSplitOptions.RemoveEmptyEntries) with
                | [| iq; idq |] -> iq, idq
                | _ -> insertSql, ""

            let setLines =
                updateFields
                |> List.map (fun col -> $"{col}=EXCLUDED.\"{col}\"\n")
                |> fun lines -> String.Join(",", lines)
            let conflictCsv = String.Join(",", conflictFields)

            StringBuilder()
                .AppendLine(insertQuery)
                .AppendLine($"ON CONFLICT({conflictCsv}) DO UPDATE SET")
                .AppendLine(setLines).Append(";")
                .AppendLine(identityQuery)
                .ToString()

        | OnConflictDoNothing conflictFields ->
            let insertQuery, identityQuery =
                match insertSql.Split([| ";" |], StringSplitOptions.RemoveEmptyEntries) with
                | [| iq; idq |] -> iq, idq
                | _ -> insertSql, ""

            let conflictCsv = String.Join(",", conflictFields)

            StringBuilder()
                .AppendLine(insertQuery)
                .AppendLine($"ON CONFLICT({conflictCsv})")
                .AppendLine("DO NOTHING;")
                .AppendLine(identityQuery)
                .ToString()

        | _ -> insertSql

    interface ISqlEmitter with
        member _.Provider = Npgsql
        member this.EmitSelect(ir) = this.EmitSelectCore(ir)
        member this.EmitInsert(ir) = this.EmitInsertCore(ir)
        member this.EmitUpdate(ir) = this.EmitUpdateCore(ir)
        member this.EmitDelete(ir) = this.EmitDeleteCore(ir)

// ─── SQLite Emitter ───

type SqliteEmitter() =
    inherit SqlEmitterBase()

    override _.QuoteIdentifier(name) = $"\"{name}\""
    override _.ParameterPrefix = "@p"

    override this.EmitInsertConflict(insertType, insertSql, _columns, _rows, _collector) =
        match insertType with
        | InsertOrReplace ->
            insertSql.Replace("INSERT", "INSERT OR REPLACE")

        | OnConflictDoUpdate (conflictFields, updateFields) ->
            let insertQuery, identityQuery =
                match insertSql.Split([| ";" |], StringSplitOptions.RemoveEmptyEntries) with
                | [| iq; idq |] -> iq, idq
                | _ -> insertSql, ""

            let setLines =
                updateFields
                |> List.map (fun col -> $"{col}=EXCLUDED.\"{col}\"\n")
                |> fun lines -> String.Join(",", lines)
            let conflictCsv = String.Join(",", conflictFields)

            StringBuilder()
                .AppendLine(insertQuery)
                .AppendLine($"ON CONFLICT({conflictCsv}) DO UPDATE SET")
                .AppendLine(setLines).Append(";")
                .AppendLine(identityQuery)
                .ToString()

        | OnConflictDoNothing conflictFields ->
            let insertQuery, identityQuery =
                match insertSql.Split([| ";" |], StringSplitOptions.RemoveEmptyEntries) with
                | [| iq; idq |] -> iq, idq
                | _ -> insertSql, ""

            let conflictCsv = String.Join(",", conflictFields)

            StringBuilder()
                .AppendLine(insertQuery)
                .AppendLine($"ON CONFLICT({conflictCsv})")
                .AppendLine("DO NOTHING;")
                .AppendLine(identityQuery)
                .ToString()

        | _ -> insertSql

    interface ISqlEmitter with
        member _.Provider = Sqlite
        member this.EmitSelect(ir) = this.EmitSelectCore(ir)
        member this.EmitInsert(ir) = this.EmitInsertCore(ir)
        member this.EmitUpdate(ir) = this.EmitUpdateCore(ir)
        member this.EmitDelete(ir) = this.EmitDeleteCore(ir)

// ─── Oracle Emitter ───

type OracleEmitter() =
    inherit SqlEmitterBase()

    override _.QuoteIdentifier(name) = $"\"{name}\""
    override _.ParameterPrefix = ":p"

    override this.EmitPagination(skip, take, sb, collector) =
        match skip with
        | Some s ->
            let paramName = collector.Add(box s)
            sb.Append($" OFFSET {paramName} ROWS") |> ignore
        | None -> ()
        match take with
        | Some t ->
            let paramName = collector.Add(box t)
            sb.Append($" FETCH FIRST {paramName} ROWS ONLY") |> ignore
        | None -> ()

    override _.EmitInsertIdentity(field) =
        $" returning \"{field}\" into :outputParam"

    override this.EmitMultiRowInsert(table, columns, rows, collector) =
        // Oracle INSERT ALL syntax
        let quotedTable = this.QuoteDotted(table)
        let quotedCols = columns |> List.map this.QuoteIdentifier |> String.concat ", "
        let sb = StringBuilder()
        sb.AppendLine("INSERT ALL") |> ignore
        for row in rows do
            let paramNames = row |> Array.map (fun v -> collector.Add(v)) |> String.concat ", "
            sb.AppendLine($"INTO {quotedTable} ({quotedCols}) VALUES ({paramNames})") |> ignore
        sb.AppendLine("SELECT * FROM DUAL") |> ignore
        sb.ToString()

    interface ISqlEmitter with
        member _.Provider = Oracle
        member this.EmitSelect(ir) = this.EmitSelectCore(ir)
        member this.EmitInsert(ir) = this.EmitInsertCore(ir)
        member this.EmitUpdate(ir) = this.EmitUpdateCore(ir)
        member this.EmitDelete(ir) = this.EmitDeleteCore(ir)

// ─── MySQL Emitter ───

type MySqlEmitter() =
    inherit SqlEmitterBase()

    override _.QuoteIdentifier(name) = $"`{name}`"
    override _.ParameterPrefix = "@p"

    interface ISqlEmitter with
        member _.Provider = MySql
        member this.EmitSelect(ir) = this.EmitSelectCore(ir)
        member this.EmitInsert(ir) = this.EmitInsertCore(ir)
        member this.EmitUpdate(ir) = this.EmitUpdateCore(ir)
        member this.EmitDelete(ir) = this.EmitDeleteCore(ir)
