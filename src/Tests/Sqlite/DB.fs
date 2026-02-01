module Sqlite.DB

open System.IO

#if NET8_0
open Sqlite.AdventureWorksNet8
#endif
#if NET9_0
open Sqlite.AdventureWorksNet9
#endif
#if NET10_0
open Sqlite.AdventureWorksNet10
#endif


let connectionString =     
    let assembly = System.Reflection.Assembly.GetExecutingAssembly().Location |> System.IO.FileInfo
    let thisDir = assembly.Directory.Parent.Parent.Parent.FullName
    let dbPath = System.IO.Path.Combine(thisDir, "TestData", "AdventureWorksLT.db")
    let dbTempPath = dbPath.Replace(".db", "_Temp.db")

    // Create a temp copy of sqlite db for testing
    File.Copy(dbPath, dbTempPath, true)

    $"Data Source={dbTempPath}"

let db = QueryContextFactory.Create(connectionString, printf "SQL: %O")
//let getConnection() = 
//    new SqliteConnection(connectionString)

//let openConnection() = 
//    let conn = getConnection()
//    conn.Open()
//    conn

let toSql (query: SqlHydra.Query.SelectQuery) = 
    let compiler = SqlKata.Compilers.SqliteCompiler()
    let sql = compiler.Compile(query.ToKataQuery()).Sql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql
