module SqlServer.DB

#if NET8_0
open SqlServer.AdventureWorksNet8
#endif
#if NET9_0
open SqlServer.AdventureWorksNet9
#endif
#if NET10_0
open SqlServer.AdventureWorksNet10
#endif

#if DOCKERHOST // devcontainer
let server = "mssql"
#else
let server = "localhost,12019"
#endif

let connectionString = $@"Server={server};Database=AdventureWorks;User=sa;Password=Password#123;Connect Timeout=3;TrustServerCertificate=True"
let db = QueryContextFactory.Create(connectionString, printf "SQL: %O")

let toSql (query: SqlHydra.Query.SelectQuery) = 
    let compiler = SqlKata.Compilers.SqlServerCompiler()
    let sql = compiler.Compile(query.ToKataQuery()).Sql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql

