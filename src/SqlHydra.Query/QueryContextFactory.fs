namespace SqlHydra.Query

open System.Data.Common
open System.Threading.Tasks

type QueryContextFactory =
    {
        CreateConnection: unit -> DbConnection
        OpenContext: unit -> QueryContext
        OpenContextAsync: unit -> Task<QueryContext>
    }
    member this.OpenConnection() =
        let conn = this.CreateConnection()
        conn.Open()
        conn

    member this.OpenConnectionAsync() =
        task {
            let conn = this.CreateConnection()
            do! conn.OpenAsync()
            return conn
        }
