#r "nuget: DuckDB.NET.Data, 1.5.3"
open DuckDB.NET.Data
open System.IO
open System.Data.Common

let dbPath = Path.GetFullPath("src/Tests/TestData/AdventureWorksLT.db")
printfn "DB Path: %s" dbPath
let conn = new DuckDBConnection("Data Source=:memory:")
conn.Open()
let cmd = conn.CreateCommand()
cmd.CommandText <- sprintf "INSTALL sqlite; LOAD sqlite; ATTACH '%s' AS main (TYPE sqlite);" dbPath
try
    cmd.ExecuteNonQuery() |> ignore
    printfn "Successfully attached!"
    cmd.CommandText <- "SELECT * FROM main.Customer LIMIT 1;"
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        printfn "Read customer: %O" (reader.GetValue(1))
with e ->
    printfn "Error: %O" e
